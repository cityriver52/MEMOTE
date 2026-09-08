using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
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
    private readonly Random _random = new();
    private readonly HashSet<Guid> _pendingSpawnIds = new();
    private readonly HashSet<Guid> _removingMemoIds = new();
    private readonly HashSet<FrameworkElement> _floatingBubbleHosts = new();

    private Forms.NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private HotkeySettings _hotkeySettings;
    private bool _hotkeyRegistered;
    private bool _exitRequested;
    private bool _ambientAnimationStarted;
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
        StartAmbientBubbleAnimation();
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
            _statusOverride = "10件いっぱいです。どれかの泡をはじいてから追加してください。";
            UpdateStatus();
            return;
        }

        var memo = new MemoItem
        {
            Text = text,
            CreatedAt = DateTimeOffset.Now
        };

        _pendingSpawnIds.Add(memo.Id);
        Memos.Insert(0, memo);

        MemoInput.Clear();
        _statusOverride = null;
        SaveMemos();
        UpdateStatus();

        // If layout has already created the visual before Loaded dispatches, make sure the birth animation is still requested.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var host = FindBubbleHost(memo.Id);
            if (host is not null && _pendingSpawnIds.Remove(memo.Id))
            {
                PlaySpawnAnimation(host);
            }
        }), DispatcherPriority.Loaded);
    }

    private async void CompleteMemo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not Guid id)
        {
            return;
        }

        await CompleteMemoWithAnimationAsync(id, keepKeyboardNavigation: false);
    }

    private async Task CompleteMemoWithAnimationAsync(Guid id, bool keepKeyboardNavigation)
    {
        if (!_removingMemoIds.Add(id))
        {
            return;
        }

        try
        {
            var index = Memos.ToList().FindIndex(memo => memo.Id == id);
            if (index < 0)
            {
                return;
            }

            var host = FindBubbleHost(id);
            if (host is not null)
            {
                await PlayPopAnimationAsync(host);
            }

            // The memo is removed only after the bubble has popped so the motion remains spatially coherent.
            index = Memos.ToList().FindIndex(memo => memo.Id == id);
            if (index < 0)
            {
                return;
            }

            Memos.RemoveAt(index);
            _statusOverride = null;
            SaveMemos();
            UpdateStatus();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!keepKeyboardNavigation || Memos.Count == 0)
                {
                    FocusInput();
                    return;
                }

                FocusMemoCompletionButton(Math.Min(index, Memos.Count - 1));
            }), DispatcherPriority.Loaded);
        }
        finally
        {
            _removingMemoIds.Remove(id);
        }
    }

    private void BubbleHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement host || host.Tag is not Guid id)
        {
            return;
        }

        StartIdleFloat(host, id);

        if (_pendingSpawnIds.Remove(id))
        {
            PlaySpawnAnimation(host);
        }
    }

    private void StartIdleFloat(FrameworkElement host, Guid id)
    {
        if (!_floatingBubbleHosts.Add(host))
        {
            return;
        }

        if (host.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            host.RenderTransform = translate;
        }

        var seed = Math.Abs(id.GetHashCode());
        var amplitudeY = 3.5 + seed % 35 / 10.0; // 3.5 - 6.9 px
        var amplitudeX = 0.8 + seed % 16 / 10.0; // 0.8 - 2.3 px
        var durationY = TimeSpan.FromSeconds(5.6 + seed % 30 / 10.0);
        var durationX = TimeSpan.FromSeconds(6.8 + seed % 37 / 10.0);
        var delay = TimeSpan.FromMilliseconds(seed % 900);

        var yAnimation = new DoubleAnimation
        {
            From = amplitudeY * 0.35,
            To = -amplitudeY,
            Duration = durationY,
            BeginTime = delay,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var xAnimation = new DoubleAnimation
        {
            From = -amplitudeX,
            To = amplitudeX,
            Duration = durationX,
            BeginTime = TimeSpan.FromMilliseconds(120 + seed % 1200),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        translate.BeginAnimation(TranslateTransform.YProperty, yAnimation);
        translate.BeginAnimation(TranslateTransform.XProperty, xAnimation);
    }

    private void PlaySpawnAnimation(FrameworkElement host)
    {
        var visual = FindNamedDescendant<Border>(host, "BubbleVisual");
        if (visual is null || !TryGetBubbleTransforms(visual, out var scale, out var translate))
        {
            return;
        }

        visual.Opacity = 0;
        scale.ScaleX = 0.42;
        scale.ScaleY = 0.42;
        translate.Y = 68;
        translate.X = _random.NextDouble() * 16 - 8;

        var opacity = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(230),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleFrames = new DoubleAnimationUsingKeyFrames();
        scaleFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.42, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.065, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(410)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        scaleFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(570)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        });

        var rise = new DoubleAnimation
        {
            From = 68,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(570),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var settleX = new DoubleAnimation
        {
            From = translate.X,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(520),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };

        visual.BeginAnimation(UIElement.OpacityProperty, opacity);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleFrames);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleFrames.Clone());
        translate.BeginAnimation(TranslateTransform.YProperty, rise);
        translate.BeginAnimation(TranslateTransform.XProperty, settleX);

        SpawnBirthParticles(host);
    }

    private async Task PlayPopAnimationAsync(FrameworkElement host)
    {
        var visual = FindNamedDescendant<Border>(host, "BubbleVisual");
        if (visual is null || !TryGetBubbleTransforms(visual, out var scale, out var translate))
        {
            return;
        }

        var center = host.TranslatePoint(
            new Point(Math.Max(0, host.ActualWidth / 2), Math.Max(0, host.ActualHeight / 2)),
            ParticleLayer);

        var completion = new TaskCompletionSource<bool>();

        var scaleFrames = new DoubleAnimationUsingKeyFrames
        {
            FillBehavior = FillBehavior.HoldEnd
        };
        scaleFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn }
        });
        scaleFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.095, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(145)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        scaleFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(225)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        var fadeFrames = new DoubleAnimationUsingKeyFrames
        {
            FillBehavior = FillBehavior.HoldEnd
        };
        fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(85))));
        fadeFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(225)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        fadeFrames.Completed += (_, _) => completion.TrySetResult(true);

        var lift = new DoubleAnimation
        {
            From = translate.Y,
            To = translate.Y - 4,
            Duration = TimeSpan.FromMilliseconds(225),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleFrames);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleFrames.Clone());
        translate.BeginAnimation(TranslateTransform.YProperty, lift);
        visual.BeginAnimation(UIElement.OpacityProperty, fadeFrames);

        await Task.Delay(82);
        SpawnPopParticles(center);
        await completion.Task;
    }

    private void SpawnBirthParticles(FrameworkElement host)
    {
        if (!IsLoaded || host.ActualWidth <= 0 || host.ActualHeight <= 0)
        {
            return;
        }

        var origin = host.TranslatePoint(
            new Point(host.ActualWidth * 0.5, host.ActualHeight * 0.9),
            ParticleLayer);

        for (var i = 0; i < 3; i++)
        {
            var size = 4.0 + _random.NextDouble() * 4.0;
            var dx = (_random.NextDouble() - 0.5) * 24;
            var dy = -(22 + _random.NextDouble() * 26);
            CreateParticle(origin, size, dx, dy, 0.42 + _random.NextDouble() * 0.20, 420 + _random.Next(0, 180));
        }
    }

    private void SpawnPopParticles(Point origin)
    {
        var count = _random.Next(5, 8);
        for (var i = 0; i < count; i++)
        {
            var size = 4.0 + _random.NextDouble() * 7.0;
            var angle = Math.PI * (0.08 + _random.NextDouble() * 0.84);
            var distance = 14 + _random.NextDouble() * 24;
            var dx = Math.Cos(angle) * distance * (_random.Next(0, 2) == 0 ? -1 : 1);
            var dy = -Math.Abs(Math.Sin(angle) * distance) - _random.NextDouble() * 8;
            CreateParticle(origin, size, dx, dy, 0.62 + _random.NextDouble() * 0.24, 190 + _random.Next(0, 100));
        }
    }

    private void CreateParticle(Point origin, double size, double dx, double dy, double opacity, int durationMs)
    {
        var particle = new Ellipse
        {
            Width = size,
            Height = size,
            Opacity = opacity,
            Fill = CreateParticleBrush(),
            Stroke = new SolidColorBrush(Color.FromArgb(145, 116, 204, 235)),
            StrokeThickness = Math.Max(0.5, size / 10),
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        var translate = new TranslateTransform();
        var scale = new ScaleTransform(0.78, 0.78);
        var transforms = new TransformGroup();
        transforms.Children.Add(scale);
        transforms.Children.Add(translate);
        particle.RenderTransform = transforms;

        Canvas.SetLeft(particle, origin.X - size / 2);
        Canvas.SetTop(particle, origin.Y - size / 2);
        ParticleLayer.Children.Add(particle);

        var duration = TimeSpan.FromMilliseconds(durationMs);
        var xAnimation = new DoubleAnimation(0, dx, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var yAnimation = new DoubleAnimation(0, dy, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var opacityAnimation = new DoubleAnimation(opacity, 0, duration)
        {
            BeginTime = TimeSpan.FromMilliseconds(durationMs * 0.30),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var scaleAnimation = new DoubleAnimation(0.78, 1.08, duration)
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };

        opacityAnimation.Completed += (_, _) => ParticleLayer.Children.Remove(particle);

        translate.BeginAnimation(TranslateTransform.XProperty, xAnimation);
        translate.BeginAnimation(TranslateTransform.YProperty, yAnimation);
        particle.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
    }

    private static Brush CreateParticleBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.35, 0.30),
            GradientOrigin = new Point(0.30, 0.25),
            RadiusX = 0.72,
            RadiusY = 0.72
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(245, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(220, 218, 247, 255), 0.52));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(90, 132, 214, 240), 1));
        return brush;
    }

    private void StartAmbientBubbleAnimation()
    {
        if (_ambientAnimationStarted)
        {
            return;
        }

        _ambientAnimationStarted = true;

        var bubbles = AmbientLayer.Children
            .OfType<Ellipse>()
            .Where(ellipse => ellipse.Width <= 30 && ellipse.Height <= 30)
            .ToList();

        foreach (var bubble in bubbles)
        {
            var translate = new TranslateTransform();
            bubble.RenderTransform = translate;

            var rise = 26 + _random.NextDouble() * 58;
            var drift = (_random.NextDouble() - 0.5) * 16;
            var duration = TimeSpan.FromSeconds(7.5 + _random.NextDouble() * 5.5);
            var delay = TimeSpan.FromMilliseconds(_random.Next(0, 1600));

            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 8,
                To = -rise,
                Duration = duration,
                BeginTime = delay,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
            {
                From = -drift * 0.35,
                To = drift,
                Duration = TimeSpan.FromSeconds(duration.TotalSeconds * 1.17),
                BeginTime = delay,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });

            bubble.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = Math.Max(0.12, bubble.Opacity * 0.72),
                To = Math.Min(0.34, bubble.Opacity * 1.18),
                Duration = TimeSpan.FromSeconds(duration.TotalSeconds * 0.68),
                BeginTime = delay,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        }
    }

    private static bool TryGetBubbleTransforms(Border visual, out ScaleTransform scale, out TranslateTransform translate)
    {
        scale = null!;
        translate = null!;

        if (visual.RenderTransform is not TransformGroup group)
        {
            return false;
        }

        scale = group.Children.OfType<ScaleTransform>().FirstOrDefault()!;
        translate = group.Children.OfType<TranslateTransform>().FirstOrDefault()!;
        return scale is not null && translate is not null;
    }

    private FrameworkElement? FindBubbleHost(Guid id)
    {
        return FindDescendant<Grid>(this, grid => grid.Name == "BubbleHost" && grid.Tag is Guid gridId && gridId == id);
    }

    private static T? FindNamedDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        return FindDescendant<T>(root, element => element.Name == name);
    }

    private static T? FindDescendant<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && predicate(typed))
            {
                return typed;
            }

            var nested = FindDescendant(child, predicate);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
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
        var normalStatus = $"{Memos.Count} / {MaxMemoCount} 件 · {_hotkeySettings.DisplayText} で表示/非表示 · ↓で泡を選択 / Deleteで弾く";
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
            _ = CompleteMemoWithAnimationAsync(id, keepKeyboardNavigation: true);
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
        return FindDescendant<WpfButton>(root, button => button.Tag is Guid buttonId && buttonId == id);
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
