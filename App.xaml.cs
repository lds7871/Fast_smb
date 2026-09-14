using Fast_smb.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Fast_smb;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// 应用上次保存的主题偏好（默认暗色）
		UserAppTheme = Theme.Load();

		// 应用上次保存的语言偏好（默认中文）
		L10n.Instance.Load();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// 打开系统应用（文件选择器 / 视频播放器等）会把本程序切到后台，
		// 期间 SMB 连接可能被服务器回收。标记连接“可能失效”，
		// 下次任何 SMB 操作前都会强制重连，避免 “The client is no longer connected”。
		window.Deactivated += (_, _) => SmbSession.Instance.MarkConnectionStale();
		window.Stopped += (_, _) => SmbSession.Instance.MarkConnectionStale();

		return window;
	}
}