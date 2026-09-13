using Fast_smb.Models;
using SMBLibrary;
using SMBLibrary.Client;

namespace Fast_smb.Services;

/// <summary>
/// SMB 连接会话封装（单例）。支持 SMB2/3，失败时回退 SMB1。
/// </summary>
public class SmbSession : IDisposable
{
    public static SmbSession Instance { get; } = new();

    private ISMBClient? _client;
    private object? _fileStore; // SMB2FileStore 或 SMB1FileStore
    private string _share = "";

    public string Host { get; private set; } = "";
    public string UserName { get; private set; } = "";
    public string Share => _share;
    public bool IsSMB1 { get; private set; }

    public bool IsConnected => _client is { IsConnected: true } && _fileStore != null;

    private SmbSession() { }

    /// <summary>连接服务器并登录（登录成功即视为连接建立，共享在打开共享时选定）。</summary>
    public (bool ok, string? message) Connect(string host, string username, string password, int port = 445)
    {
        Disconnect();
        Host = "";
        UserName = "";

        if (string.IsNullOrWhiteSpace(host))
            return (false, L10n.Instance["err_host_empty"]);

        // 优先 SMB2/3
        var smb2 = new Smb2ClientEx();
        try
        {
            if (smb2.Connect(host, port))
            {
                var st = smb2.Login("", username, password);
                if (st == NTStatus.STATUS_SUCCESS)
                {
                    _client = smb2;
                    IsSMB1 = false;
                    Host = host;
                    UserName = username;
                    return (true, null);
                }
                smb2.Disconnect();
                return (false, L10n.Instance["login_fail"] + DescribeStatus(st));
            }
            smb2.Disconnect();
        }
        catch (Exception ex)
        {
            smb2.Disconnect();
            return (false, L10n.Instance["connect_err"] + ex.Message);
        }

        // SMB2 连接失败，回退 SMB1（老设备/NAS）
        var smb1 = new Smb1ClientEx();
        try
        {
            if (smb1.Connect(host, port))
            {
                var st = smb1.Login("", username, password);
                if (st == NTStatus.STATUS_SUCCESS)
                {
                    _client = smb1;
                    IsSMB1 = true;
                    Host = host;
                    UserName = username;
                    return (true, null);
                }
                smb1.Disconnect();
                return (false, L10n.Instance["login_fail_smb1"] + DescribeStatus(st));
            }
            smb1.Disconnect();
            return (false, L10n.Instance["cant_connect"]);
        }
        catch (Exception ex)
        {
            smb1.Disconnect();
            return (false, L10n.Instance["connect_err"] + ex.Message);
        }
    }

    /// <summary>列出服务器上的共享（过滤 IPC$/结尾带 $ 的隐藏共享）。</summary>
    public (List<string>? shares, string? message) ListShares()
    {
        if (_client == null || !_client.IsConnected)
            return (null, L10n.Instance["not_connected_server"]);

        var shares = _client.ListShares(out var status);
        if (status != NTStatus.STATUS_SUCCESS)
            return (null, L10n.Instance["enum_shares_fail"] + DescribeStatus(status));

        var list = shares
            .Where(s => !s.EndsWith("$", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return (list, null);
    }

    /// <summary>打开指定共享，之后才能浏览/下载。</summary>
    public (bool ok, string? message) OpenShare(string share)
    {
        if (_client == null || !_client.IsConnected)
            return (false, L10n.Instance["not_connected_server"]);

        var store = _client.TreeConnect(share, out var status);
        if (status != NTStatus.STATUS_SUCCESS)
            return (false, L10n.Instance["open_share_fail"] + DescribeStatus(status));

        _fileStore = store;
        _share = share;
        return (true, null);
    }

    /// <summary>列出共享内某目录（path 用 "/" 分隔，空串表示共享根目录）。</summary>
    public (List<SmbEntry>? entries, string? message) ListDirectory(string path)
    {
        if (_fileStore == null)
            return (null, L10n.Instance["not_opened_share"]);

        string smbPath = ToSmbPath(path);
        object? handle = null;
        try
        {
            var st = CreateFile(out handle, smbPath, isDirectory: true);
            if (st != NTStatus.STATUS_SUCCESS)
                return (null, L10n.Instance["open_dir_fail"] + DescribeStatus(st));

            var list = new List<SmbEntry>();
            while (true)
            {
                st = QueryDirectory(handle, out var results);
                // 注意：SMB2 在返回最后一批条目时会同时给出 STATUS_NO_MORE_FILES，
                // 必须先处理结果，再判断是否继续
                foreach (var r in results)
                {
                    if (r is not FileDirectoryInformation fdi)
                        continue;
                    if (fdi.FileName == "." || fdi.FileName == "..")
                        continue;
                    list.Add(new SmbEntry
                    {
                        Name = fdi.FileName,
                        IsDirectory = fdi.FileAttributes.HasFlag(SMBLibrary.FileAttributes.Directory),
                        Size = fdi.EndOfFile,
                        LastWriteTime = fdi.LastWriteTime,
                    });
                }
                if (st != NTStatus.STATUS_SUCCESS || results.Count == 0)
                    break;
            }

            list.Sort((a, b) =>
            {
                int c = b.IsDirectory.CompareTo(a.IsDirectory);
                return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return (list, null);
        }
        finally
        {
            if (handle != null)
                CloseFile(handle);
        }
    }

    /// <summary>
    /// 从共享下载文件到指定输出流（path 用 "/" 分隔的相对路径）。
    /// </summary>
    public async Task<(bool ok, string? message)> DownloadFileAsync(
        string path, Stream output, IProgress<double> progress, CancellationToken ct)
    {
        if (_fileStore == null)
            return (false, L10n.Instance["not_opened_share"]);

        string smbPath = ToSmbPath(path);
        object? handle = null;
        try
        {
            var st = CreateFile(out handle, smbPath, isDirectory: false);
            if (st != NTStatus.STATUS_SUCCESS)
                return (false, L10n.Instance["open_file_fail"] + DescribeStatus(st));

            long total = 0;
            st = GetFileSize(handle, out total);
            if (st != NTStatus.STATUS_SUCCESS || total <= 0)
                total = 1;

            const int chunk = 64 * 1024;
            long offset = 0;
            byte[]? data;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                st = ReadFile(handle, offset, chunk, out data);
                if (st == NTStatus.STATUS_END_OF_FILE)
                    break;
                if (st != NTStatus.STATUS_SUCCESS)
                    return (false, L10n.Instance["read_file_fail"] + DescribeStatus(st));
                if (data is null || data.Length == 0)
                    break;
                await output.WriteAsync(data.AsMemory(0, data.Length), ct);
                offset += data.Length;
                progress?.Report(offset / (double)total);
            }
            await output.FlushAsync(ct);
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            return (false, L10n.Instance["cancelled"]);
        }
        catch (Exception ex)
        {
            return (false, L10n.Instance["download_err"] + ex.Message);
        }
        finally
        {
            if (handle != null)
                CloseFile(handle);
        }
    }

    /// <summary>
    /// 读取文件前 maxBytes 字节到内存（用于预览），返回字节数组。
    /// </summary>
    public async Task<(bool ok, byte[]? data, string? message)> ReadBytesAsync(
        string path, int maxBytes, CancellationToken ct)
    {
        if (_fileStore == null)
            return (false, null, L10n.Instance["not_opened_share"]);

        string smbPath = ToSmbPath(path);
        object? handle = null;
        try
        {
            var st = CreateFile(out handle, smbPath, isDirectory: false);
            if (st != NTStatus.STATUS_SUCCESS)
                return (false, null, L10n.Instance["open_file_fail"] + DescribeStatus(st));

            using var ms = new MemoryStream();
            long offset = 0;
            while (ms.Length < maxBytes)
            {
                ct.ThrowIfCancellationRequested();
                int want = (int)Math.Min(maxBytes - ms.Length, 64 * 1024);
                st = ReadFile(handle, offset, want, out var data);
                if (st == NTStatus.STATUS_END_OF_FILE || data is null || data.Length == 0)
                    break;
                if (st != NTStatus.STATUS_SUCCESS)
                    return (false, null, L10n.Instance["read_file_fail"] + DescribeStatus(st));
                ms.Write(data, 0, data.Length);
                offset += data.Length;
            }
            return (true, ms.ToArray(), null);
        }
        catch (OperationCanceledException)
        {
            return (false, null, L10n.Instance["cancelled"]);
        }
        catch (Exception ex)
        {
            return (false, null, L10n.Instance["read_err"] + ex.Message);
        }
        finally
        {
            if (handle != null)
                CloseFile(handle);
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_fileStore != null)
            {
                if (_fileStore is ISMBFileStore fs)
                    fs.Disconnect();
                _fileStore = null;
            }
            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }
        }
        catch
        {
            // 忽略断开时的异常
        }
        _share = "";
        IsSMB1 = false;
    }

    public void Dispose() => Disconnect();

    // ---------- 平台无关的文件操作分发（SMB2/SMB1 签名一致） ----------

    private NTStatus CreateFile(out object handle, string smbPath, bool isDirectory)
    {
        handle = null!;
        if (_fileStore is SMB2FileStore s2)
        {
            FileStatus fs;
            var st = s2.CreateFile(out handle, out fs, smbPath,
                AccessMask.GENERIC_READ,
                isDirectory ? SMBLibrary.FileAttributes.Directory : SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                isDirectory ? CreateOptions.FILE_DIRECTORY_FILE : CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);
            return st;
        }
        if (_fileStore is SMB1FileStore s1)
        {
            FileStatus fs;
            var st = s1.CreateFile(out handle, out fs, smbPath,
                AccessMask.GENERIC_READ,
                isDirectory ? SMBLibrary.FileAttributes.Directory : SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                isDirectory ? CreateOptions.FILE_DIRECTORY_FILE : CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);
            return st;
        }
        return NTStatus.STATUS_INVALID_HANDLE;
    }

    private NTStatus QueryDirectory(object handle, out List<QueryDirectoryFileInformation> results)
    {
        results = new List<QueryDirectoryFileInformation>();
        if (_fileStore is SMB2FileStore s2)
            return s2.QueryDirectory(out results, handle, "*", FileInformationClass.FileDirectoryInformation);
        if (_fileStore is SMB1FileStore s1)
            return s1.QueryDirectory(out results, handle, "*", FileInformationClass.FileDirectoryInformation);
        return NTStatus.STATUS_INVALID_HANDLE;
    }

    private NTStatus ReadFile(object handle, long offset, int maxCount, out byte[]? data)
    {
        data = null;
        if (_fileStore is SMB2FileStore s2)
            return s2.ReadFile(out data, handle, offset, maxCount);
        if (_fileStore is SMB1FileStore s1)
            return s1.ReadFile(out data, handle, offset, maxCount);
        return NTStatus.STATUS_INVALID_HANDLE;
    }

    private NTStatus GetFileSize(object handle, out long size)
    {
        size = 0;
        if (_fileStore is SMB2FileStore s2)
        {
            var st = s2.GetFileInformation(out var info, handle, FileInformationClass.FileStandardInformation);
            if (st == NTStatus.STATUS_SUCCESS && info is FileStandardInformation fsi)
                size = fsi.EndOfFile;
            return st;
        }
        if (_fileStore is SMB1FileStore s1)
        {
            var st = s1.GetFileInformation(out var info, handle, FileInformationClass.FileStandardInformation);
            if (st == NTStatus.STATUS_SUCCESS && info is FileStandardInformation fsi)
                size = fsi.EndOfFile;
            return st;
        }
        return NTStatus.STATUS_INVALID_HANDLE;
    }

    private NTStatus CloseFile(object handle)
    {
        if (_fileStore is SMB2FileStore s2)
            return s2.CloseFile(handle);
        if (_fileStore is SMB1FileStore s1)
            return s1.CloseFile(handle);
        return NTStatus.STATUS_INVALID_HANDLE;
    }

    // ---------- 路径与状态 ----------

    private static string ToSmbPath(string path)
    {
        // SMB2/1 CREATE 的 Name 是相对共享的路径，不能带前导反斜杠
        // （真实 Samba 严格校验：根目录用 ""，子目录用 "dir\\sub"）
        if (string.IsNullOrEmpty(path))
            return "";
        return path.Replace("/", "\\").TrimStart('\\');
    }

    private static string DescribeStatus(NTStatus status) => status switch
    {
        NTStatus.STATUS_LOGON_FAILURE or NTStatus.STATUS_WRONG_PASSWORD
            => L10n.Instance["st_bad_cred"],
        NTStatus.STATUS_INVALID_SMB => L10n.Instance["st_invalid_smb"],
        NTStatus.STATUS_ACCESS_DENIED => L10n.Instance["st_denied"],
        NTStatus.STATUS_OBJECT_NAME_NOT_FOUND or NTStatus.STATUS_OBJECT_PATH_NOT_FOUND
            => L10n.Instance["st_no_path"],
        NTStatus.STATUS_BAD_NETWORK_NAME or NTStatus.STATUS_NETWORK_NAME_DELETED
            => L10n.Instance["st_no_share"],
        NTStatus.STATUS_IO_TIMEOUT => L10n.Instance["st_timeout"],
        _ => $"{status}",
    };
}
