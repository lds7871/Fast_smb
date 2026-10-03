using Fast_smb.Services;

namespace Fast_smb.Controls;

/// <summary>
/// 排序方式选择弹窗：四个排序按钮 + 取消，当前生效的方式高亮显示。
/// 交互（淡入淡出、遮罩、紫色边框卡片）与共享选择弹窗保持一致。
/// </summary>
public partial class SortPickerView : ContentView
{
    private TaskCompletionSource<SortMode?>? _tcs;

    public SortPickerView()
    {
        InitializeComponent();
    }

    /// <summary>显示弹窗，返回选中的排序方式；取消返回 null。</summary>
    public async Task<SortMode?> ShowAsync(SortMode current)
    {
        ModeList.Children.Clear();
        foreach (var mode in SortSettings.All)
        {
            bool active = mode == current;
            var btn = new Button
            {
                Text = L10n.Instance[SortSettings.TextKey(mode)],
                // 当前方式用强调色实心按钮，其余为次级样式
                BackgroundColor = Theme.Get(active ? "Accent" : "Surface2"),
                TextColor = Theme.Get(active ? "TextOnAccent" : "Accent"),
                FontSize = 16,
                HeightRequest = 48,
                CornerRadius = 10,
            };
            var picked = mode;
            btn.Clicked += (_, _) => _tcs?.TrySetResult(picked);
            ModeList.Children.Add(btn);
        }

        _tcs = new TaskCompletionSource<SortMode?>();
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

    /// <summary>外部关闭弹窗（如按系统返回键），等同于点「取消」。</summary>
    public void Close() => _tcs?.TrySetResult(null);
}
