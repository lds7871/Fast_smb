using System.Text;

namespace Fast_smb.Pages;

public enum PreviewKind
{
    Text,
    Image,
}

public partial class PreviewPage : ContentPage
{
    private readonly PreviewKind _kind;
    private readonly byte[] _data;

    public PreviewPage(string title, PreviewKind kind, byte[] data)
    {
        InitializeComponent();
        Title = title;
        _kind = kind;
        _data = data;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ShowContent();
    }

    private void ShowContent()
    {
        if (_kind == PreviewKind.Image)
        {
            PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_data));
            PreviewImage.IsVisible = true;
            Busy.IsVisible = false;
        }
        else
        {
            TextLabel.Text = DecodeText(_data);
            TextScroll.IsVisible = true;
            Busy.IsVisible = false;
        }
    }

    /// <summary>文本解码：优先 UTF-8（严格校验），失败回退 GBK，再回退系统默认。</summary>
    private static string DecodeText(byte[] data)
    {
        // UTF-8 严格模式：含非法序列则抛异常
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return utf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
        }

        try
        {
            var gbk = Encoding.GetEncoding("GBK");
            return gbk.GetString(data);
        }
        catch
        {
        }

        return Encoding.Default.GetString(data);
    }
}
