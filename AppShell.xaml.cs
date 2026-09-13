using Fast_smb.Pages;

namespace Fast_smb;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("browser", typeof(FileBrowserPage));
	}
}
