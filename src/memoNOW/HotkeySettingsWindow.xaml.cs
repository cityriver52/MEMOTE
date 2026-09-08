using System.Windows;
using System.Windows.Input;

namespace memoNOW;

public partial class HotkeySettingsWindow : Window
{
    private HotkeySettings _candidate;

    public HotkeySettings SelectedSettings => _candidate.Clone();

    public HotkeySettingsWindow(HotkeySettings current)
    {
        InitializeComponent();
        _candidate = current.Clone();
        UpdateCaptureButton();
    }

    private void HotkeyCaptureButton_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        HintText.Text = "新しいショートカットを押してください…";
    }

    private void HotkeyCaptureButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var candidate = HotkeySettings.FromInput(key, Keyboard.Modifiers);
        if (!candidate.IsValid(out var message))
        {
            HintText.Text = message;
            e.Handled = true;
            return;
        }

        _candidate = candidate;
        UpdateCaptureButton();
        HintText.Text = "この組み合わせでよければ「保存」を押してください。";
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_candidate.IsValid(out var message))
        {
            HintText.Text = message;
            return;
        }

        DialogResult = true;
        Close();
    }

    private void UpdateCaptureButton()
    {
        HotkeyCaptureButton.Content = _candidate.DisplayText;
    }
}
