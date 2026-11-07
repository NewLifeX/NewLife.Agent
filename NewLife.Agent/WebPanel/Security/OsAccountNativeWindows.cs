#if !NET40
using System.Runtime.InteropServices;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>Windows 本机/域账号认证（P/Invoke）</summary>
/// <remarks>
/// 使用 <c>LogonUser</c> 的网络登录类型验证口令，不需要进程具备 TCB 特权；
/// 随后用 <c>CheckTokenMembership</c> 判断账号是否属于内置管理员组（S-1-5-32-544）。
/// 全部通过而是返回布尔值，不抛出异常，避免把账户是否存在等信息泄漏给调用方。
/// </remarks>
internal static class OsAccountNativeWindows
{
    #region 常量
    private const Int32 LOGON32_LOGON_NETWORK = 3;
    private const Int32 LOGON32_PROVIDER_DEFAULT = 0;
    private const Int32 WinBuiltinAdministratorsSid = 26;
    #endregion

    #region 外部方法
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern Boolean LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, Int32 dwLogonType, Int32 dwLogonProvider, out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean CheckTokenMembership(IntPtr tokenHandle, IntPtr sidToCheck, out Boolean isMember);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean CreateWellKnownSid(Int32 wellKnownSidType, IntPtr domainSid, IntPtr pSid, ref Int32 cbSid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Boolean CloseHandle(IntPtr handle);
    #endregion

    #region 方法
    /// <summary>验证 Windows 账号口令</summary>
    /// <param name="user">用户名，支持 <c>DOMAIN\user</c> 与 <c>user@domain</c> 两种写法</param>
    /// <param name="password">口令</param>
    /// <param name="isAdmin">验证成功时，是否为内置管理员组成员</param>
    /// <returns>口令是否有效</returns>
    public static Boolean Logon(String user, String password, out Boolean isAdmin)
    {
        isAdmin = false;

        var name = user;
        String domain = null;
        var index = user.IndexOf('\\');
        if (index >= 0)
        {
            domain = user[..index];
            name = user[(index + 1)..];
        }
        else if (user.IndexOf('@') >= 0)
        {
            // UPN 形式由系统自行解析域
            domain = null;
        }
        else
        {
            domain = Environment.MachineName;
        }

        if (!LogonUser(name, domain, password, LOGON32_LOGON_NETWORK, LOGON32_PROVIDER_DEFAULT, out var token)) return false;

        try
        {
            isAdmin = IsAdministrator(token);

            return true;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    /// <summary>判断令牌是否属于内置管理员组</summary>
    /// <param name="token">登录令牌</param>
    /// <returns>是否管理员</returns>
    private static Boolean IsAdministrator(IntPtr token)
    {
        var size = 0;
        CreateWellKnownSid(WinBuiltinAdministratorsSid, IntPtr.Zero, IntPtr.Zero, ref size);
        if (size <= 0) return false;

        var sid = Marshal.AllocHGlobal(size);
        try
        {
            if (!CreateWellKnownSid(WinBuiltinAdministratorsSid, IntPtr.Zero, sid, ref size)) return false;

            return CheckTokenMembership(token, sid, out var isMember) && isMember;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(sid);
        }
    }
    #endregion
}
#endif
