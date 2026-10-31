using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using NoWayYahweh.Core;

namespace NoWayYahweh.Tests;

/// <summary>A throwaway HKCU key, so registry tests never touch real policies or Run entries.</summary>
public sealed class ScratchKey : IDisposable
{
    private readonly string _path = $@"Software\NoWayYahwehTests\{Guid.NewGuid():N}";
    public RegistryKey Root { get; }

    public ScratchKey() => Root = Registry.CurrentUser.CreateSubKey(_path);

    public void Dispose()
    {
        Root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
    }
}

public class StartupEntryTests
{
    [Fact]
    public void Enable_disable_round_trip_with_background_argument()
    {
        using var scratch = new ScratchKey();
        var entry = new StartupEntry(scratch.Root, "Run", "NoWayYahweh");
        const string exe = @"C:\Apps\No Way\NoWayYahweh.exe";

        Assert.False(entry.IsEnabled(exe));
        entry.Enable(exe);
        Assert.True(entry.IsEnabled(exe));
        using (var run = scratch.Root.OpenSubKey("Run"))
            Assert.Equal($"\"{exe}\" --background", run!.GetValue("NoWayYahweh"));
        Assert.False(entry.IsEnabled(@"C:\Other\NoWayYahweh.exe")); // moved app = not enabled
        entry.Disable();
        Assert.False(entry.IsEnabled(exe));
        entry.Disable(); // idempotent
    }
}

public class BrowserPoliciesTests
{
    [Fact]
    public void Apply_sets_every_private_window_policy_and_remove_clears_them()
    {
        using var scratch = new ScratchKey();
        Assert.False(BrowserPolicies.IsApplied(scratch.Root));
        BrowserPolicies.Apply(scratch.Root);
        Assert.True(BrowserPolicies.IsApplied(scratch.Root));
        using (var brave = scratch.Root.OpenSubKey(@"SOFTWARE\Policies\BraveSoftware\Brave"))
        {
            Assert.Equal(1, brave!.GetValue("IncognitoModeAvailability"));
            Assert.Equal(RegistryValueKind.DWord, brave.GetValueKind("IncognitoModeAvailability"));
            Assert.Equal(1, brave.GetValue("TorDisabled"));
        }
        BrowserPolicies.Remove(scratch.Root);
        Assert.False(BrowserPolicies.IsApplied(scratch.Root));
    }

    [Fact]
    public void Remove_leaves_other_policies_alone()
    {
        using var scratch = new ScratchKey();
        using (var chrome = scratch.Root.CreateSubKey(@"SOFTWARE\Policies\Google\Chrome"))
            chrome.SetValue("HomepageLocation", "https://example.com");
        BrowserPolicies.Apply(scratch.Root);
        BrowserPolicies.Remove(scratch.Root);
        using var after = scratch.Root.OpenSubKey(@"SOFTWARE\Policies\Google\Chrome");
        Assert.Equal("https://example.com", after!.GetValue("HomepageLocation"));
        Assert.Null(after.GetValue("IncognitoModeAvailability"));
    }

    [Fact]
    public void Partial_policy_is_not_reported_as_applied()
    {
        using var scratch = new ScratchKey();
        BrowserPolicies.Apply(scratch.Root);
        using (var edge = scratch.Root.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Edge", writable: true))
            edge!.DeleteValue("InPrivateModeAvailability");
        Assert.False(BrowserPolicies.IsApplied(scratch.Root));
    }
}

public class PasswordGuardTests
{
    [Fact]
    public void Verifies_only_the_right_password()
    {
        var hash = PasswordGuard.Hash("pai-nosso-123");
        Assert.True(PasswordGuard.Verify("pai-nosso-123", hash));
        Assert.False(PasswordGuard.Verify("pai-nosso-124", hash));
        Assert.False(PasswordGuard.Verify("", hash));
        Assert.DoesNotContain("pai-nosso", hash);
    }

    [Fact]
    public void Same_password_gets_a_different_salt_each_time() =>
        Assert.NotEqual(PasswordGuard.Hash("segredo1"), PasswordGuard.Hash("segredo1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("pbkdf2$x$AAAA$AAAA")]
    [InlineData("pbkdf2$1000$não-base64$AAAA")]
    public void Missing_or_malformed_hash_never_verifies(string? stored) => Assert.False(PasswordGuard.Verify("qualquer", stored));
}

public class PingAndIntegrityTests
{
    [Fact]
    public void Ping_answers_pong_without_touching_state()
    {
        using var dir = new TempDir();
        var handler = new MessageHandler(new StateStore(dir.File("state.json")), () => []);
        var reply = JsonSerializer.SerializeToElement(handler.Handle("""{"type":"ping"}"""u8));
        Assert.Equal("pong", reply.GetProperty("type").GetString());
        Assert.False(File.Exists(dir.File("state.json")));
        Assert.Equal("error", JsonSerializer.SerializeToElement(handler.Handle("""{"type":"ping","x":1}"""u8)).GetProperty("type").GetString());
    }

    [Fact]
    public void Integrity_check_reaches_the_built_host()
    {
        var host = Path.Combine(RepoRoot.Path, "artifacts", "bin", "Debug", HostRegistration.HostExeName);
        var results = IntegrityCheck.Run(host);
        Assert.Contains(results, r => r is { Name: "Serviço local", Ok: true });
        Assert.Contains(results, r => r is { Name: "Resposta do serviço", Ok: true });
        Assert.Contains(results, r => r.Name == "Registro no brave");
    }

    [Fact]
    public void Integrity_check_reports_a_missing_host()
    {
        var results = IntegrityCheck.Run(@"Z:\nao\existe\NoWayYahweh.Host.exe");
        Assert.Contains(results, r => r is { Name: "Serviço local", Ok: false });
        Assert.Contains(results, r => r is { Name: "Resposta do serviço", Ok: false });
    }
}

public class SignInModeTests
{
    [Fact]
    public void Background_mode_repairs_registration_and_exits_without_a_window()
    {
        var bin = Path.Combine(RepoRoot.Path, "artifacts", "bin", "Debug");
        var host = Path.Combine(bin, HostRegistration.HostExeName);
        using var dir = new TempDir();
        // The app writes the real per-user browser keys; put back whatever was there, or the
        // keys would point at this test's deleted manifest and break the real integration.
        var previous = HostRegistration.BrowserKeys.Values.ToDictionary(k => k, k =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(k);
            return key?.GetValue(null) as string;
        });
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(bin, "NoWayYahweh.exe"), StartupEntry.BackgroundArgument);
            psi.Environment[AppPaths.DataDirVariable] = dir.Path;
            using var p = Process.Start(psi)!;
            Assert.True(p.WaitForExit(15_000), "não terminou sozinho");
            Assert.Equal(0, p.ExitCode);
            Assert.Equal(IntPtr.Zero, p.MainWindowHandle);
            Assert.All(HostRegistration.BrowserKeys.Keys, b => Assert.True(HostRegistration.IsRegistered(b, host), b));
        }
        finally
        {
            foreach (var (path, value) in previous)
            {
                if (value is null) { Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false); continue; }
                using var key = Registry.CurrentUser.CreateSubKey(path);
                key.SetValue(null, value);
            }
        }
    }
}
