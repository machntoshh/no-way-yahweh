using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NoWayYahweh.Core;
using NoWayYahweh.Core.Challenges;

namespace NoWayYahweh.App;

/// <summary>
/// The pause before a decision: reflection with a verse (with a forced wait), then — for a
/// pause — a choice of duration, then reasoning challenges and, if one is set, the
/// responsible-person password. "Continuar protegido" is always the easiest button; closing
/// the window at any point changes nothing.
/// </summary>
public partial class ChallengeWindow : Window
{
    /// <summary>What passing leads to. Only <see cref="Pause"/> acts here; the others return true and the caller acts.</summary>
    public enum Purpose { Pause, RemovePrivateWindowPolicy, Uninstall }

    /// <summary>Removing a protection or uninstalling costs as much as the longest pause.</summary>
    public const int UnprotectChallenges = 3;

    private enum Step { Reflection, Duration, Challenge, Password, Done }

    private readonly StateStore _state;
    private readonly Purpose _purpose;
    private readonly ChallengeGenerator _generator = new(Random.Shared);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<ChallengeKind> _usedThisSession = [];
    private PausePolicy.Option? _option;
    private Challenge? _challenge;
    private int _solved;
    private int _secondsLeft;

    private int RequiredChallenges => _purpose == Purpose.Pause ? _option!.Challenges : UnprotectChallenges;

    public ChallengeWindow(StateStore state, Purpose purpose = Purpose.Pause)
    {
        _state = state;
        _purpose = purpose;
        InitializeComponent();
        var verse = Verses.Random();
        VerseText.Text = verse.Text;
        VerseRef.Text = $"{verse.Reference} — Bíblia Livre (BLIVRE)";
        ReflectionText.Text = verse.Reflection;
        DurationOptions.ItemsSource = PausePolicy.Options;
        _timer.Tick += (_, _) => Tick();
        Show(Step.Reflection);
    }

    private void Show(Step step)
    {
        ReflectionStep.Visibility = step == Step.Reflection ? Visibility.Visible : Visibility.Collapsed;
        DurationStep.Visibility = step == Step.Duration ? Visibility.Visible : Visibility.Collapsed;
        ChallengeStep.Visibility = step == Step.Challenge ? Visibility.Visible : Visibility.Collapsed;
        PasswordStep.Visibility = step == Step.Password ? Visibility.Visible : Visibility.Collapsed;
        DoneStep.Visibility = step == Step.Done ? Visibility.Visible : Visibility.Collapsed;
        StayProtected.Visibility = step == Step.Done ? Visibility.Collapsed : Visibility.Visible;
        Proceed.Visibility = step is Step.Reflection or Step.Done ? Visibility.Visible : Visibility.Collapsed;

        switch (step)
        {
            case Step.Reflection:
                StepTitle.Text = _purpose switch
                {
                    Purpose.Pause => "Antes de pausar, uma pausa.",
                    Purpose.RemovePrivateWindowPolicy => "Antes de liberar as janelas privadas, uma pausa.",
                    _ => "Antes de desinstalar, uma pausa.",
                };
                _secondsLeft = PausePolicy.ReflectionSeconds;
                Proceed.IsEnabled = false;
                UpdateProceedCountdown();
                _timer.Start();
                StayProtected.Focus();
                break;
            case Step.Duration:
                StepTitle.Text = "Por quanto tempo?";
                break;
            case Step.Challenge:
                StepTitle.Text = "Um desafio para o raciocínio";
                NextChallenge();
                break;
            case Step.Password:
                StepTitle.Text = "Senha de responsável";
                Password.Focus();
                break;
            case Step.Done:
                StepTitle.Text = "Pausa concedida.";
                Proceed.Content = "Fechar";
                Proceed.IsEnabled = true;
                break;
        }
    }

    private void Tick()
    {
        _secondsLeft--;
        if (ReflectionStep.Visibility == Visibility.Visible)
        {
            UpdateProceedCountdown();
            if (_secondsLeft <= 0) { _timer.Stop(); Proceed.IsEnabled = true; }
        }
        else if (_challenge?.ShowSeconds is not null)
        {
            MemoryCountdown.Text = $"Some em {_secondsLeft} s…";
            if (_secondsLeft <= 0) { _timer.Stop(); HideMemoryItems(); }
        }
    }

    private void UpdateProceedCountdown()
    {
        var label = _purpose == Purpose.Pause ? "Ainda quero pausar" : "Ainda quero continuar";
        Proceed.Content = _secondsLeft > 0 ? $"{label} ({_secondsLeft})" : label;
    }

    private void Proceed_Click(object sender, RoutedEventArgs e)
    {
        if (DoneStep.Visibility == Visibility.Visible) { DialogResult = true; return; }
        Show(_purpose == Purpose.Pause ? Step.Duration : Step.Challenge);
    }

    private void StayProtected_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Duration_Click(object sender, RoutedEventArgs e)
    {
        _option = (PausePolicy.Option)((Button)sender).Tag;
        Show(Step.Challenge);
    }

    private void NextChallenge()
    {
        var avoid = _state.Read().RecentChallengeKinds.Concat(_usedThisSession).ToHashSet();
        _challenge = _generator.Next(avoid);
        _usedThisSession.Add(_challenge.Kind);
        // Remembered right away, so closing and reopening does not fish for an easier kind.
        _state.Update(s => PausePolicy.Remember(s, _challenge.Kind));

        ChallengeProgress.Text = $"Desafio {_solved + 1} de {RequiredChallenges}";
        ChallengeTitle.Text = _challenge.Title;
        ChallengePrompt.Text = _challenge.Prompt;
        Answer.Text = "";
        Answer.IsReadOnly = _challenge.Kind == ChallengeKind.Ordering;
        Answer.IsEnabled = true;
        Confirm.IsEnabled = true;
        RestartOrdering.Visibility = _challenge.Kind == ChallengeKind.Ordering ? Visibility.Visible : Visibility.Collapsed;
        MemoryCountdown.Visibility = Visibility.Collapsed;
        Items.Children.Clear();

        if (_challenge.Kind == ChallengeKind.Ordering)
        {
            foreach (var item in _challenge.Items!)
            {
                var tile = new Button { Content = item, Margin = new Thickness(0, 0, 8, 8), MinWidth = 64, FontSize = 16 };
                tile.Click += (_, _) =>
                {
                    Answer.Text = (Answer.Text + " " + item).Trim();
                    tile.IsEnabled = false;
                };
                Items.Children.Add(tile);
            }
        }
        else if (_challenge.ShowSeconds is int seconds)
        {
            foreach (var item in _challenge.Items!)
                Items.Children.Add(new TextBlock { Text = item, FontSize = 30, Margin = new Thickness(0, 0, 14, 0), Foreground = (System.Windows.Media.Brush)FindResource("Text") });
            Answer.IsEnabled = false;
            Confirm.IsEnabled = false;
            _secondsLeft = seconds;
            MemoryCountdown.Text = $"Some em {seconds} s…";
            MemoryCountdown.Visibility = Visibility.Visible;
            _timer.Start();
        }
        else Answer.Focus();
    }

    private void HideMemoryItems()
    {
        Items.Children.Clear();
        MemoryCountdown.Text = "Agora digite a sequência.";
        Answer.IsEnabled = true;
        Confirm.IsEnabled = true;
        Answer.Focus();
    }

    private void RestartOrdering_Click(object sender, RoutedEventArgs e)
    {
        Answer.Text = "";
        foreach (var tile in Items.Children.OfType<Button>()) tile.IsEnabled = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_challenge is null || !Confirm.IsEnabled || string.IsNullOrWhiteSpace(Answer.Text)) return;
        if (!_challenge.Check(Answer.Text))
        {
            Feedback.Text = "Não foi dessa vez. Aqui vai outro — sem pressa.";
            NextChallenge();
            return;
        }

        Feedback.Text = "";
        if (++_solved < RequiredChallenges) { NextChallenge(); return; }
        if (_state.Read().GuardPasswordHash is not null) Show(Step.Password);
        else Finish();
    }

    private void Password_Click(object sender, RoutedEventArgs e)
    {
        if (!PasswordGuard.Verify(Password.Password, _state.Read().GuardPasswordHash))
        {
            PasswordFeedback.Text = "Senha incorreta.";
            Password.Clear();
            return;
        }
        Finish();
    }

    private void Finish()
    {
        if (_purpose != Purpose.Pause) { DialogResult = true; return; }

        var state = _state.Update(s => PausePolicy.Pause(s, _option!, DateTimeOffset.UtcNow));
        DoneText.Text =
            $"As proteções secundárias ficam pausadas até {state.PausedUntil!.Value.ToLocalTime():HH:mm} e voltam sozinhas. " +
            "Os sites pornográficos conhecidos continuam bloqueados. O navegador aplica a pausa em até 1 minuto.\n\n" +
            "Se mudar de ideia, use \"Reativar agora\" no painel.";
        Show(Step.Done);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
