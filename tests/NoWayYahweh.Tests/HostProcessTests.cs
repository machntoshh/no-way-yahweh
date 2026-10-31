using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NoWayYahweh.Core;

namespace NoWayYahweh.Tests;

/// <summary>Runs the real host executable the way the browser does: origin argument + framed stdin/stdout.</summary>
public class HostProcessTests : IDisposable
{
    private static readonly string HostExe = Path.Combine(RepoRoot.Path, "artifacts", "bin",
#if DEBUG
        "Debug",
#else
        "Release",
#endif
        HostRegistration.HostExeName);

    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private Process Start(string origin)
    {
        var psi = new ProcessStartInfo(HostExe, [origin, "--parent-window=0"])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment[AppPaths.DataDirVariable] = _dir.Path;
        return Process.Start(psi)!;
    }

    private static void Send(Process p, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        p.StandardInput.BaseStream.Write(BitConverter.GetBytes(body.Length));
        p.StandardInput.BaseStream.Write(body);
        p.StandardInput.BaseStream.Flush();
    }

    private static JsonElement Receive(Process p) =>
        JsonDocument.Parse(NativeMessaging.Read(p.StandardOutput.BaseStream)!).RootElement;

    [Fact]
    public void Answers_hello_and_blocked_then_exits_cleanly()
    {
        File.WriteAllText(_dir.File("extra-domains.txt"), "meu-bloqueio.com\n");
        using var p = Start(HostRegistration.AllowedOrigins[0]);

        Send(p, """{"type":"hello","browser":"chrome","version":"0.1.0"}""");
        var state = Receive(p);
        Assert.Equal("state", state.GetProperty("type").GetString());
        Assert.Equal("meu-bloqueio.com", state.GetProperty("extraDomains")[0].GetString());

        Send(p, """{"type":"blocked","count":4}""");
        Assert.Equal("ack", Receive(p).GetProperty("type").GetString());

        p.StandardInput.Close();
        Assert.True(p.WaitForExit(5000));
        Assert.Equal(0, p.ExitCode);
        Assert.Equal(4, new StateStore(_dir.File("state.json")).Read().BlockCount);
    }

    [Fact]
    public void Refuses_unknown_origin()
    {
        using var p = Start("chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/");
        Assert.True(p.WaitForExit(5000));
        Assert.Equal(2, p.ExitCode);
        Assert.Equal(-1, p.StandardOutput.BaseStream.ReadByte()); // nothing written
    }

    [Fact]
    public void Stops_on_oversized_frame()
    {
        using var p = Start(HostRegistration.AllowedOrigins[0]);
        p.StandardInput.BaseStream.Write(BitConverter.GetBytes(NativeMessaging.MaxIncomingBytes + 1));
        p.StandardInput.BaseStream.Flush();
        Assert.Equal("error", Receive(p).GetProperty("type").GetString());
        Assert.True(p.WaitForExit(5000));
        Assert.Equal(3, p.ExitCode);
    }
}
