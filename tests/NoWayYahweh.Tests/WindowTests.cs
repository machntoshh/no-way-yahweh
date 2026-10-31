using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using NoWayYahweh.App;
using NoWayYahweh.Core;

namespace NoWayYahweh.Tests;

/// <summary>
/// Opens the real challenge window (with the app's resources) on an STA thread, so XAML or
/// resource mistakes fail here instead of when the user clicks "Pausar proteção…".
/// </summary>
public class WindowTests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); } catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new Exception("Falha na thread STA", error);
    }

    /// <summary>Clicks through UI automation, then lets the dispatcher run the queued click.</summary>
    private static void Click(Button button)
    {
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    [Fact]
    public void Challenge_window_opens_on_reflection_with_a_verse_and_a_forced_wait()
    {
        using var dir = new TempDir();
        OnSta(() =>
        {
            var app = Application.Current ?? new NoWayYahweh.App.App();
            if (app.Resources.Count == 0) ((NoWayYahweh.App.App)app).InitializeComponent();
            var store = new StateStore(dir.File("state.json"));
            var window = new ChallengeWindow(store);
            window.Show();
            try
            {
                var verse = (TextBlock)window.FindName("VerseText");
                var proceed = (Button)window.FindName("Proceed");
                var stay = (Button)window.FindName("StayProtected");
                Assert.False(string.IsNullOrWhiteSpace(verse.Text));
                Assert.False(proceed.IsEnabled); // the reflection wait
                Assert.Contains("20", proceed.Content.ToString());
                Assert.True(stay.IsEnabled);
                Assert.Null(store.Read().PausedUntil);
            }
            finally { window.Close(); }
            Assert.Null(store.Read().PausedUntil); // closing never pauses
        });
    }

    [Theory]
    [InlineData(ChallengeWindow.Purpose.Uninstall, "desinstalar")]
    [InlineData(ChallengeWindow.Purpose.RemovePrivateWindowPolicy, "janelas privadas")]
    public void Unprotect_windows_open_on_reflection(ChallengeWindow.Purpose purpose, string titleWord)
    {
        using var dir = new TempDir();
        OnSta(() =>
        {
            var app = Application.Current ?? new NoWayYahweh.App.App();
            if (app.Resources.Count == 0) ((NoWayYahweh.App.App)app).InitializeComponent();
            var window = new ChallengeWindow(new StateStore(dir.File("state.json")), purpose);
            window.Show();
            try
            {
                Assert.Contains(titleWord, ((TextBlock)window.FindName("StepTitle")).Text);
                Assert.False(((Button)window.FindName("Proceed")).IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Password_window_rejects_short_and_mismatched_passwords()
    {
        using var dir = new TempDir();
        OnSta(() =>
        {
            var app = Application.Current ?? new NoWayYahweh.App.App();
            if (app.Resources.Count == 0) ((NoWayYahweh.App.App)app).InitializeComponent();
            var store = new StateStore(dir.File("state.json"));
            var window = new PasswordWindow(store, PasswordWindow.Mode.Set);
            window.Show();
            try
            {
                var (newBox, repeat, ok, feedback) = ((PasswordBox)window.FindName("New"), (PasswordBox)window.FindName("Repeat"),
                    (Button)window.FindName("Ok"), (TextBlock)window.FindName("Feedback"));
                newBox.Password = "123"; repeat.Password = "123";
                Click(ok);
                Assert.Contains("6", feedback.Text);
                newBox.Password = "segredo-1"; repeat.Password = "segredo-2"; // privacy-scan: test value
                Click(ok);
                Assert.Contains("não conferem", feedback.Text);
                Assert.Null(store.Read().GuardPasswordHash);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Dashboard_opens()
    {
        using var dir = new TempDir();
        Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, dir.Path);
        try
        {
            OnSta(() =>
            {
                var app = Application.Current ?? new NoWayYahweh.App.App();
                if (app.Resources.Count == 0) ((NoWayYahweh.App.App)app).InitializeComponent();
                var window = new MainWindow();
                window.Show();
                Assert.Equal(Visibility.Visible, ((Button)window.FindName("PauseButton")).Visibility);
                window.Close();
            });
        }
        finally { Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, null); }
    }
}
