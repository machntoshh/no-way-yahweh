using Microsoft.Win32;

namespace NoWayYahweh.Core;

/// <summary>
/// Per-user "run at sign-in" entry (HKCU Run key, no admin). At sign-in the app runs with
/// <see cref="BackgroundArgument"/>: it repairs the browser registration and exits — no window.
/// </summary>
public sealed class StartupEntry(RegistryKey root, string keyPath, string valueName)
{
    public const string BackgroundArgument = "--background";

    public static StartupEntry Default => new(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "NoWayYahweh");

    public static string CommandFor(string appExePath) => $"\"{appExePath}\" {BackgroundArgument}";

    public bool IsEnabled(string appExePath)
    {
        using var key = root.OpenSubKey(keyPath);
        return string.Equals(key?.GetValue(valueName) as string, CommandFor(appExePath), StringComparison.OrdinalIgnoreCase);
    }

    public void Enable(string appExePath)
    {
        using var key = root.CreateSubKey(keyPath);
        key.SetValue(valueName, CommandFor(appExePath));
    }

    /// <summary>After an install or upgrade moved the app: keep an existing entry, pointing at the new path.</summary>
    public void Repoint(string appExePath)
    {
        using var key = root.OpenSubKey(keyPath);
        if (key?.GetValue(valueName) is string) Enable(appExePath);
    }

    public void Disable()
    {
        using var key = root.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
