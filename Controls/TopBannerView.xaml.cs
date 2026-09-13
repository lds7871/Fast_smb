using Fast_smb.Services;

namespace Fast_smb.Controls;

/// <summary>
/// 应用顶部的下滑横幅提示：快速滑入显示一段时间后自动收回。
/// </summary>
public partial class TopBannerView : ContentView
{
    public TopBannerView()
    {
        InitializeComponent();
    }

    public Task ShowAsync(string message, bool error = false, int durationMs = 1000)
    {
        MsgLabel.Text = message;
        MsgLabel.TextColor = error ? Theme.Get("Error") : Theme.Get("TextPrimary");
        Banner.Stroke = error ? Theme.Get("Error") : Theme.Get("Accent");
        return AnimateAsync(durationMs);
    }

    private async Task AnimateAsync(int durationMs)
    {
        Banner.IsVisible = true;
        Banner.Opacity = 0;
        Banner.TranslationY = -80;

        // 快速滑入
        await Task.WhenAll(
            Banner.FadeToAsync(1, 140),
            Banner.TranslateToAsync(0, 0, 170, Easing.CubicOut));

        // 停留
        await Task.Delay(durationMs);

        // 收回
        await Task.WhenAll(
            Banner.FadeToAsync(0, 150),
            Banner.TranslateToAsync(0, -80, 160, Easing.CubicIn));

        Banner.IsVisible = false;
    }
}
