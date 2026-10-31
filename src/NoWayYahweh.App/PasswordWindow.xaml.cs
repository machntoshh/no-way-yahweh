using System.Windows;
using NoWayYahweh.Core;

namespace NoWayYahweh.App;

/// <summary>Set, change or remove the responsible-person password. Changing or removing needs the current one.</summary>
public partial class PasswordWindow : Window
{
    public enum Mode { Set, Change, Remove }

    private readonly StateStore _state;
    private readonly Mode _mode;

    public PasswordWindow(StateStore state, Mode mode)
    {
        _state = state;
        _mode = mode;
        InitializeComponent();
        CurrentPanel.Visibility = mode == Mode.Set ? Visibility.Collapsed : Visibility.Visible;
        NewPanel.Visibility = mode == Mode.Remove ? Visibility.Collapsed : Visibility.Visible;
        Ok.Content = mode switch { Mode.Set => "Definir", Mode.Change => "Alterar", _ => "Remover" };
        Intro.Text = mode == Mode.Remove
            ? "Sem a senha, pausar e desinstalar voltam a pedir só a reflexão e os desafios."
            : "Peça a alguém de confiança — cônjuge, mentor, amigo — para digitar e guardar esta senha. " +
              "Ela passa a ser pedida para pausar, liberar janelas privadas e desinstalar. " +
              "Se ela se perder, o guia de recuperação (docs/RECUPERACAO.md) continua valendo.";
        (mode == Mode.Set ? New : Current).Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_mode != Mode.Set && !PasswordGuard.Verify(Current.Password, _state.Read().GuardPasswordHash))
        {
            Feedback.Text = "Senha atual incorreta.";
            return;
        }
        if (_mode == Mode.Remove)
        {
            _state.Update(s => s.GuardPasswordHash = null);
            DialogResult = true;
            return;
        }
        if (New.Password.Length < PasswordGuard.MinLength)
        {
            Feedback.Text = $"Use pelo menos {PasswordGuard.MinLength} caracteres.";
            return;
        }
        if (New.Password != Repeat.Password)
        {
            Feedback.Text = "As senhas não conferem.";
            return;
        }
        var hash = PasswordGuard.Hash(New.Password);
        _state.Update(s => s.GuardPasswordHash = hash);
        DialogResult = true;
    }
}
