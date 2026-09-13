namespace Fast_smb.Controls;

/// <summary>
/// 应用内共享选择弹窗：快速淡入淡出，紫色边框风格。
/// </summary>
public partial class SharePickerView : ContentView
{
    private TaskCompletionSource<string?>? _tcs;

    public SharePickerView()
    {
        InitializeComponent();
    }

    /// <summary>显示弹窗，返回选中的共享名；取消返回 null。</summary>
    public async Task<string?> ShowAsync(IEnumerable<string> shares)
    {
        ShareList.Children.Clear();
        foreach (var share in shares)
        {
            var btn = new Button
            {
                Text = share,
                BackgroundColor = Color.FromArgb("#21262D"),
                TextColor = Color.FromArgb("#A371F7"),
                FontSize = 16,
                HeightRequest = 48,
                CornerRadius = 10,
            };
            var picked = share;
            btn.Clicked += (_, _) => _tcs?.TrySetResult(picked);
            ShareList.Children.Add(btn);
        }

        _tcs = new TaskCompletionSource<string?>();
        IsVisible = true;
        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = 0.92;

        // 快速淡入
        await Task.WhenAll(
            Scrim.FadeToAsync(1, 120),
            Card.FadeToAsync(1, 120),
            Card.ScaleToAsync(1, 130, Easing.CubicOut));

        var result = await _tcs.Task;

        // 快速淡出
        await Task.WhenAll(
            Scrim.FadeToAsync(0, 100),
            Card.FadeToAsync(0, 100),
            Card.ScaleToAsync(0.95, 100, Easing.CubicIn));

        IsVisible = false;
        return result;
    }

    private void OnCancelClicked(object? sender, EventArgs e) => _tcs?.TrySetResult(null);
}
