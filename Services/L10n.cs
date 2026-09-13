using System.ComponentModel;

namespace Fast_smb.Services;

/// <summary>
/// 简单本地化管理器：中/英两套字符串字典，切换时通过 INPC 刷新所有绑定。
/// XAML 用法：Text="{Binding [key], Source={x:Static l10n:L10n.Instance}}"
/// 代码用法：L10n.Instance["key"]
/// </summary>
public class L10n : INotifyPropertyChanged
{
    public static L10n Instance { get; } = new();

    public bool IsEnglish { get; private set; }

    private L10n() { }

    /// <summary>启动时读取持久化的语言偏好（默认中文）。</summary>
    public void Load()
    {
        IsEnglish = Preferences.Default.Get("lang", "zh") == "en";
    }

    /// <summary>切换语言并持久化。</summary>
    public void SetLanguage(bool english)
    {
        if (IsEnglish == english)
            return;
        IsEnglish = english;
        Preferences.Default.Set("lang", english ? "en" : "zh");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public string this[string key]
        => (IsEnglish ? En : Zh).TryGetValue(key, out var v) ? v : key;

    public event PropertyChangedEventHandler? PropertyChanged;

    // ============ 中文（默认） ============
    private static readonly Dictionary<string, string> Zh = new()
    {
        ["connect_title"] = "连接",
        ["browser_title"] = "文件",
        ["connect_subtitle"] = "SMB 连接与文件管理",
        ["server_label"] = "服务器 IP / 主机名",
        ["server_placeholder"] = "例如 192.168.1.100（可带 :端口 或 /共享名）",
        ["share_label"] = "共享名（可选，留空自动检测）",
        ["share_placeholder"] = "例如 WD_DATA",
        ["user_label"] = "用户名（可留空）",
        ["user_placeholder"] = "用户名",
        ["pass_label"] = "密码",
        ["pass_placeholder"] = "密码",
        ["remember"] = "记住输入",
        ["connect_btn"] = "连 接",
        ["help_btn"] = "帮助",
        ["lang_zh"] = "中",
        ["lang_en"] = "EN",
        ["notes_title"] = "连接注意事项",
        ["notes_gotit"] = "知道了",
        ["notes_text"] =
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
            "• 返回主界面会自动断开连接",
        ["err_host_empty"] = "请输入服务器 IP 或主机名",
        ["err_port"] = "端口格式不正确，示例：192.168.1.100:4450",
        ["err_connect"] = "连接失败",
        ["err_open_share"] = "打开共享失败",
        ["open_share_fail"] = "打开共享失败：",
        ["connect_err"] = "连接出错：",
        ["err_no_shares"] = "服务器上没有可用的共享",
        ["share_picker_title"] = "选择共享",
        ["share_picker_cancel"] = "取消",
        ["toolbar_up"] = "上级",
        ["toolbar_refresh"] = "刷新",
        ["toolbar_search"] = "搜索",
        ["toolbar_batch"] = "批量",
        ["toolbar_done"] = "完成",
        ["search_placeholder"] = "搜索当前目录文件名…",
        ["cancel"] = "取消",
        ["dir_empty"] = "此目录为空",
        ["loading_dir"] = "加载中…",
        ["no_match"] = "没有匹配的文件",
        ["download_btn"] = "⬇ 下载",
        ["download_selected"] = "下载所选",
        ["select_all"] = "全选",
        ["invert"] = "反选",
        ["select_none"] = "全不选",
        ["cancel_batch"] = "取消批量",
        ["preview_title"] = "预览",
        ["load_dir_fail"] = "无法读取目录：",
        ["unknown_err"] = "未知错误",
        ["no_preview"] = "该类型暂不支持预览，请点右侧 ⬇ 下载",
        ["loading_preview"] = "正在加载预览",
        ["loading"] = "正在加载",
        ["downloading"] = "正在下载",
        ["preview_fail"] = "预览失败：",
        ["load_fail"] = "加载失败：",
        ["download_fail"] = "下载失败：",
        ["download_failed"] = "下载失败",
        ["download_done"] = "✓ 下载完成：",
        ["downloaded"] = "下载完成",
        ["select_first"] = "请先选择要下载的文件",
        ["batch_cancelled"] = "已取消，完成",
        ["batch_complete"] = "批量下载完成",
        ["cancelled"] = "已取消",
        ["create_file_fail"] = "无法创建本地文件（存储不可用）",
        ["not_connected_server"] = "尚未连接服务器",
        ["not_opened_share"] = "尚未打开共享",
        ["enum_shares_fail"] = "枚举共享失败：",
        ["open_dir_fail"] = "打开目录失败：",
        ["open_file_fail"] = "打开文件失败：",
        ["read_file_fail"] = "读取文件失败：",
        ["read_err"] = "读取出错：",
        ["download_err"] = "下载出错：",
        ["login_fail"] = "登录失败：",
        ["login_fail_smb1"] = "登录失败（SMB1）：",
        ["cant_connect"] = "无法连接服务器（请检查 IP、网络与端口）",
        ["st_bad_cred"] = "用户名或密码错误",
        ["st_invalid_smb"] = "服务器拒绝登录，请检查用户名或密码",
        ["st_denied"] = "拒绝访问（无权限）",
        ["st_no_path"] = "路径不存在",
        ["st_no_share"] = "共享名不存在",
        ["st_timeout"] = "网络超时",
    };

    // ============ 英文 ============
    private static readonly Dictionary<string, string> En = new()
    {
        ["connect_title"] = "Connect",
        ["browser_title"] = "Files",
        ["connect_subtitle"] = "SMB Connect & File Manager",
        ["server_label"] = "Server IP / Host",
        ["server_placeholder"] = "e.g. 192.168.1.100 (with :port or /share)",
        ["share_label"] = "Share (optional, auto-detect if empty)",
        ["share_placeholder"] = "e.g. WD_DATA",
        ["user_label"] = "Username (optional)",
        ["user_placeholder"] = "Username",
        ["pass_label"] = "Password",
        ["pass_placeholder"] = "Password",
        ["remember"] = "Remember input",
        ["connect_btn"] = "Connect",
        ["help_btn"] = "Help",
        ["lang_zh"] = "中",
        ["lang_en"] = "EN",
        ["notes_title"] = "Connection Notes",
        ["notes_gotit"] = "Got it",
        ["notes_text"] =
            "[Before connecting]\n" +
            "• Server address: LAN IP of your NAS / PC, e.g. 192.168.1.100\n" +
            "• May include port or share: IP:4450 or IP/WD_DATA\n" +
            "• Share may be left empty to auto-detect; if enumeration is not supported, type it manually\n" +
            "• Username / password: the SMB account on the NAS; leave empty for anonymous access\n" +
            "• Phone and server must be on the same LAN, and SMB (port 445) must be enabled\n" +
            "• Can't connect? Check the IP, confirm 445 is open, verify share permissions\n" +
            "• Very old servers may only support SMB1; the app falls back automatically\n\n" +
            "[After connecting]\n" +
            "• Tap a folder to enter, tap a file to preview (text / image)\n" +
            "• Video and audio open with the system player / picker\n" +
            "• Tap the ⬇ button for a single download; use Batch mode for multiple files\n" +
            "• A top banner shows download results; files are saved to the Downloads folder\n" +
            "• Use Search on the toolbar to filter file names\n" +
            "• Returning to the main page disconnects automatically",
        ["err_host_empty"] = "Please enter the server IP or hostname",
        ["err_port"] = "Invalid port format, e.g. 192.168.1.100:4450",
        ["err_connect"] = "Connection failed",
        ["err_open_share"] = "Failed to open share",
        ["open_share_fail"] = "Failed to open share: ",
        ["connect_err"] = "Connection error: ",
        ["err_no_shares"] = "No shares available on the server",
        ["share_picker_title"] = "Select share",
        ["share_picker_cancel"] = "Cancel",
        ["toolbar_up"] = "Up",
        ["toolbar_refresh"] = "Refresh",
        ["toolbar_search"] = "Search",
        ["toolbar_batch"] = "Batch",
        ["toolbar_done"] = "Done",
        ["search_placeholder"] = "Search file names in this folder…",
        ["cancel"] = "Cancel",
        ["dir_empty"] = "This folder is empty",
        ["loading_dir"] = "Loading…",
        ["no_match"] = "No matching files",
        ["download_btn"] = "⬇ Download",
        ["download_selected"] = "Download selected",
        ["select_all"] = "Select all",
        ["invert"] = "Invert",
        ["select_none"] = "Deselect all",
        ["cancel_batch"] = "Cancel batch",
        ["preview_title"] = "Preview",
        ["load_dir_fail"] = "Cannot read folder: ",
        ["unknown_err"] = "Unknown error",
        ["no_preview"] = "Preview not supported, tap ⬇ to download",
        ["loading_preview"] = "Loading preview",
        ["loading"] = "Loading",
        ["downloading"] = "Downloading",
        ["preview_fail"] = "Preview failed: ",
        ["load_fail"] = "Load failed: ",
        ["download_fail"] = "Download failed: ",
        ["download_failed"] = "Download failed",
        ["download_done"] = "✓ Downloaded: ",
        ["downloaded"] = "Downloaded",
        ["select_first"] = "Please select files to download first",
        ["batch_cancelled"] = "Cancelled, done",
        ["batch_complete"] = "Batch download complete",
        ["cancelled"] = "Cancelled",
        ["create_file_fail"] = "Cannot create local file (storage unavailable)",
        ["not_connected_server"] = "Not connected to a server",
        ["not_opened_share"] = "No share opened",
        ["enum_shares_fail"] = "Failed to enumerate shares: ",
        ["open_dir_fail"] = "Failed to open folder: ",
        ["open_file_fail"] = "Failed to open file: ",
        ["read_file_fail"] = "Failed to read file: ",
        ["read_err"] = "Read error: ",
        ["download_err"] = "Download error: ",
        ["login_fail"] = "Login failed: ",
        ["login_fail_smb1"] = "Login failed (SMB1): ",
        ["cant_connect"] = "Cannot connect to server (check IP, network and port)",
        ["st_bad_cred"] = "Incorrect username or password",
        ["st_invalid_smb"] = "Server rejected the login, check username or password",
        ["st_denied"] = "Access denied (no permission)",
        ["st_no_path"] = "Path not found",
        ["st_no_share"] = "Share not found",
        ["st_timeout"] = "Network timeout",
    };
}
