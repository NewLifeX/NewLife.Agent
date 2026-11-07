#if !NET40
using NewLife.Log;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>操作系统账号认证器</summary>
/// <remarks>
/// 直接使用操作系统账号验证口令，不再保存任何面板专用口令。
/// 由于面板以系统管理员权限执行，只有管理员组内的账号才允许登录，
/// 否则任何普通账号都会变成提权到管理员/root 的通道。
/// </remarks>
public class OsAccountAuthenticator : IPanelAuthenticator
{
    #region 属性
    /// <summary>认证模式名称</summary>
    public String Name => "OsAccount";

    /// <summary>是否支持在面板内修改口令</summary>
    /// <remarks>系统账号口令不由面板维护</remarks>
    public Boolean CanChangePassword => false;

    /// <summary>默认管理员组。Windows 为内置管理员组，Linux 为 root/sudo/wheel</summary>
    public static String DefaultAdminGroups => Runtime.Windows ? "Administrators" : "root,sudo,wheel,admin";
    #endregion

    #region 方法
    /// <summary>验证用户凭据</summary>
    /// <param name="panel">所属面板</param>
    /// <param name="user">操作系统用户名</param>
    /// <param name="password">操作系统口令</param>
    /// <param name="remoteIp">客户端地址，仅用于审计</param>
    /// <returns>认证是否通过</returns>
    public Boolean Authenticate(AgentWebPanel panel, String user, String password, String remoteIp)
    {
        if (user.IsNullOrEmpty() || password.IsNullOrEmpty()) return false;

        // 用户名会作为 id 命令参数使用，严格限制字符集，避免命令参数注入
        if (!IsValidUserName(user))
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Rejected an OS sign-in attempt with an invalid user name from {0}.", remoteIp);
            return false;
        }

        if (!AuthenticateOs(user, password, out var isAdmin))
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "OS account {0} failed to authenticate from {1}.", user, remoteIp);
            return false;
        }

        if (!isAdmin)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "OS account {0} authenticated from {1} but is not a member of the administrator group.", user, remoteIp);
            return false;
        }

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "OS account {0} signed in from {1}.", user, remoteIp);

        return true;
    }

    /// <summary>校验用户名是否只包含安全字符</summary>
    /// <param name="user">用户名</param>
    /// <returns>是否合法</returns>
    public static Boolean IsValidUserName(String user)
    {
        if (user.IsNullOrEmpty() || user.Length > 64) return false;
        if (user[0] == '-') return false;

        foreach (var ch in user)
        {
            if (ch is >= 'a' and <= 'z') continue;
            if (ch is >= 'A' and <= 'Z') continue;
            if (ch is >= '0' and <= '9') continue;
            if (ch is '.' or '_' or '-' or '@' or '\\') continue;

            return false;
        }

        return true;
    }

    /// <summary>读取管理员组配置</summary>
    /// <returns>管理员组名称数组</returns>
    public static String[] GetAdminGroups()
    {
        var value = NewLife.Agent.Setting.Current.WebAdminGroups;
        if (value.IsNullOrWhiteSpace()) value = DefaultAdminGroups;

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => !e.IsNullOrEmpty())
            .ToArray();
    }

    /// <summary>按当前操作系统验证账号，并判断是否属于管理员组</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">口令</param>
    /// <param name="isAdmin">认证成功时，是否为管理员组成员</param>
    /// <returns>认证是否通过</returns>
    private static Boolean AuthenticateOs(String user, String password, out Boolean isAdmin)
    {
        isAdmin = false;

        if (Runtime.Windows)
        {
            if (!OsAccountNativeWindows.Logon(user, password, out var isAdministrator)) return false;

            isAdmin = isAdministrator;
            return true;
        }

        if (Runtime.Linux)
        {
            if (!OsAccountNativeLinux.Authenticate(user, password)) return false;

            isAdmin = OsAccountNativeLinux.IsInGroups(user, GetAdminGroups());
            return true;
        }

        // 其它平台暂不支持操作系统账号认证，失败关闭
        XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "OS account authentication is not supported on this platform; falling back to a failed sign-in.");
        return false;
    }
    #endregion
}
#endif
