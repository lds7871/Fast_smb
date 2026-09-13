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
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}