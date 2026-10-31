using Microsoft.Win32;

namespace NoWayYahweh.Core;

/// <summary>
/// Optional machine policy that turns off private/incognito windows (and Brave's Tor windows),
/// where the extension may not run. Written under HKLM, so applying and removing need admin —
/// a deliberate extra step. Only our own values are touched; other policies are left alone.
/// </summary>
public static class BrowserPolicies
{
    public const string ApplyArgument = "--apply-private-window-policy";
    public const string RemoveArgument = "--remove-private-window-policy";

    /// <summary>(policy key, value name, value). Values documented by Chromium/Brave/Edge: 1 = disabled.</summary>
    public static readonly IReadOnlyList<(string Key, string Name, int Value)> PrivateWindows =
    [
        (@"SOFTWARE\Policies\BraveSoftware\Brave", "IncognitoModeAvailability", 1),
        (@"SOFTWARE\Policies\BraveSoftware\Brave", "TorDisabled", 1),
        (@"SOFTWARE\Policies\Google\Chrome", "IncognitoModeAvailability", 1),
        (@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability", 1),
    ];

    /// <summary>Production root is HKLM; tests pass a scratch key.</summary>
    public static void Apply(RegistryKey root)
    {
        foreach (var (path, name, value) in PrivateWindows)
        {
            using var key = root.CreateSubKey(path);
            key.SetValue(name, value, RegistryValueKind.DWord);
        }
    }

    public static void Remove(RegistryKey root)
    {
        foreach (var (path, name, _) in PrivateWindows)
        {
            using var key = root.OpenSubKey(path, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }

    public static bool IsApplied(RegistryKey root) => PrivateWindows.All(p =>
    {
        using var key = root.OpenSubKey(p.Key);
        return key?.GetValue(p.Name) is int v && v == p.Value;
    });
}
