using Fast_smb.Models;

namespace Fast_smb.Services;

/// <summary>目录条目排序方式。</summary>
public enum SortMode
{
    /// <summary>日期（新）：修改时间从新到旧。</summary>
    DateNew,
    /// <summary>日期（旧）：修改时间从旧到新。</summary>
    DateOld,
    /// <summary>名称（A）：名称升序。</summary>
    NameAsc,
    /// <summary>名称（Z）：名称降序。</summary>
    NameDesc,
}

/// <summary>
/// 排序设置：Preferences 持久化 + 统一排序实现。
/// 约定：**文件夹始终排在文件之前**，只对同类条目按所选方式比较。
/// </summary>
public static class SortSettings
{
    private const string PrefKey = "sort_mode";

    /// <summary>当前排序方式（默认名称 A→Z），读写 Preferences。</summary>
    public static SortMode Current
    {
        get => Preferences.Default.Get(PrefKey, nameof(SortMode.NameAsc)) switch
        {
            nameof(SortMode.DateNew) => SortMode.DateNew,
            nameof(SortMode.DateOld) => SortMode.DateOld,
            nameof(SortMode.NameDesc) => SortMode.NameDesc,
            _ => SortMode.NameAsc,
        };
        set => Preferences.Default.Set(PrefKey, value.ToString());
    }

    /// <summary>按当前排序方式就地排序。</summary>
    public static void Apply(List<SmbEntry> entries) => Apply(entries, Current);

    /// <summary>按指定方式就地排序（文件夹始终优先）。</summary>
    public static void Apply(List<SmbEntry> entries, SortMode mode)
    {
        entries.Sort((a, b) =>
        {
            // 文件夹始终优先，只在同类内部按所选方式比较
            int byType = b.IsDirectory.CompareTo(a.IsDirectory);
            if (byType != 0)
                return byType;

            return mode switch
            {
                SortMode.DateNew => b.LastWriteTime.CompareTo(a.LastWriteTime),
                SortMode.DateOld => a.LastWriteTime.CompareTo(b.LastWriteTime),
                SortMode.NameDesc => string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase),
                _ => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            };
        });
    }

    /// <summary>该排序方式对应的本地化文案 key（弹窗按钮用）。</summary>
    public static string TextKey(SortMode mode) => mode switch
    {
        SortMode.DateNew => "sort_date_new",
        SortMode.DateOld => "sort_date_old",
        SortMode.NameDesc => "sort_name_desc",
        _ => "sort_name_asc",
    };

    /// <summary>四种排序方式（弹窗按此顺序展示）。</summary>
    public static readonly SortMode[] All =
    {
        SortMode.DateNew, SortMode.DateOld, SortMode.NameAsc, SortMode.NameDesc,
    };
}
