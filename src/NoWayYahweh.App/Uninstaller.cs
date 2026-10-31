using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using NoWayYahweh.Core;

namespace NoWayYahweh.App;

/// <summary>
/// Removes what the app set up outside its own folder. The installed app's files are removed by
/// the Windows uninstaller (Inno Setup), which first runs this app with <see cref="GateArgument"/>:
/// reflection + challenges, and the uninstall only proceeds if they are completed.
/// </summary>
public static class Uninstaller
{
    public const string GateArgument = "--uninstall-gate";

    /// <summary>Inno Setup's uninstaller next to the app, when installed.</summary>
    public static string? WindowsUninstaller =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "unins*.exe").OrderDescending().FirstOrDefault();

    /// <summary>Returns notes about anything that could not be removed.</summary>
    public static List<string> Cleanup(Func<string, bool> runElevated)
    {
        var notes = new List<string>();
        if (BrowserPolicies.IsApplied(Registry.LocalMachine) && !runElevated(BrowserPolicies.RemoveArgument))
            notes.Add("A política de janelas privadas continua (administrador negado) — veja docs/RECUPERACAO.md.");
        StartupEntry.Default.Disable();
        HostRegistration.Unregister();
        try { if (Directory.Exists(AppPaths.DataDir)) Directory.Delete(AppPaths.DataDir, recursive: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { notes.Add("Não foi possível apagar " + AppPaths.DataDir); }
        return notes;
    }

    /// <summary>Runs this app elevated (UAC) for one HKLM change; false if the user declined.</summary>
    public static bool RunElevated(string argument)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(App.AppExe, argument) { UseShellExecute = true, Verb = "runas" })!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // UAC prompt cancelled
        }
    }
}
