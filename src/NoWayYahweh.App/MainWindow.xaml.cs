using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NoWayYahweh.Core;
using NoWayYahweh.Core.Challenges;

namespace NoWayYahweh.App;

public partial class MainWindow : Window
{
    private static string HostExe => App.HostExe;
    private static readonly Dictionary<string, string> BrowserNames = new() { ["chrome"] = "Google Chrome", ["edge"] = "Microsoft Edge", ["brave"] = "Brave" };

    /// <summary>The extension checks in every minute while its browser is open.</summary>
    private static readonly TimeSpan RecentContact = TimeSpan.FromHours(1);

    private readonly StateStore _state = StateStore.Default;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    /// <summary>First-run setup done by App when the dashboard opens (not when tests build the window).</summary>
    public void SetUp()
    {
        Register(quiet: true);
        try
        {
            // Verification at sign-in is on by default, once; afterwards the checkbox decides.
            _state.Update(s =>
            {
                if (s.StartupDefaultApplied) return;
                StartupEntry.Default.Enable(App.AppExe);
                s.StartupDefaultApplied = true;
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or TimeoutException)
        {
            Message.Text = "Não foi possível ativar a verificação ao iniciar: " + e.Message;
        }
        Refresh();
    }

    private void Register(bool quiet)
    {
        try
        {
            HostRegistration.Register(HostExe);
            if (!quiet) Message.Text = "Integração registrada para Chrome, Edge e Brave. Reinicie o navegador se ele já estava aberto.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Message.Text = "Não foi possível registrar a integração: " + e.Message;
        }
    }

    private void Refresh()
    {
        ProtectionState state;
        try
        {
            // The first launch writes the file, which records when protection started.
            state = File.Exists(AppPaths.StateFile) ? _state.Read() : _state.Update(_ => { });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException)
        {
            SetStatus(false, "Estado indisponível", e.Message);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        BlockCount.Text = state.BlockCount.ToString("N0");
        ProtectedFor.Text = FormatDuration(now - state.ProtectedSince);

        var registered = BrowserNames.Keys.All(b => HostRegistration.IsRegistered(b, HostExe));
        Browsers.ItemsSource = BrowserNames.Select(b => state.BrowsersLastSeen.TryGetValue(b.Key, out var seen)
            ? $"{b.Value} — protegido · último contato há {FormatDuration(now - seen)}"
            : $"{b.Value} — extensão ainda não conectada").ToList();

        var paused = PausePolicy.IsPaused(state, now);
        PauseButton.Visibility = paused ? Visibility.Collapsed : Visibility.Visible;
        ResumeButton.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
        RefreshSettings(state);

        if (!registered)
            SetStatus(false, "Integração incompleta", "O registro do serviço local nos navegadores está ausente. Use \"Reparar integração\".");
        else if (paused)
            SetStatus(false, $"Pausada até {state.PausedUntil!.Value.ToLocalTime():HH:mm}",
                "Palavras no endereço e domínios extras estão liberados. Os sites pornográficos conhecidos continuam bloqueados.");
        else if (state.BrowsersLastSeen.Count == 0)
            SetStatus(false, "Instale a extensão", "Nenhum navegador conectou ainda. Instale a extensão No way? Yahweh! no Brave, Chrome ou Edge (veja o README).");
        else if (state.BrowsersLastSeen.Values.Any(seen => now - seen < RecentContact))
            SetStatus(true, "Proteção ativa", "O bloqueio de sites funciona dentro do navegador, mesmo offline.");
        else
            SetStatus(true, "Proteção instalada", "Sem contato recente — normal se o navegador está fechado.");
    }

    private void SetStatus(bool ok, string text, string detail)
    {
        StatusDot.Fill = (Brush)FindResource(ok ? "Accent" : "Warn");
        StatusText.Text = text;
        StatusDetail.Text = detail;
    }

    private static string FormatDuration(TimeSpan t) => t switch
    {
        { TotalMinutes: < 1 } => "menos de 1 min",
        { TotalHours: < 1 } => $"{(int)t.TotalMinutes} min",
        { TotalDays: < 1 } => $"{(int)t.TotalHours} h {t.Minutes} min",
        _ => $"{(int)t.TotalDays} d {t.Hours} h",
    };

    // --- settings and security (Fase 4)

    private void RefreshSettings(ProtectionState state)
    {
        _refreshing = true;
        StartupCheck.IsChecked = StartupEntry.Default.IsEnabled(App.AppExe);
        _refreshing = false;

        var policy = BrowserPolicies.IsApplied(Microsoft.Win32.Registry.LocalMachine);
        PrivateStatus.Text = policy
            ? "Janelas privadas/anônimas (e Tor no Brave): bloqueadas por política do Windows."
            : "Janelas privadas/anônimas: permitidas. Bloqueá-las fecha um atalho comum (pede administrador).";
        BlockPrivateButton.Visibility = policy ? Visibility.Collapsed : Visibility.Visible;
        UnblockPrivateButton.Visibility = policy ? Visibility.Visible : Visibility.Collapsed;

        var hasPassword = state.GuardPasswordHash is not null;
        PasswordStatus.Text = hasPassword
            ? "Senha de responsável: definida — pedida para pausar, liberar janelas privadas e desinstalar."
            : "Senha de responsável: não definida (opcional).";
        SetPasswordButton.Visibility = hasPassword ? Visibility.Collapsed : Visibility.Visible;
        ChangePasswordButton.Visibility = RemovePasswordButton.Visibility = hasPassword ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshing) return;
        if (StartupCheck.IsChecked == true) StartupEntry.Default.Enable(App.AppExe);
        else StartupEntry.Default.Disable();
    }

    private static bool RunElevated(string argument) => Uninstaller.RunElevated(argument);

    private void BlockPrivate_Click(object sender, RoutedEventArgs e)
    {
        Message.Text = RunElevated(BrowserPolicies.ApplyArgument)
            ? "Janelas privadas bloqueadas. Reinicie o navegador para aplicar."
            : "Não foi aplicado (permissão de administrador negada).";
        Refresh();
    }

    private void UnblockPrivate_Click(object sender, RoutedEventArgs e)
    {
        if (new ChallengeWindow(_state, ChallengeWindow.Purpose.RemovePrivateWindowPolicy) { Owner = this }.ShowDialog() != true) return;
        Message.Text = RunElevated(BrowserPolicies.RemoveArgument)
            ? "Janelas privadas liberadas. Reinicie o navegador para aplicar."
            : "Não foi removido (permissão de administrador negada).";
        Refresh();
    }

    private void SetPassword_Click(object sender, RoutedEventArgs e) => EditPassword(PasswordWindow.Mode.Set);
    private void ChangePassword_Click(object sender, RoutedEventArgs e) => EditPassword(PasswordWindow.Mode.Change);
    private void RemovePassword_Click(object sender, RoutedEventArgs e) => EditPassword(PasswordWindow.Mode.Remove);

    private void EditPassword(PasswordWindow.Mode mode)
    {
        if (new PasswordWindow(_state, mode) { Owner = this }.ShowDialog() == true)
            Message.Text = mode == PasswordWindow.Mode.Remove ? "Senha removida." : "Senha salva.";
        Refresh();
    }

    private void Integrity_Click(object sender, RoutedEventArgs e) =>
        IntegrityResults.ItemsSource = IntegrityCheck.Run(HostExe).Select(r => $"{(r.Ok ? "✓" : "✗")}  {r.Name}: {r.Detail}").ToList();

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        // Installed: the Windows uninstaller runs the same gate (reflection + challenges) and removes the files.
        if (Uninstaller.WindowsUninstaller is { } uninstaller)
        {
            Process.Start(new ProcessStartInfo(uninstaller) { UseShellExecute = true });
            Close();
            return;
        }

        // Development copy (no installer): gate here, then clean up what was set outside the folder.
        if (new ChallengeWindow(_state, ChallengeWindow.Purpose.Uninstall) { Owner = this }.ShowDialog() != true) return;
        _timer.Stop();
        var notes = Uninstaller.Cleanup(RunElevated);
        MessageBox.Show(this,
            "O app foi desligado deste computador.\n\nFalta: remover a extensão em brave://extensions (ou chrome:// / edge://) " +
            "e apagar a pasta do programa.\n" + string.Join("\n", notes),
            "Desinstalado", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        new ChallengeWindow(_state) { Owner = this }.ShowDialog();
        Refresh();
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        _state.Update(PausePolicy.Resume);
        Message.Text = "Proteção completa reativada. O navegador aplica em até 1 minuto.";
        Refresh();
    }

    private void Repair_Click(object sender, RoutedEventArgs e)
    {
        Register(quiet: false);
        Refresh();
    }

    private void ExtraDomains_Click(object sender, RoutedEventArgs e)
    {
        var file = AppPaths.ExtraDomainsFile;
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(file, "# Um domínio por linha (ex.: exemplo.com). Subdomínios são bloqueados juntos.\n# O navegador aplica a lista em até 1 minuto.\n");
        }
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{file}\"") { UseShellExecute = false });
        Message.Text = "Depois de salvar, a lista é aplicada em até 1 minuto.";
    }
}
