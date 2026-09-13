using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Fast_smb.Models;

/// <summary>共享目录中的一个条目（文件夹或文件）。</summary>
public class SmbEntry : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime LastWriteTime { get; init; }

    public string Icon => IsDirectory ? "📁" : "📄";

    public string ActionText => IsDirectory ? "›" : "⬇ 下载";

    public bool ShowDownload => !IsDirectory;

    public string DisplayInfo => IsDirectory
        ? ""
        : $"{FormatSize(Size)} · {LastWriteTime:yyyy-MM-dd HH:mm}";

    /// <summary>是否处于批量选择模式（由页面切换时设置）。</summary>
    private bool _isBatchMode;
    public bool IsBatchMode
    {
        get => _isBatchMode;
        set
        {
            if (_isBatchMode == value) return;
            _isBatchMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(ShowCheckbox));
        }
    }

    /// <summary>非批量模式且为文件时显示「⬇ 下载」按钮。</summary>
    public bool ShowDownloadButton => ShowDownload && !IsBatchMode;

    /// <summary>批量模式且为文件时显示勾选框。</summary>
    public bool ShowCheckbox => ShowDownload && IsBatchMode;

    /// <summary>批量模式下是否被选中。</summary>
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int u = 0;
        while (size >= 1024 && u < units.Length - 1)
        {
            size /= 1024;
            u++;
        }
        return u == 0 ? $"{bytes} B" : $"{size:0.##} {units[u]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
