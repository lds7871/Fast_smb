using Fast_smb.Models;
using Fast_smb.Services;

namespace Fast_smb.Pages;

public partial class FileBrowserPage : ContentPage
{
    /// <summary>顶栏布局：true=多行（退出+路径 / 功能按钮两行），false=单行（Shell 导航栏，默认）。</summary>
    public static bool IsMultiLineToolbar
    {
        get => Preferences.Default.Get("toolbar_mode", "single") == "multi";
        set => Preferences.Default.Set("toolbar_mode", value ? "multi" : "single");
    }

    private static readonly HashSet<string> TextExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".json", ".xml", ".csv", ".ini", ".conf", ".cfg",
        ".sh", ".py", ".cs", ".java", ".c", ".cpp", ".h", ".html", ".htm", ".css", ".js", ".ts", ".yml", ".yaml",
    };
    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
    };
    private static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".3gp", ".m4v", ".wmv", ".flv",
    };
    private static readonly HashSet<string> AudioExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".opus", ".wma", ".ape", ".amr", ".mid", ".midi",
    };

    private readonly List<string> _pathStack = new();
    private string _currentPath = ""; // "" 表示共享根目录
    private bool _busy;
    private CancellationTokenSource? _cts;
    private bool _isBatchMode;
    private bool _isDeleteMode;
    private List<SmbEntry> _currentEntries = new();
    private List<SmbEntry> _displayedEntries = new();
    private bool _batchAbort;

    public FileBrowserPage()
    {
        InitializeComponent();

        // 单行/多行共用同一套自定义顶栏，始终隐藏 Shell 导航栏
        Shell.SetNavBarIsVisible(this, false);
        ApplyToolbarLayout();
    }

    /// <summary>根据顶栏模式设置布局：单行=一行（退出+路径+功能按钮），多行=两行（第一行退出+路径，第二行功能按钮靠右）。</summary>
    private void ApplyToolbarLayout()
    {
        if (IsMultiLineToolbar)
        {
            // 多行：两行，功能按钮移到第二行（占满第 1、2 列，靠右）
            TopToolbar.RowDefinitions.Clear();
            TopToolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TopToolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FuncBar.SetValue(Grid.RowProperty, 1);
            FuncBar.SetValue(Grid.ColumnProperty, 1);
            FuncBar.SetValue(Grid.ColumnSpanProperty, 2);
            ExitBtn.SetValue(Grid.RowProperty, 0);
            PathLabel.SetValue(Grid.RowProperty, 0);
        }
        else
        {
            // 单行：一行，功能按钮与退出/路径同行靠右
            TopToolbar.RowDefinitions.Clear();
            TopToolbar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FuncBar.SetValue(Grid.RowProperty, 0);
            FuncBar.SetValue(Grid.ColumnProperty, 2);
            FuncBar.SetValue(Grid.ColumnSpanProperty, 1);
            ExitBtn.SetValue(Grid.RowProperty, 0);
            PathLabel.SetValue(Grid.RowProperty, 0);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 从系统应用（视频播放器/文件选择器）返回时连接可能已被回收：
        // 先尝试自动重连，只有重连失败才退回连接页（而不是因 IsConnected 短暂为 false 被弹出）。
        var ensure = await Task.Run(() => SmbSession.Instance.EnsureConnected());
        if (!ensure.ok)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        UpdateTitle();
        await LoadDirectoryAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_busy)
        {
            _cts?.Cancel();
            _busy = false;
            SetProgressVisible(false);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_busy)
        {
            _cts?.Cancel();
            return true;
        }
        // 删除/批量选择模式下按返回：先退出选择模式，而不是返回上一级目录
        if (_isDeleteMode || _isBatchMode)
        {
            if (_isDeleteMode)
                SetDeleteMode(false);
            else
                SetBatchMode(false);
            return true;
        }
        if (_pathStack.Count > 0)
        {
            _currentPath = _pathStack[^1];
            _pathStack.RemoveAt(_pathStack.Count - 1);
            UpdateTitle();
            ClearSearch();
            _ = LoadDirectoryAsync();
            return true;
        }
        return false; // 返回连接页
    }

    private static string CombinePath(string parent, string name) =>
        string.IsNullOrEmpty(parent) ? name : parent + "/" + name;

    private void UpdateTitle()
    {
        var share = SmbSession.Instance.Share;
        var path = string.IsNullOrEmpty(_currentPath) ? share : $"{share} / {_currentPath}";
        Title = path;
        // 多行模式：路径显示在第一行靠左的 Label（自动收缩，不被功能按钮覆盖）
        if (PathLabel != null)
            PathLabel.Text = path;
    }

    /// <summary>顶栏的「退出」：返回连接页（会自动断开）。</summary>
    private async void OnExitTapped(object? sender, TappedEventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    private bool _loading;
    private async Task LoadDirectoryAsync()
    {
        if (_loading)
            return; // 防止重复加载
        _loading = true;
        try
        {
            // 加载期间显示动画并清空上一级内容
            LoadingPanel.IsVisible = true;
            FileList.ItemsSource = null;
            EmptyLabel.IsVisible = false;

            var result = await Task.Run(() => SmbSession.Instance.ListDirectory(_currentPath));
            if (result.entries != null)
            {
                _currentEntries = result.entries;
                ApplyBatchModeToEntries();
                ApplyDeleteModeToEntries();
                ApplySearch();
            }
            else
            {
                ApplySearch();
                await Banner.ShowAsync(L10n.Instance["load_dir_fail"] + (result.message ?? L10n.Instance["unknown_err"]), error: true, durationMs: 2500);
            }
        }
        catch (Exception ex)
        {
            ApplySearch();
            await Banner.ShowAsync(L10n.Instance["load_dir_fail"] + ex.Message, error: true, durationMs: 2500);
        }
        finally
        {
            LoadingPanel.IsVisible = false;
            _loading = false;
        }
    }

    // ---------- 搜索 ----------

    private void OnSearchClicked(object? sender, EventArgs e)
    {
        SearchBar.IsVisible = !SearchBar.IsVisible;
        if (SearchBar.IsVisible)
        {
            SearchEntry.Focus();
        }
        else
        {
            ClearSearch();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySearch();
    }

    private void OnSearchCancelClicked(object? sender, EventArgs e)
    {
        SearchBar.IsVisible = false;
        ClearSearch();
        SearchEntry.Unfocus();
    }

    private void ClearSearch()
    {
        SearchEntry.Text = "";
        ApplySearch();
    }

    /// <summary>按搜索词过滤当前目录条目并刷新列表（空词显示全部）。</summary>
    private void ApplySearch()
    {
        var keyword = SearchEntry?.Text?.Trim() ?? "";
        if (keyword.Length == 0)
        {
            _displayedEntries = _currentEntries;
            EmptyLabel.Text = "此目录为空";
        }
        else
        {
            _displayedEntries = _currentEntries
                .Where(e => e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
            EmptyLabel.Text = "没有匹配的文件";
        }
        FileList.ItemsSource = _displayedEntries;
        EmptyLabel.IsVisible = _displayedEntries.Count == 0;
        UpdateSelectionCounts();
    }

    // ---------- 批量模式 / 删除模式（两套互斥的选择模式，共用勾选 UI） ----------

    private void ApplyBatchModeToEntries()
    {
        foreach (var e in _currentEntries)
            e.IsBatchMode = _isBatchMode;
    }

    private void ApplyDeleteModeToEntries()
    {
        foreach (var e in _currentEntries)
            e.IsDeleteMode = _isDeleteMode;
    }

    private void SetBatchMode(bool on)
    {
        if (on && _isDeleteMode)
            SetDeleteMode(false); // 两种选择模式互斥
        _isBatchMode = on;
        BatchPanel.IsVisible = on;
        ApplyBatchModeToEntries();
        if (!on)
        {
            foreach (var e in _currentEntries)
                e.IsSelected = false;
        }
        UpdateSelectionCounts();
    }

    private void SetDeleteMode(bool on)
    {
        if (on && _isBatchMode)
            SetBatchMode(false); // 两种选择模式互斥
        _isDeleteMode = on;
        DeletePanel.IsVisible = on;
        ApplyDeleteModeToEntries();
        if (!on)
        {
            foreach (var e in _currentEntries)
                e.IsSelected = false;
        }
        UpdateSelectionCounts();
    }

    /// <summary>刷新两个选择面板的计数（批量：下载所选 N；删除：删除所选 N）。</summary>
    private void UpdateSelectionCounts()
    {
        int n = _displayedEntries.Count(e => e.IsSelected);
        if (BatchDownloadBtn != null)
        {
            BatchDownloadBtn.Text = n > 0 ? $"{L10n.Instance["download_selected"]} ({n})" : $"{L10n.Instance["download_selected"]} (0)";
            BatchDownloadBtn.IsEnabled = n > 0;
        }
        if (DeleteSelectedBtn != null)
        {
            DeleteSelectedBtn.Text = n > 0 ? $"{L10n.Instance["delete_selected"]} ({n})" : $"{L10n.Instance["delete_selected"]} (0)";
            DeleteSelectedBtn.IsEnabled = n > 0;
        }
    }

    private void OnBatchToggleClicked(object? sender, EventArgs e) => SetBatchMode(!_isBatchMode);

    private void OnDeleteToggleClicked(object? sender, EventArgs e) => SetDeleteMode(!_isDeleteMode);

    private void OnSelectAll(object? sender, EventArgs e)
    {
        foreach (var entry in _displayedEntries)
            if (_isDeleteMode || !entry.IsDirectory) // 删除模式：文件+文件夹；批量模式：仅文件
                entry.IsSelected = true;
        UpdateSelectionCounts();
    }

    private void OnInvertSelection(object? sender, EventArgs e)
    {
        foreach (var entry in _displayedEntries)
            if (_isDeleteMode || !entry.IsDirectory)
                entry.IsSelected = !entry.IsSelected;
        UpdateSelectionCounts();
    }

    private void OnSelectNone(object? sender, EventArgs e)
    {
        foreach (var entry in _displayedEntries)
            entry.IsSelected = false;
        UpdateSelectionCounts();
    }

    private void OnCancelBatch(object? sender, EventArgs e) => SetBatchMode(false);

    private void OnCancelDelete(object? sender, EventArgs e) => SetDeleteMode(false);

    private async void OnBatchDownloadClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;
        var targets = _displayedEntries.Where(x => x.IsSelected && !x.IsDirectory).ToList();
        if (targets.Count == 0)
        {
            await Banner.ShowAsync(L10n.Instance["select_first"], durationMs: 1500);
            return;
        }

        _batchAbort = false;
        int success = 0;
        foreach (var entry in targets)
        {
            if (_batchAbort)
                break;
            ProgressLabel.Text = $"{L10n.Instance["downloading"]} {entry.Name} …（{success}/{targets.Count}）";
            var ok = await DownloadOneAsync(entry);
            if (ok) success++;
        }

        await Banner.ShowAsync(
            _batchAbort
                ? $"{L10n.Instance["batch_cancelled"]} {success}/{targets.Count}"
                : $"{L10n.Instance["batch_complete"]}：{success}/{targets.Count}",
            error: success < targets.Count,
            durationMs: 2200);
        SetProgressVisible(false);
    }

    /// <summary>删除所选（文件和文件夹都支持）。确认后逐个删除，完成后刷新目录。</summary>
    private async void OnDeleteSelectedClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;
        var targets = _displayedEntries.Where(x => x.IsSelected).ToList();
        if (targets.Count == 0)
        {
            await Banner.ShowAsync(L10n.Instance["select_delete_first"], durationMs: 1500);
            return;
        }

        bool confirm = await DisplayAlertAsync(
            L10n.Instance["delete_confirm_title"],
            string.Format(L10n.Instance["delete_confirm_msg"], targets.Count),
            L10n.Instance["toolbar_delete"],
            L10n.Instance["cancel"]);
        if (!confirm)
            return;

        _busy = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        int success = 0;
        try
        {
            foreach (var entry in targets)
            {
                token.ThrowIfCancellationRequested();
                SetProgressVisible(true, $"{L10n.Instance["deleting"]} {entry.Name} …");
                var remotePath = CombinePath(_currentPath, entry.Name);
                var result = await Task.Run(() => SmbSession.Instance.DeleteEntry(remotePath, entry.IsDirectory));
                if (result.ok)
                    success++;
            }
            await Banner.ShowAsync(
                $"{L10n.Instance["delete_done"]}{success}/{targets.Count}",
                error: success < targets.Count,
                durationMs: 2200);
        }
        catch (OperationCanceledException)
        {
            await Banner.ShowAsync($"{L10n.Instance["cancelled"]} {success}/{targets.Count}", error: true, durationMs: 1800);
        }
        catch (Exception ex)
        {
            await Banner.ShowAsync(L10n.Instance["delete_fail"] + ex.Message, error: true, durationMs: 2500);
        }
        finally
        {
            _busy = false;
            SetProgressVisible(false);
        }

        // 删除操作结束（含取消/部分失败）后直接退出删除模式，不再停留在持续勾选/删除状态
        SetDeleteMode(false);
        await LoadDirectoryAsync(); // 删除后刷新目录
    }

    // ---------- 行点击：文件夹进入 / 文件预览 ----------

    private async void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not SmbEntry entry)
            return;
        if (_busy)
            return;

        if (_isDeleteMode)
        {
            // 删除模式：点条目切换勾选（文件和文件夹都可以选）
            entry.IsSelected = !entry.IsSelected;
            UpdateSelectionCounts();
        }
        else if (entry.IsDirectory)
        {
            _pathStack.Add(_currentPath);
            _currentPath = CombinePath(_currentPath, entry.Name);
            UpdateTitle();
            ClearSearch();
            await LoadDirectoryAsync();
        }
        else if (_isBatchMode)
        {
            // 批量模式：点文件切换勾选
            entry.IsSelected = !entry.IsSelected;
            UpdateSelectionCounts();
        }
        else
        {
            await PreviewAsync(entry);
        }
    }

    private void OnItemCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not SmbEntry entry)
            return;
        entry.IsSelected = e.Value;
        UpdateSelectionCounts();
    }

    // ---------- 下载按钮（仅这里触发下载） ----------

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not SmbEntry entry)
            return;
        await StartDownloadAsync(entry);
    }

    // ---------- 预览 ----------

    private async Task PreviewAsync(SmbEntry entry)
    {
        var ext = Path.GetExtension(entry.Name);
        var remotePath = CombinePath(_currentPath, entry.Name);

        if (TextExts.Contains(ext) || ImageExts.Contains(ext))
        {
            int cap = TextExts.Contains(ext) ? 512 * 1024 : 20 * 1024 * 1024;
            SetProgressVisible(true, $"{L10n.Instance["loading_preview"]} {entry.Name} …");
            _cts = new CancellationTokenSource();
            try
            {
                var result = await Task.Run(() =>
                    SmbSession.Instance.ReadBytesAsync(remotePath, cap, _cts.Token));
                if (result.ok && result.data != null)
                {
                    var kind = TextExts.Contains(ext) ? PreviewKind.Text : PreviewKind.Image;
                    await Navigation.PushAsync(new PreviewPage(entry.Name, kind, result.data));
                }
                else
                {
                    await Banner.ShowAsync(L10n.Instance["preview_fail"] + (result.message ?? L10n.Instance["unknown_err"]), error: true, durationMs: 2500);
                }
            }
            catch (Exception ex)
            {
                await Banner.ShowAsync(L10n.Instance["preview_fail"] + ex.Message, error: true, durationMs: 2500);
            }
            finally
            {
                SetProgressVisible(false);
            }
        }
        else if (VideoExts.Contains(ext))
        {
            await LoadMediaAndPlayAsync(entry, remotePath, "video/*");
        }
        else if (AudioExts.Contains(ext))
        {
            await LoadMediaAndPlayAsync(entry, remotePath, "audio/*");
        }
        else
        {
            await Banner.ShowAsync(L10n.Instance["no_preview"], durationMs: 1800);
        }
    }

    /// <summary>把媒体文件下载到缓存，再交给系统应用选择查看（视频/音频）。</summary>
    private async Task LoadMediaAndPlayAsync(SmbEntry entry, string remotePath, string mimeType)
    {
        if (_busy)
            return;
        _busy = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var cacheFile = Path.Combine(FileSystem.CacheDirectory, SanitizeName(entry.Name));
        SetProgressVisible(true, $"{L10n.Instance["loading"]} {entry.Name} …");
        ProgressBar.Progress = 0;

        var progress = new Progress<double>(p =>
        {
            ProgressBar.Progress = p;
            ProgressLabel.Text = $"{L10n.Instance["loading"]} {entry.Name} · {p:P0}";
        });

        try
        {
            var result = await Task.Run(async () =>
            {
                using var fs = new FileStream(cacheFile, FileMode.Create, FileAccess.Write);
                return await SmbSession.Instance.DownloadFileAsync(remotePath, fs, progress, token);
            });

            if (!result.ok)
            {
                await Banner.ShowAsync(L10n.Instance["load_fail"] + (result.message ?? L10n.Instance["unknown_err"]), error: true, durationMs: 2500);
                return;
            }

#if ANDROID
            var context = Android.App.Application.Context;
            var file = new Java.IO.File(cacheFile);
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                context, $"{context.PackageName}.fileprovider", file);
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            intent.SetDataAndType(uri, mimeType);
            // Application 上下文启动 Activity 必须带 NEW_TASK
            intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission
                | Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);
#endif
        }
        catch (Exception ex)
        {
            await Banner.ShowAsync(L10n.Instance["load_fail"] + ex.Message, error: true, durationMs: 2500);
        }
        finally
        {
            _busy = false;
            SetProgressVisible(false);
        }
    }

    // ---------- 下载 ----------

    private async Task StartDownloadAsync(SmbEntry entry)
    {
        var ok = await DownloadOneAsync(entry);
        await Banner.ShowAsync(
            ok ? $"{L10n.Instance["download_done"]}{entry.Name}" : L10n.Instance["download_failed"],
            error: !ok,
            durationMs: ok ? 1000 : 2500);
    }

    /// <summary>下载单个文件（共用进度面板），返回是否成功。</summary>
    private async Task<bool> DownloadOneAsync(SmbEntry entry)
    {
        if (_busy)
            return false;
        var ensure = await Task.Run(() => SmbSession.Instance.EnsureConnected());
        if (!ensure.ok)
            return false;
        _busy = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var remotePath = CombinePath(_currentPath, entry.Name);
        SetProgressVisible(true, $"{L10n.Instance["downloading"]} {entry.Name} …");
        ProgressBar.Progress = 0;

        var progress = new Progress<double>(p =>
        {
            ProgressBar.Progress = p;
            ProgressLabel.Text = $"{L10n.Instance["downloading"]} {entry.Name} · {p:P0}";
        });

        try
        {
            var result = await Task.Run(async () =>
            {
                using var output = DownloadSaver.CreateDownloadStream(entry.Name);
                if (output == null)
                    return (ok: false, message: L10n.Instance["create_file_fail"]);
                return await SmbSession.Instance.DownloadFileAsync(remotePath, output, progress, token);
            });

            if (result.ok)
            {
                return true;
            }
            await Banner.ShowAsync(L10n.Instance["download_fail"] + (result.message ?? L10n.Instance["unknown_err"]), error: true, durationMs: 1800);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            await Banner.ShowAsync(L10n.Instance["download_fail"] + ex.Message, error: true, durationMs: 1800);
            return false;
        }
        finally
        {
            _busy = false;
            SetProgressVisible(false); // 下载结束隐藏进度面板
        }
    }

    private void OnUpClicked(object? sender, EventArgs e)
    {
        if (_pathStack.Count > 0)
        {
            _currentPath = _pathStack[^1];
            _pathStack.RemoveAt(_pathStack.Count - 1);
            UpdateTitle();
            ClearSearch();
            _ = LoadDirectoryAsync();
        }
        else
        {
            _ = Shell.Current.GoToAsync("..");
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadDirectoryAsync();

    // ---------- 上传 ----------

    private async void OnUploadClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        try
        {
            // 系统文件选择器
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = L10n.Instance["toolbar_upload"],
            });
#if ANDROID
            Android.Util.Log.Info("FastSMB", $"Upload: picker returned={result?.FileName ?? "(null)"}");
#endif
            if (result == null)
                return; // 用户取消

            using var input = await result.OpenReadAsync();
#if ANDROID
            Android.Util.Log.Info("FastSMB", $"Upload: stream opened, canSeek={input?.CanSeek}");
#endif
            if (input == null)
            {
                await Banner.ShowAsync(L10n.Instance["upload_fail"] + L10n.Instance["unknown_err"], error: true, durationMs: 2500);
                return;
            }

            var remotePath = CombinePath(_currentPath, result.FileName);
            await UploadOneAsync(result.FileName, remotePath, input);
        }
        catch (Exception ex)
        {
#if ANDROID
            Android.Util.Log.Info("FastSMB", $"Upload: exception {ex}");
#endif
            await Banner.ShowAsync(L10n.Instance["upload_fail"] + ex.Message, error: true, durationMs: 2500);
        }
    }

    /// <summary>执行单个上传（共用进度面板），完成后刷新目录并横幅提示。</summary>
    private async Task UploadOneAsync(string fileName, string remotePath, Stream input)
    {
        // 文件选择器打开期间连接可能被系统回收，先确保连接可用
        var ensure = await Task.Run(() => SmbSession.Instance.EnsureConnected());
        if (!ensure.ok)
        {
            await Banner.ShowAsync(ensure.message ?? L10n.Instance["unknown_err"], error: true, durationMs: 2500);
            return;
        }

        _busy = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        SetProgressVisible(true, $"{L10n.Instance["uploading"]} {fileName} …");
        ProgressBar.Progress = 0;

        var progress = new Progress<double>(p =>
        {
            ProgressBar.Progress = p;
            ProgressLabel.Text = $"{L10n.Instance["uploading"]} {fileName} · {p:P0}";
        });

        try
        {
            var result = await Task.Run(() =>
                SmbSession.Instance.UploadFileAsync(remotePath, input, progress, token));
#if ANDROID
            Android.Util.Log.Info("FastSMB", $"Upload: done ok={result.ok} msg={result.message}");
#endif

            if (result.ok)
            {
                await Banner.ShowAsync($"{L10n.Instance["upload_done"]}{fileName}", durationMs: 1000);
                await LoadDirectoryAsync(); // 上传后刷新列表
            }
            else
            {
                await Banner.ShowAsync(result.message ?? L10n.Instance["unknown_err"], error: true, durationMs: 2500);
            }
        }
        catch (Exception ex)
        {
            await Banner.ShowAsync(L10n.Instance["upload_fail"] + ex.Message, error: true, durationMs: 2500);
        }
        finally
        {
            _busy = false;
            SetProgressVisible(false);
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _batchAbort = true;
        _cts?.Cancel();
    }

    private void SetProgressVisible(bool visible, string? text = null)
    {
        ProgressPanel.IsVisible = visible;
        if (text != null)
            ProgressLabel.Text = text;
        CancelBtn.IsVisible = visible;
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        var result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "video" : result;
    }
}
