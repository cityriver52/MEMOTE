using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;

namespace memoNOW;

public partial class MainWindow : Window
{
    private const int MaxMemoCount = 10;
    private const int HotkeyId = 0x4D4E;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly MemoStore _store = new();
    private readonly SettingsStore _settingsStore = new();
    private Forms.NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private HotkeySettings _hotkeySettings;
    private bool _hotkeyRegistered;
    private bool _exitRequested;
    private string? _statusOverride;

    public ObservableCollection<MemoItem> Memos { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _hotkeySettings = _settingsStore.LoadHotkey();

        foreach (var memo in _store.Load())
        {
            Memos.Add(memo);
        }

        CreateTrayIcon();
        UpdateStatus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        if (!TryRegisterHotkey(_hotkeySettings))
        {
            _statusOverride = $"{_hotkeySettings.DisplayText} を登録できませんでした。ショートカット設定から変更してください。";
            UpdateStatus();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusInput();
    }

    private void MemoInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None && Memos.Count > 0)
        {
            e.Handled = true;
            FocusMemoCompletionButton(0);
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        e.Handled = true;
        AddMemo();
    }

    private void AddMemo()
    {
        var text = MemoInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (Memos.Count >= MaxMemoCount)
        {
            _statusOverride = "10件いっぱいです。どれかを終わらせてから追加してください。";
            UpdateStatus();
            return;
        }

        Memos.Insert(0, new MemoItem
        {
            Text = text,
            CreatedAt = DateTimeOffset.Now
        });

        MemoInput.Clear();
        _statusOverride = null;
        SaveMemos();
        UpdateStatus();
    }

    private void CompleteMemo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not Guid id)
        {
            return;
        }

        CompleteMemoById(id, keepKeyboardNavigation: false);
    }

    private void CompleteMemoById(Guid id, bool keepKeyboardNavigation)
    {
        var index = Memos.ToList().FindIndex(memo => memo.Id == id);
        if (index < 0)
        {
            return;
        }

        Memos.RemoveAt(index);
        _statusOverride = null;
        SaveMemos();
        UpdateStatus();

        if (!keepKeyboardNavigation || Memos.Count == 0)
        {
            FocusInput();
            return;
        }

        FocusMemoCompletionButton(Math.Min(index, Memos.Count - 1));
    }

    private void ShortcutSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenHotkeySettings();
    }

    private void OpenHotkeySettings()
    {
        var previous = _hotkeySettings.Clone();
        UnregisterCurrentHotkey();

        var dialog = new HotkeySettingsWindow(previous)
        {
            Owner = this
        };

        var result = dialog.ShowDialog();
        if (result != true)
        {
            _hotkeySettings = previous;
            RestorePreviousHotkey(previous);
            return;
        }

        var candidate = dialog.SelectedSettings;
        if (!TryRegisterHotkey(candidate))
        {
            _hotkeySettings = previous;
            RestorePreviousHotkey(previous);
            _statusOverride = $"{candidate.DisplayText} は登録できません。別の組み合わせを試してください。";
            UpdateStatus();

            System.Windows.MessageBox.Show(
                this,
                $"{candidate.DisplayText} はWindowsまたは別のアプリが使用しているため登録できませんでした。\n\n元のショートカットに戻しました。",
                "ショートカットの競合",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _hotkeySettings = candidate;
        _statusOverride = null;

        try
        {
            _settingsStore.SaveHotkey(_hotkeySettings);
        }
        catch (IOException)
        {
            _statusOverride = "ショートカットは変更されましたが、設定を保存できませんでした。";
        }
        catch (UnauthorizedAccessException)
        {
            _statusOverride = "ショートカットは変更されましたが、設定保存先へのアクセス権がありません。";
        }

        UpdateStatus();
        FocusInput();
    }

    private void RestorePreviousHotkey(HotkeySettings previous)
    {
        if (!TryRegisterHotkey(previous))
        {
            _statusOverride = $"{previous.DisplayText} を登録できません。ショートカット設定から別の組み合わせを指定してください。";
        }
        else
        {
            _statusOverride = null;
        }

        UpdateStatus();
    }

    private bool TryRegisterHotkey(HotkeySettings settings)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !settings.TryGetKey(out var key) || !settings.IsValid(out _))
        {
            _hotkeyRegistered = false;
            return false;
        }

        uint modifiers = ModNoRepeat;
        if (settings.Ctrl) modifiers |= ModControl;
        if (settings.Alt) modifiers |= ModAlt;
        if (settings.Shift) modifiers |= ModShift;
        if (settings.Win) modifiers |= ModWin;

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        _hotkeyRegistered = RegisterHotKey(handle, HotkeyId, modifiers, virtualKey);
        return _hotkeyRegistered;
    }

    private void UnregisterCurrentHotkey()
    {
        if (!_hotkeyRegistered)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotkeyId);
        }

        _hotkeyRegistered = false;
    }

    private void SaveMemos()
    {
        try
        {
            _store.Save(Memos);
        }
        catch (IOException)
        {
            _statusOverride = "メモを保存できませんでした。";
        }
        catch (UnauthorizedAccessException)
        {
            _statusOverride = "メモ保存先へのアクセス権がありません。";
        }
    }

    private void UpdateStatus()
    {
        var normalStatus = $"{Memos.Count} / {MaxMemoCount} 件 · {_hotkeySettings.DisplayText} で表示/非表示 · ↓で選択 / Deleteで完了";
        StatusText.Text = _statusOverride ?? normalStatus;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
            return;
        }

        if (Keyboard.FocusedElement is not WpfButton button || button.Tag is not Guid id)
        {
            return;
        }

        var index = Memos.ToList().FindIndex(memo => memo.Id == id);
        if (index < 0)
        {
            return;
        }

        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            CompleteMemoById(id, keepKeyboardNavigation: true);
            return;
        }

        if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (index == 0)
            {
                FocusInput();
            }
            else
            {
                FocusMemoCompletionButton(index - 1);
            }

            return;
        }

        if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None && index < Memos.Count - 1)
        {
            e.Handled = true;
            FocusMemoCompletionButton(index + 1);
        }
    }

    private void FocusMemoCompletionButton(int index)
    {
        if (index < 0 || index >= Memos.Count)
        {
            FocusInput();
            return;
        }

        var id = Memos[index].Id;
        var button = FindCompletionButton(this, id);
        if (button is null)
        {
            return;
        }

        button.Focus();
        Keyboard.Focus(button);
    }

    private static WpfButton? FindCompletionButton(DependencyObject root, Guid id)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is WpfButton button && button.Tag is Guid buttonId && buttonId == id)
            {
                return button;
            }

            var nested = FindCompletionButton(child, id);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        Hide();
        WindowState = WindowState.Normal;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        CleanupNativeResources();
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "memoNOW",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => Dispatcher.Invoke(ShowAndFocus));
        menu.Items.Add("Shortcut settings...", null, (_, _) => Dispatcher.Invoke(() =>
        {
            ShowAndFocus();
            OpenHotkeySettings();
        }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowAndFocus);
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ShowAndFocus();
    }

    private void ShowAndFocus()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();

        // Briefly toggling Topmost helps recover focus when invoked over another app.
        Topmost = true;
        Topmost = false;

        FocusInput();
    }

    private void FocusInput()
    {
        MemoInput.Focus();
        Keyboard.Focus(MemoInput);
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void CleanupNativeResources()
    {
        UnregisterCurrentHotkey();

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleVisibility();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
