using System.Text;
using Fast_smb.Services;

namespace Fast_smb.Pages;

/// <summary>预览页里可左右滑动切换的一张图片（同一目录，按浏览器当前显示顺序）。</summary>
public record PreviewImage(string Name, string RemotePath);

public partial class PreviewPage : ContentPage
{
    /// <summary>图片预览单张大小上限（超过则只读到上限，与浏览器页原有限制一致）。</summary>
    private const int MaxImageBytes = 20 * 1024 * 1024;

    private readonly IReadOnlyList<PreviewImage> _images = Array.Empty<PreviewImage>();
    private readonly byte[]? _textData;
    private int _index;                     // 当前已显示的图片
    private int _targetIndex;               // 正载入/待载入的目标（滑动时立即推进，不等加载完成）
    private byte[]? _currentImage;          // 当前已加载的图片数据
    private CancellationTokenSource? _cts;  // 正在进行的图片读取
    private bool _loading;
    private int _pendingIndex = -1;         // 加载期间用户又滑到的目标

    /// <summary>文本预览（内容已在浏览器页读取好）。</summary>
    public PreviewPage(string title, byte[] data)
    {
        InitializeComponent();
        Title = title;
        _textData = data;
    }

    /// <summary>图片预览：images 为同目录图片（含当前这张），index 为起始下标，支持左右滑动切换。</summary>
    public PreviewPage(IReadOnlyList<PreviewImage> images, int index)
    {
        InitializeComponent();
        _images = images;
        _index = Math.Clamp(index, 0, Math.Max(0, images.Count - 1));
        _targetIndex = _index;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_textData != null)
        {
            TextLabel.Text = DecodeText(_textData);
            TextScroll.IsVisible = true;
            Busy.IsVisible = false;
            return;
        }

        UpdateTitle();
        if (_currentImage == null)
            RequestImage(_index);   // 首次进入，或上次离开时读取被取消
        else
            ShowImage(_currentImage);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // 离开预览页时中断未完成的读取，把 SMB 连接让给浏览页
        _cts?.Cancel();
    }

    // ---------- 左右滑动切换图片 ----------

    private void OnSwipedLeft(object? sender, SwipedEventArgs e) => RequestImage(_targetIndex + 1);

    private void OnSwipedRight(object? sender, SwipedEventArgs e) => RequestImage(_targetIndex - 1);

    /// <summary>请求显示第 index 张；加载期间再滑动只记下目标并打断当前读取（SMB 读取串行，避免并发访问会话）。</summary>
    private void RequestImage(int index)
    {
        if (_images.Count == 0)
            return;
        index = Math.Clamp(index, 0, _images.Count - 1);
        _targetIndex = index; // 立即推进，连滑多次才能一次跨多张

        if (_loading)
        {
            _pendingIndex = index;
            _cts?.Cancel();
            return;
        }
        if (index == _index && _currentImage != null)
            return; // 已经在这张上
        _ = LoadImageAsync(index);
    }

    private async Task LoadImageAsync(int index)
    {
        _loading = true;
        try
        {
            while (true)
            {
                int target = index;
                _pendingIndex = -1;
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                var item = _images[target];

                Busy.IsVisible = true;
                ErrorLabel.IsVisible = false;

                var result = await Task.Run(() => SmbSession.Instance.ReadBytesAsync(item.RemotePath, MaxImageBytes, token));
#if ANDROID
                Android.Util.Log.Info("FastSMB", $"Preview: image \"{item.Name}\" ({target + 1}/{_images.Count}) ok={result.ok} bytes={result.data?.Length ?? 0} cancelled={token.IsCancellationRequested}");
#endif

                if (token.IsCancellationRequested)
                {
                    // 已被新的滑动打断：还有目标就继续加载，没有（如离开页面）就结束
                    if (_pendingIndex >= 0)
                    {
                        index = _pendingIndex;
                        continue;
                    }
                    _currentImage = null;
                    return;
                }

                _index = target;
                _targetIndex = target;
                UpdateTitle();

                if (!result.ok || result.data == null)
                {
                    _currentImage = null;
                    PreviewImage.IsVisible = false;
                    ErrorLabel.Text = L10n.Instance["preview_fail"] + (result.message ?? L10n.Instance["unknown_err"]);
                    ErrorLabel.IsVisible = true;
                    Busy.IsVisible = false;
                    return;
                }

                _currentImage = result.data;
                ShowImage(result.data);
                return;
            }
        }
        catch (Exception ex)
        {
            _currentImage = null;
            PreviewImage.IsVisible = false;
            ErrorLabel.Text = L10n.Instance["preview_fail"] + ex.Message;
            ErrorLabel.IsVisible = true;
            Busy.IsVisible = false;
#if ANDROID
            Android.Util.Log.Info("FastSMB", $"Preview: load image failed {ex.Message}");
#endif
        }
        finally
        {
            _loading = false;
        }
    }

    private void ShowImage(byte[] data)
    {
        var bytes = data; // 闭包持有数据，供 ImageSource 按需反复建流
        PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        PreviewImage.IsVisible = true;
        ErrorLabel.IsVisible = false;
        Busy.IsVisible = false;
    }

    private void UpdateTitle()
    {
        if (_images.Count == 0)
            return;
        Title = $"{_images[_index].Name} ({_index + 1}/{_images.Count})";
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
