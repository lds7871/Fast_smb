using Fast_smb.Services;

namespace Fast_smb.Pages;

public partial class ConnectPage : ContentPage
{
    private static string _lastHost = "";
    private static string _lastUser = "";
    private static string _lastPass = "";
    private static string _lastShare = "";

    public ConnectPage()
    {
        InitializeComponent();

        // 读取「记住输入」的已存内容
        if (Preferences.Default.ContainsKey("remember_host"))
        {
            _lastHost = Preferences.Default.Get("remember_host", "");
            _lastUser = Preferences.Default.Get("remember_user", "");
            _lastPass = Preferences.Default.Get("remember_pass", "");
            _lastShare = Preferences.Default.Get("remember_share", "");
            RememberCheck.IsChecked = true;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        HostEntry.Text = _lastHost;
        ShareEntry.Text = _lastShare;
        UserEntry.Text = _lastUser;
        PassEntry.Text = _lastPass;

        // 主题按钮图标：当前暗色显示 🌙，亮色显示 ☀️
        ThemeBtn.Text = Application.Current?.UserAppTheme == AppTheme.Light ? "☀️" : "🌙";

        // 退出到主界面默认断开（从文件浏览器返回时）
        if (SmbSession.Instance.IsConnected)
            SmbSession.Instance.Disconnect();
    }

    // ---------- 主题切换 ----------

    private void OnThemeToggleClicked(object? sender, EventArgs e)
    {
        var next = Theme.Toggle();
        ThemeBtn.Text = next == AppTheme.Light ? "☀️" : "🌙";
    }

    private void OnRememberLabelTapped(object? sender, TappedEventArgs e)
    {
        RememberCheck.IsChecked = !RememberCheck.IsChecked;
    }

    // ---------- 连接注意事项浮层 ----------

    private const string NotesText =
        "【连接前】\n" +
        "• 服务器地址：填 NAS / 电脑的局域网 IP，例如 192.168.1.100\n" +
        "• 可带端口或共享名：IP:4450 或 IP/WD_DATA\n" +
        "• 共享名可留空自动检测；若服务器不支持枚举，建议手动填写\n" +
        "• 用户名 / 密码：NAS 的 SMB 账号，留空表示匿名访问\n" +
        "• 手机与服务器需在同一局域网，且服务器已开启 SMB（445 端口）\n" +
        "• 连不上时：核对 IP、确认 445 开放、检查账号共享权限\n" +
        "• 老设备服务器可能仅支持 SMB1，应用会自动回退尝试\n\n" +
        "【连接后】\n" +
        "• 点文件夹进入，点文件预览（文本 / 图片）\n" +
        "• 视频、音频会自动调起系统播放器 / 打开方式\n" +
        "• 点文件右侧「⬇ 下载」单个下载；顶栏「批量」可多选批量下载\n" +
        "• 下载完成顶部横幅提示，文件保存到手机「下载」目录\n" +
        "• 顶栏「搜索」按文件名过滤当前目录\n" +
        "• 返回主界面会自动断开连接";

    private bool _notesVisible;

    private async void OnHelpClicked(object? sender, EventArgs e)
    {
        if (_notesVisible)
            return;
        _notesVisible = true;

        NotesLabel.Text = NotesText;
        NotesScrim.IsVisible = true;
        NotesCard.IsVisible = true;
        NotesScrim.Opacity = 0;
        NotesCard.Opacity = 0;
        NotesCard.Scale = 0.94;

        await Task.WhenAll(
            NotesScrim.FadeToAsync(1, 130),
            NotesCard.FadeToAsync(1, 130),
            NotesCard.ScaleToAsync(1, 140, Easing.CubicOut));
    }

    private async void OnNotesCloseClicked(object? sender, EventArgs e)
    {
        if (!_notesVisible)
            return;
        _notesVisible = false;

        await Task.WhenAll(
            NotesScrim.FadeToAsync(0, 110),
            NotesCard.FadeToAsync(0, 110),
            NotesCard.ScaleToAsync(0.95, 110, Easing.CubicIn));

        NotesScrim.IsVisible = false;
        NotesCard.IsVisible = false;
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        var hostInput = HostEntry.Text?.Trim() ?? "";
        var shareInput = ShareEntry.Text?.Trim() ?? "";
        var user = UserEntry.Text?.Trim() ?? "";
        var pass = PassEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(hostInput))
        {
            ShowError("请输入服务器 IP 或主机名");
            return;
        }

        // 解析主机栏：支持 "IP" | "IP:端口" | "IP/共享名" | "IP:端口/共享名"
        var host = hostInput;
        var port = 445;
        var shareFromHost = "";

        var slashIdx = hostInput.IndexOf('/');
        if (slashIdx >= 0)
        {
            shareFromHost = hostInput[(slashIdx + 1)..].Trim();
            host = hostInput[..slashIdx].Trim();
        }

        if (host.Contains(':'))
        {
            var parts = host.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var p) && p is > 0 and < 65536)
            {
                host = parts[0].Trim();
                port = p;
            }
            else
            {
                ShowError("端口格式不正确，示例：192.168.1.100:4450");
                return;
            }
        }

        // 共享名优先级：主机栏里的 "/共享名" > 共享名输入框
        var share = shareFromHost.Length > 0 ? shareFromHost : shareInput;

        // 记住输入
        SaveRemember(hostInput, shareInput, user, pass);

        _lastHost = hostInput;
        _lastUser = user;
        _lastPass = pass;
        _lastShare = shareInput;
        SetBusy(true);

        try
        {
            var result = await Task.Run(() => SmbSession.Instance.Connect(host, user, pass, port));
            if (!result.ok)
            {
                ShowError(result.message ?? "连接失败");
                return;
            }

            // 指定了共享名：直接打开，跳过枚举
            if (!string.IsNullOrWhiteSpace(share))
            {
                var openResult = await Task.Run(() => SmbSession.Instance.OpenShare(share));
                if (!openResult.ok)
                {
                    ShowError(openResult.message ?? "打开共享失败");
                    SmbSession.Instance.Disconnect();
                    return;
                }
                PassEntry.Text = "";
                StatusLabel.IsVisible = false;
                await Shell.Current.GoToAsync("browser");
                return;
            }

            // 未指定共享名：枚举后选择（应用内弹窗）
            var sharesResult = await Task.Run(() => SmbSession.Instance.ListShares());
            if (sharesResult.shares is not { Count: > 0 })
            {
                ShowError(sharesResult.message ?? "服务器上没有可用的共享");
                SmbSession.Instance.Disconnect();
                return;
            }

            string pickedShare;
            if (sharesResult.shares.Count == 1)
            {
                pickedShare = sharesResult.shares[0];
            }
            else
            {
                pickedShare = await SharePicker.ShowAsync(sharesResult.shares) ?? "";
                if (pickedShare.Length == 0)
                {
                    SmbSession.Instance.Disconnect();
                    SetBusy(false);
                    return;
                }
            }

            var open2Result = await Task.Run(() => SmbSession.Instance.OpenShare(pickedShare));
            if (!open2Result.ok)
            {
                ShowError(open2Result.message ?? "打开共享失败");
                SmbSession.Instance.Disconnect();
                return;
            }

            PassEntry.Text = "";
            StatusLabel.IsVisible = false;
            await Shell.Current.GoToAsync("browser");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveRemember(string host, string share, string user, string pass)
    {
        if (RememberCheck.IsChecked)
        {
            Preferences.Default.Set("remember_host", host);
            Preferences.Default.Set("remember_user", user);
            Preferences.Default.Set("remember_pass", pass);
            Preferences.Default.Set("remember_share", share);
        }
        else
        {
            Preferences.Default.Remove("remember_host");
            Preferences.Default.Remove("remember_user");
            Preferences.Default.Remove("remember_pass");
            Preferences.Default.Remove("remember_share");
        }
    }

    private void ShowError(string message)
    {
        // 颜色由 XAML 按主题绑定（Error 色），这里只设置文本与可见性
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        ConnectBtn.IsEnabled = !busy;
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
    }
}
