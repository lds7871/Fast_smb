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

        // 语言按钮：显示当前语言（中文“中” / 英文“EN”）
        LangBtn.Text = L10n.Instance.IsEnglish ? L10n.Instance["lang_en"] : L10n.Instance["lang_zh"];

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

    // ---------- 语言切换 ----------

    private void OnLangToggleClicked(object? sender, EventArgs e)
    {
        L10n.Instance.SetLanguage(!L10n.Instance.IsEnglish);
        LangBtn.Text = L10n.Instance.IsEnglish ? L10n.Instance["lang_en"] : L10n.Instance["lang_zh"];
    }

    private void OnRememberLabelTapped(object? sender, TappedEventArgs e)
    {
        RememberCheck.IsChecked = !RememberCheck.IsChecked;
    }

    // ---------- 连接注意事项浮层 ----------

    private bool _notesVisible;

    private async void OnHelpClicked(object? sender, EventArgs e)
    {
        if (_notesVisible)
            return;
        _notesVisible = true;

        NotesLabel.Text = L10n.Instance["notes_text"];
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
            ShowError(L10n.Instance["err_host_empty"]);
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
                ShowError(L10n.Instance["err_port"]);
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
                ShowError(result.message ?? L10n.Instance["err_connect"]);
                return;
            }

            // 指定了共享名：直接打开，跳过枚举
            if (!string.IsNullOrWhiteSpace(share))
            {
                var openResult = await Task.Run(() => SmbSession.Instance.OpenShare(share));
                if (!openResult.ok)
                {
                    ShowError(openResult.message ?? L10n.Instance["err_open_share"]);
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
                ShowError(sharesResult.message ?? L10n.Instance["err_no_shares"]);
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
                ShowError(open2Result.message ?? L10n.Instance["err_open_share"]);
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
