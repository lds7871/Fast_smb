namespace Fast_smb.Services;

/// <summary>
/// 主题工具：代码中取当前主题下的语义颜色；切换主题并持久化。
/// 色板见 Resources/Styles/ThemeColors.xaml（key = 语义名 + Light/Dark）。
/// </summary>
public static class Theme
{
    /// <summary>取当前主题下的语义颜色（如 "Accent"、"Error"、"Surface2"、"TextPrimary"）。</summary>
    public static Color Get(string semanticKey)
    {
        var isDark = Application.Current?.UserAppTheme != AppTheme.Light;
        var key = semanticKey + (isDark ? "Dark" : "Light");
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
            return color;
        return Colors.White;
    }

    /// <summary>切换应用主题并保存偏好。</summary>
    public static AppTheme Toggle()
    {
        var next = Application.Current?.UserAppTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        Apply(next);
        return next;
    }

    public static void Apply(AppTheme theme)
    {
        if (Application.Current != null)
            Application.Current.UserAppTheme = theme;
        Preferences.Default.Set("theme", theme == AppTheme.Light ? "light" : "dark");
    }

    /// <summary>读取保存的主题偏好（默认暗色）。</summary>
    public static AppTheme Load()
        => Preferences.Default.Get("theme", "dark") == "light" ? AppTheme.Light : AppTheme.Dark;
}
