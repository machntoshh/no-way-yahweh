using System.Text.Json;
using Microsoft.Win32;

namespace NoWayYahweh.Core;

/// <summary>Registers the Native Messaging host for Chrome, Edge and Brave (per user, no admin needed).</summary>
public static class HostRegistration
{
    public const string HostName = "com.nowayyahweh.host";
    public const string HostExeName = "NoWayYahweh.Host.exe";

    /// <summary>
    /// Extensions allowed to talk to the host. The development ID comes from the public key in
    /// extension/manifest.json; store IDs are added when the extension is published (Fase 5).
    /// </summary>
    public static readonly string[] AllowedOrigins =
    [
        "chrome-extension://olipaknkbpadbdcdaaehfnpolcdeieee/",
    ];

    public static readonly IReadOnlyDictionary<string, string> BrowserKeys = new Dictionary<string, string>
    {
        ["chrome"] = @"Software\Google\Chrome\NativeMessagingHosts\" + HostName,
        ["edge"] = @"Software\Microsoft\Edge\NativeMessagingHosts\" + HostName,
        ["brave"] = @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\" + HostName,
    };

    public static string ManifestJson(string hostExePath) => JsonSerializer.Serialize(new
    {
        name = HostName,
        description = "No way? Yahweh! — serviço local",
        path = hostExePath,
        type = "stdio",
        allowed_origins = AllowedOrigins,
    }, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Writes the host manifest into the data folder and points both browsers at it.</summary>
    public static void Register(string hostExePath)
    {
        if (!File.Exists(hostExePath)) throw new FileNotFoundException("Host nativo não encontrado.", hostExePath);
        Directory.CreateDirectory(AppPaths.DataDir);
        var manifest = Path.Combine(AppPaths.DataDir, HostName + ".json");
        File.WriteAllText(manifest, ManifestJson(hostExePath));
        foreach (var key in BrowserKeys.Values)
        {
            using var k = Registry.CurrentUser.CreateSubKey(key);
            k.SetValue(null, manifest);
        }
    }

    public static void Unregister()
    {
        foreach (var key in BrowserKeys.Values) Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        File.Delete(Path.Combine(AppPaths.DataDir, HostName + ".json"));
    }

    /// <summary>True when the browser's registry entry points to a manifest naming this exact host executable.</summary>
    public static bool IsRegistered(string browser, string hostExePath)
    {
        using var k = Registry.CurrentUser.OpenSubKey(BrowserKeys[browser]);
        if (k?.GetValue(null) is not string manifest || !File.Exists(manifest)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            return string.Equals(doc.RootElement.GetProperty("path").GetString(), hostExePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }
}
