using Microsoft.Extensions.DependencyInjection;

namespace Fast_smb;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// 强制使用暗色主题（无论系统设置如何）
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}