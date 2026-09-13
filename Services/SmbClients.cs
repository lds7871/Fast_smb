using System.Net;
using SMBLibrary;
using SMBLibrary.Client;

namespace Fast_smb.Services;

/// <summary>
/// 支持自定义端口的 SMB2 客户端。
/// SMBLibrary 公开的 Connect 只使用默认端口（445/139），
/// 这里通过子类暴露其 protected internal 的带端口重载。
/// </summary>
public sealed class Smb2ClientEx : SMB2Client
{
    public bool Connect(string host, int port)
    {
        var ip = ResolveIPv4(host);
        return ip != null && Connect(ip, SMBTransportType.DirectTCPTransport, port);
    }

    private static IPAddress? ResolveIPv4(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
                return null;
            foreach (var a in addresses)
                if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return a;
            return addresses[0];
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>支持自定义端口的 SMB1 客户端（老设备回退用）。</summary>
public sealed class Smb1ClientEx : SMB1Client
{
    public bool Connect(string host, int port)
    {
        var ip = ResolveIPv4(host);
        return ip != null && Connect(ip, SMBTransportType.DirectTCPTransport, port, true);
    }

    private static IPAddress? ResolveIPv4(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
                return null;
            foreach (var a in addresses)
                if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return a;
            return addresses[0];
        }
        catch
        {
            return null;
        }
    }
}
