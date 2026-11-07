#if !NET40
using System.Diagnostics;
using System.Runtime.InteropServices;
using NewLife.Log;

namespace NewLife.Agent.WebPanel.Security;

/// <summary>Linux 系统账号认证（PAM P/Invoke）与用户组查询</summary>
/// <remarks>
/// 认证走 PAM，可复用系统既有的口令策略、账号过期与锁定规则；
/// 只支持单段口令提示，若 PAM 模块要求动态口令等第二段输入会直接失败（宁拒绝不放行）。
/// 组归属通过系统 <c>id -Gn</c> 查询，用户名已在上层做字符集校验，命令参数不可注入。
/// </remarks>
internal static class OsAccountNativeLinux
{
    #region 常量
    private const String PamLibrary = "libpam.so.0";

    private const Int32 PAM_SUCCESS = 0;
    private const Int32 PAM_AUTH_ERR = 7;
    private const Int32 PAM_NEW_AUTHTOK_REQD = 12;
    private const Int32 PAM_CONV_ERR = 19;
    private const Int32 PAM_PROMPT_ECHO_OFF = 1;

    /// <summary>PAM 服务名候选。不同发行版默认口令栈文件不同，逐个尝试</summary>
    private static readonly String[] _services = ["system-auth", "common-auth", "login"];
    #endregion

    #region 结构
    [StructLayout(LayoutKind.Sequential)]
    private struct PamMessage
    {
        public Int32 MsgStyle;
        public IntPtr Msg;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PamResponse
    {
        public IntPtr Resp;
        public Int32 RespRetCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PamConv
    {
        public IntPtr Conv;
        public IntPtr AppDataPtr;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate Int32 PamConvCallback(Int32 numMsg, IntPtr msg, IntPtr resp, IntPtr appData);
    #endregion

    #region 外部方法
    [DllImport(PamLibrary, CharSet = CharSet.Ansi)]
    private static extern Int32 pam_start(String service, String user, ref PamConv conv, out IntPtr handle);

    [DllImport(PamLibrary)]
    private static extern Int32 pam_authenticate(IntPtr handle, Int32 flags);

    [DllImport(PamLibrary)]
    private static extern Int32 pam_acct_mgmt(IntPtr handle, Int32 flags);

    [DllImport(PamLibrary)]
    private static extern Int32 pam_end(IntPtr handle, Int32 status);
    #endregion

    #region 会话回调
    /// <summary>回调委托必须保持强引用，避免被 GC 回收后 PAM 调用野指针</summary>
    private static readonly PamConvCallback _callback = OnConversation;

    /// <summary>当前线程已分配的口令应答，pam_end 之后统一释放</summary>
    [ThreadStatic]
    private static List<IntPtr> _responses;

    private static Int32 OnConversation(Int32 numMsg, IntPtr msg, IntPtr resp, IntPtr appData)
    {
        if (resp == IntPtr.Zero || numMsg <= 0) return PAM_CONV_ERR;

        var password = appData == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(appData);
        var answered = false;
#if NET5_0_OR_GREATER
        var msgSize = Marshal.SizeOf<PamMessage>();
        var respSize = Marshal.SizeOf<PamResponse>();
#else
        var msgSize = Marshal.SizeOf(typeof(PamMessage));
        var respSize = Marshal.SizeOf(typeof(PamResponse));
#endif

        for (var i = 0; i < numMsg; i++)
        {
#if NET5_0_OR_GREATER
            var message = Marshal.PtrToStructure<PamMessage>(msg + i * msgSize);
#else
            var message = (PamMessage)Marshal.PtrToStructure(msg + i * msgSize, typeof(PamMessage));
#endif
            var response = new PamResponse();

            if (message.MsgStyle == PAM_PROMPT_ECHO_OFF)
            {
                // 只应答第一段口令提示；第二段（动态口令/改密等）直接失败，避免把口令重复送给其它模块
                if (answered || password.IsNullOrEmpty()) return PAM_CONV_ERR;

                response.Resp = Marshal.StringToHGlobalAnsi(password);
                answered = true;
                (_responses ??= []).Add(response.Resp);
            }

            Marshal.StructureToPtr(response, resp + i * respSize, false);
        }

        return PAM_SUCCESS;
    }

    private static void FreeResponses()
    {
        var list = _responses;
        if (list == null || list.Count == 0) return;

        foreach (var item in list)
        {
            if (item != IntPtr.Zero) Marshal.FreeHGlobal(item);
        }
        list.Clear();
    }
    #endregion

    #region 方法
    /// <summary>使用 PAM 验证系统账号口令</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">口令</param>
    /// <returns>是否通过</returns>
    public static Boolean Authenticate(String user, String password)
    {
        var passwordPtr = Marshal.StringToHGlobalAnsi(password);
        try
        {
            var conv = new PamConv
            {
                Conv = Marshal.GetFunctionPointerForDelegate(_callback),
                AppDataPtr = passwordPtr,
            };

            foreach (var service in _services)
            {
                if (TryAuthenticate(service, user, ref conv, out var error)) return true;

                // 明确的口令错误直接结束；配置缺失/服务异常才尝试下一个候选
                if (error == PAM_AUTH_ERR) return false;
            }

            return false;
        }
        catch (DllNotFoundException)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "libpam is not available; OS account authentication cannot be used.");
            return false;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(passwordPtr);
            FreeResponses();
        }
    }

    private static Boolean TryAuthenticate(String service, String user, ref PamConv conv, out Int32 error)
    {
        error = PAM_SUCCESS;

        if (pam_start(service, user, ref conv, out var handle) != PAM_SUCCESS)
        {
            error = -1;
            return false;
        }

        try
        {
            var rc = pam_authenticate(handle, 0);
            if (rc == PAM_SUCCESS) rc = pam_acct_mgmt(handle, 0);

            error = rc;

            // 口令正确但需要改密时，同样视为认证通过（改密由系统策略负责）
            return rc == PAM_SUCCESS || rc == PAM_NEW_AUTHTOK_REQD;
        }
        finally
        {
            pam_end(handle, error);
            FreeResponses();
        }
    }

    /// <summary>查询账号是否属于指定组</summary>
    /// <param name="user">用户名</param>
    /// <param name="groups">组名集合</param>
    /// <returns>是否属于其中任意一个组</returns>
    public static Boolean IsInGroups(String user, String[] groups)
    {
        if (user.IsNullOrEmpty() || groups == null || groups.Length == 0) return false;

        var output = RunId(user);
        if (output.IsNullOrEmpty()) return false;

        var items = output.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            foreach (var group in groups)
            {
                if (item.EqualIgnoreCase(group)) return true;
            }
        }

        return false;
    }

    /// <summary>执行 id -Gn 获取组成员关系。参数不经过 shell，用户名已在调用方校验</summary>
    private static String RunId(String user)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = File.Exists("/usr/bin/id") ? "/usr/bin/id" : "id",
                Arguments = $"-Gn \"{user}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { }

                return null;
            }

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            return null;
        }
    }
    #endregion
}
#endif
