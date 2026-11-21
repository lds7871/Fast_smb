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
    private bool _downloading;              // 正在下载当前图片
    private CancellationTokenSource? _downloadCts;

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

        // 图片预览才显示左下角索引与右下角下载按钮
        IndexBadge.IsVisible = true;
        DownloadBtn.IsVisible = true;
        UpdateTitle();
        if (_currentImage == null)
            RequestImage(_index);   // 首次进入，或上次离开时读取被取消
        else
            ShowImage(_currentImage);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // 离开预览页时中断未完成的读取与下载，把 SMB 连接让给浏览页
        _cts?.Cancel();
        _downloadCts?.Cancel();
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
        // 顶栏只显示文件名；「当前/总数」索引移到左下角悬浮文本
        Title = _images[_index].Name;
        IndexLabel.Text = $"{_index + 1}/{_images.Count}";
    }

    // ---------- 下载当前图片 ----------

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        if (_images.Count == 0)
            return;
        await SaveCurrentImageAsync(_images[_index]);
    }

    /// <summary>把当前这张图片完整下载到手机「下载」目录（复用与浏览页相同的落盘逻辑）。</summary>
    private async Task SaveCurrentImageAsync(PreviewImage item)
    {
        if (_downloading)
            return;

        var ensure = await Task.Run(() => SmbSession.Instance.EnsureConnected());
        if (!ensure.ok)
        {
            await Banner.ShowAsync(L10n.Instance["download_fail"] + (ensure.message ?? L10n.Instance["unknown_err"]), error: true, durationMs: 2500);
            return;
        }

        _downloading = true;
        DownloadBtn.IsEnabled = false;
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        try
        {
            var ok = await Task.Run(async () =>
            {
                using var output = DownloadSaver.CreateDownloadStream(item.Name);
                if (output == null)
                    return false;
                // 重新按完整大小读取并落盘（预览只读到 20MB 上限，直接存内存数据可能截断大文件）
                var result = await SmbSession.Instance.DownloadFileAsync(item.RemotePath, output, null, token);
                return result.ok;
            });

            await Banner.ShowAsync(
                ok ? $"{L10n.Instance["download_done"]}{item.Name}" : L10n.Instance["download_failed"],
                error: !ok,
                durationMs: ok ? 1200 : 2500);
        }
        catch (OperationCanceledException)
        {
            // 离开页面导致的中断，忽略
        }
        catch (Exception ex)
        {
            await Banner.ShowAsync(L10n.Instance["download_fail"] + ex.Message, error: true, durationMs: 2500);
        }
        finally
        {
            _downloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            DownloadBtn.IsEnabled = true;
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
