using System.IO;
using System.Windows;
using Microsoft.Win32;
using NoWayYahweh.Core;

namespace NoWayYahweh.App;

/// <summary>
/// Modes:
///   (none)                              dashboard
///   --background                        at sign-in / after install: repair browser registration, exit, no window
///   --uninstall-gate                    run by the Windows uninstaller: reflection + challenges, then cleanup;
///                                       exit code 0 lets the uninstall proceed, anything else cancels it
///   --apply-private-window-policy       elevated (UAC): write the HKLM policy and exit
///   --remove-private-window-policy      elevated (UAC): remove it and exit
/// </summary>
public partial class App : Application
{
    public static string AppExe => Environment.ProcessPath!;
    public static string HostExe => Path.Combine(AppContext.BaseDirectory, HostRegistration.HostExeName);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        switch (e.Args.FirstOrDefault())
        {
            case StartupEntry.BackgroundArgument:
                Shutdown(Try(() =>
                {
                    HostRegistration.Register(HostExe);
                    StartupEntry.Default.Repoint(AppExe); // an install or upgrade may have moved the app
                }));
                return;
            case Uninstaller.GateArgument:
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var gate = new ChallengeWindow(StateStore.Default, ChallengeWindow.Purpose.Uninstall);
                if (gate.ShowDialog() != true) { Shutdown(1); return; }
                var notes = Uninstaller.Cleanup(Uninstaller.RunElevated);
                if (notes.Count > 0) MessageBox.Show(string.Join("\n", notes), "No way? Yahweh!", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(0);
                return;
            case BrowserPolicies.ApplyArgument:
                Shutdown(Try(() => BrowserPolicies.Apply(Registry.LocalMachine)));
                return;
            case BrowserPolicies.RemoveArgument:
                Shutdown(Try(() => BrowserPolicies.Remove(Registry.LocalMachine)));
                return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        window.SetUp();
    }

    private static int Try(Action action)
    {
        try { action(); return 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return 1; }
    }
}
