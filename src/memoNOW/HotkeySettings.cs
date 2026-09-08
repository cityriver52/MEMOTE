using System.Windows.Input;

namespace memoNOW;

public sealed class HotkeySettings
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; } = true;
    public bool Win { get; set; } = true;
    public string Key { get; set; } = nameof(System.Windows.Input.Key.Space);

    public static HotkeySettings Default => new();

    public HotkeySettings Clone() => new()
    {
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        Key = Key
    };

    public bool TryGetKey(out System.Windows.Input.Key key)
    {
        return Enum.TryParse(Key, ignoreCase: true, out key) && key != System.Windows.Input.Key.None;
    }

    public ModifierKeys GetModifiers()
    {
        var modifiers = ModifierKeys.None;
        if (Ctrl) modifiers |= ModifierKeys.Control;
        if (Alt) modifiers |= ModifierKeys.Alt;
        if (Shift) modifiers |= ModifierKeys.Shift;
        if (Win) modifiers |= ModifierKeys.Windows;
        return modifiers;
    }

    public string DisplayText
    {
        get
        {
            if (!TryGetKey(out var key))
            {
                return "未設定";
            }

            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(GetFriendlyKeyName(key));
            return string.Join(" + ", parts);
        }
    }

    public bool IsValid(out string message)
    {
        if (!TryGetKey(out var key))
        {
            message = "キーを選択してください。";
            return false;
        }

        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
        {
            message = "修飾キーだけでは登録できません。";
            return false;
        }

        if (GetModifiers() == ModifierKeys.None)
        {
            message = "誤操作を避けるため、Ctrl / Alt / Shift / Win のいずれかを組み合わせてください。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static HotkeySettings FromInput(System.Windows.Input.Key key, ModifierKeys modifiers)
    {
        return new HotkeySettings
        {
            Ctrl = modifiers.HasFlag(ModifierKeys.Control),
            Alt = modifiers.HasFlag(ModifierKeys.Alt),
            Shift = modifiers.HasFlag(ModifierKeys.Shift),
            Win = modifiers.HasFlag(ModifierKeys.Windows),
            Key = key.ToString()
        };
    }

    private static string GetFriendlyKeyName(System.Windows.Input.Key key)
    {
        return key switch
        {
            System.Windows.Input.Key.Space => "Space",
            System.Windows.Input.Key.Return => "Enter",
            System.Windows.Input.Key.Escape => "Esc",
            System.Windows.Input.Key.Next => "PageDown",
            System.Windows.Input.Key.Prior => "PageUp",
            _ => key.ToString()
        };
    }
}
