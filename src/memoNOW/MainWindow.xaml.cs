using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace memoNOW;

public partial class MainWindow : Window
{
    private const int MaxMemoCount = 10;
    private const int HotkeyId = 0x4D4E;
    private const int WmHotkey = 0x0312;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly MemoStore _store = new();
    private Forms.NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;
    private bool _exitRequested;
    private string? _statusOverride;

    public ObservableCollection<MemoItem> Memos { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

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

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.Space);
        _hotkeyRegistered = RegisterHotKey(handle, HotkeyId, ModWin | ModShift, virtualKey);

        if (!_hotkeyRegistered)
        {
            _statusOverride = "Win + Shift + Space を登録できませんでした。別アプリと競合している可能性があります。";
            UpdateStatus();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusInput();
    }

    private void MemoInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
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
        if (sender is not System.Windows.Controls.Button button || button.Tag is not Guid id)
        {
            return;
        }

        var item = Memos.FirstOrDefault(memo => memo.Id == id);
        if (item is null)
        {
            return;
        }

        Memos.Remove(item);
        _statusOverride = null;
        SaveMemos();
        UpdateStatus();
        FocusInput();
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
        StatusText.Text = _statusOverride ?? $"{Memos.Count} / {MaxMemoCount} 件 · Win + Shift + Space ですぐ入力 · Escで隠す";
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Hide();
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
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowAndFocus);
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
        var handle = new WindowInteropHelper(this).Handle;

        if (_hotkeyRegistered && handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotkeyId);
            _hotkeyRegistered = false;
        }

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
            ShowAndFocus();
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
