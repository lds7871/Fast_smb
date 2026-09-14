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
            OnPropertyChanged(nameof(ShowArrow));
        }
    }

    /// <summary>是否处于删除选择模式（由页面切换时设置，与批量模式互斥）。</summary>
    private bool _isDeleteMode;
    public bool IsDeleteMode
    {
        get => _isDeleteMode;
        set
        {
            if (_isDeleteMode == value) return;
            _isDeleteMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(ShowCheckbox));
            OnPropertyChanged(nameof(ShowArrow));
        }
    }

    /// <summary>非批量/删除模式且为文件时显示「⬇ 下载」按钮。</summary>
    public bool ShowDownloadButton => ShowDownload && !IsBatchMode && !IsDeleteMode;

    /// <summary>选择模式下显示勾选框：批量模式仅文件，删除模式文件+文件夹。</summary>
    public bool ShowCheckbox => IsBatchMode ? ShowDownload : IsDeleteMode;

    /// <summary>文件夹进入箭头（删除模式下被勾选框替代）。</summary>
    public bool ShowArrow => IsDirectory && !IsDeleteMode;

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
