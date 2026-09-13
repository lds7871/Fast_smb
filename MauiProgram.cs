using System.Text;
using Microsoft.Extensions.Logging;

namespace Fast_smb;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// 支持 GBK 等代码页编码（预览中文文本文件时回退用）
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
