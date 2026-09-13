namespace Fast_smb.Services;

/// <summary>
/// 把下载的文件保存到用户可见的位置：
/// Android 10+ 用 MediaStore 写入公共「下载」目录（无需存储权限）；
/// 旧版 Android 直接写公共下载目录；其他平台写应用数据目录。
/// </summary>
public static class DownloadSaver
{
    /// <summary>创建用于写入下载内容的输出流。</summary>
    public static Stream? CreateDownloadStream(string fileName)
    {
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
        {
#pragma warning disable CA1416 // 运行时已检查 SdkInt >= 29，此处 API 仅在 29+ 使用
            var context = Android.App.Application.Context;
            var resolver = context.ContentResolver;
            if (resolver == null)
                return null;
            var values = new Android.Content.ContentValues();
            values.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, "application/octet-stream");
            values.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);

            var uri = resolver.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, values);
            if (uri == null)
                return null;
            var stream = resolver.OpenOutputStream(uri);
            if (stream == null)
                return null;
            // 记住 Uri，便于在文件管理器中定位
            LastSavedUri = uri.ToString();
            return stream;
#pragma warning restore CA1416
        }

        var dir = Android.OS.Environment.GetExternalStoragePublicDirectory(
            Android.OS.Environment.DirectoryDownloads);
        dir?.Mkdirs();
        if (dir == null)
            return null;
        var path = System.IO.Path.Combine(dir.AbsolutePath, Sanitize(fileName));
        return new FileStream(path, FileMode.Create, FileAccess.Write);
#else
        var dir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "Downloads");
        Directory.CreateDirectory(dir);
        return new FileStream(System.IO.Path.Combine(dir, Sanitize(fileName)), FileMode.Create, FileAccess.Write);
#endif
    }

    /// <summary>最近一次保存的 MediaStore Uri（Android 10+）。</summary>
    public static string? LastSavedUri { get; private set; }

    private static string Sanitize(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        var result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "download" : result;
    }
}
