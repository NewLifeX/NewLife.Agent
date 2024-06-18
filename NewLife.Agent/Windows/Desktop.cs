using System.Diagnostics;
using System.Runtime.InteropServices;
using NewLife.Log;

namespace NewLife.Agent.Windows;

/// <summary>桌面助手</summary>
public class Desktop
{
    /// <summary>在用户桌面上启动进程</summary>
    /// <param name="fileName"></param>
    /// <param name="commandLine"></param>
    /// <param name="workDir"></param>
    /// <param name="noWindow"></param>
    /// <param name="minimize"></param>
    /// <exception cref="ApplicationException"></exception>
    public void StartProcess(String fileName, String commandLine = null, String workDir = null, Boolean noWindow = false, Boolean minimize = false)
    {
        WriteLog("StartProcess {0}", fileName);

        var userToken = IntPtr.Zero;
        var duplicateToken = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;

        try
        {
            // 获取当前活动的控制台会话ID
            var sessionId = GetSessionId();
            WriteLog("sessionId: {0}", sessionId);

            var winlogonPid = 0;
            foreach (var p in Process.GetProcessesByName("winlogon"))
            {
                if ((UInt32)p.SessionId == sessionId)
                {
                    winlogonPid = p.Id;
                }
            }

            // 获取进程令牌
            var hProcess = OpenProcess(MAXIMUM_ALLOWED, false, winlogonPid);
            if (!OpenProcessToken(hProcess, TOKEN_DUPLICATE, out userToken))
                throw new ApplicationException("Could not OpenProcessToken.");

            //// 获取用户令牌
            //if (!WTSQueryUserToken(sessionId, out userToken))
            //    throw new ApplicationException("Could not get user token.");

            WriteLog("UserToken: 0x{0:X8}", userToken);

            // 复制令牌
            var sa = new SECURITY_ATTRIBUTES();
            sa.Length = Marshal.SizeOf(sa);
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, ref sa, (Int32)SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (Int32)TOKEN_TYPE.TokenPrimary, out duplicateToken))
                throw new ApplicationException("Could not duplicate token.");

            WriteLog("duplicateToken: 0x{0:X8}", duplicateToken);

            //// 把令牌的SessionId替换成当前活动的Session(即替换到可与用户交互的winsta0下)
            //if (!SetTokenInformation(duplicateToken, TOKEN_INFORMATION_CLASS.TokenSessionId, ref sessionId, (UInt32)IntPtr.Size))
            //    throw new ApplicationException("Could not set token information.");

            // 创建环境块
            if (!CreateEnvironmentBlock(out environmentBlock, duplicateToken, false))
                throw new ApplicationException("Could not create environment block.");

            WriteLog("environmentBlock: 0x{0:X8}", environmentBlock);

            // 启动信息
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = @"winsta0\default";

            if (minimize)
            {
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = (Int16)SW.SW_MINIMIZE;
            }
            if (!noWindow)
            {
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = (Int16)SW.SW_SHOW;
            }

            // 指定进程的优先级和创建方法，这里代表是普通优先级，并且创建方法是带有UI的进程
            var dwCreationFlags = CREATE_UNICODE_ENVIRONMENT | NORMAL_PRIORITY_CLASS | (noWindow ? CREATE_NO_WINDOW : CREATE_NEW_CONSOLE);

            var pi = new PROCESS_INFORMATION();

            // 在用户会话中创建进程
            if (!CreateProcessAsUser(duplicateToken,
                fileName,
                commandLine,
                ref sa,
                ref sa,
                false,
                (UInt32)dwCreationFlags,
                environmentBlock,
                workDir,
                ref si,
                out pi))
                throw new ApplicationException("Could not create process as user.");

            WriteLog("ProcessId: {0}", pi.dwProcessId);
        }
        finally
        {
            // 清理资源
            if (userToken != IntPtr.Zero) CloseHandle(userToken);
            if (duplicateToken != IntPtr.Zero) CloseHandle(duplicateToken);
            if (environmentBlock != IntPtr.Zero) DestroyEnvironmentBlock(environmentBlock);
        }
    }

    /// <summary>获取会话Id</summary>
    /// <returns></returns>
    public UInt32 GetSessionId()
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId > 0) return sessionId;

        var pSessionInfo = IntPtr.Zero;
        try
        {
            var sessionCount = 0;

            // 枚举所有用户会话
            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref pSessionInfo, ref sessionCount) != 0)
            {
                var arrayElementSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                var current = pSessionInfo;

                for (var i = 0; i < sessionCount; i++)
                {
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure(current, typeof(WTS_SESSION_INFO));
                    current += arrayElementSize;

                    if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive)
                    {
                        return si.SessionID;
                    }
                }
            }

            return UInt32.MaxValue;
        }
        finally
        {
            WTSFreeMemory(pSessionInfo);
            CloseHandle(pSessionInfo);
        }
    }

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);

    #region PInvoke调用
    private const Int32 TOKEN_DUPLICATE = 0x0002;
    private const UInt32 MAXIMUM_ALLOWED = 0x2000000;
    private const Int32 CREATE_NEW_CONSOLE = 0x00000010;
    private const Int32 CREATE_NO_WINDOW = 0x08000000;
    private const Int32 CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const Int32 NORMAL_PRIORITY_CLASS = 0x20;
    private const Int32 STARTF_USESHOWWINDOW = 0x00000001;

    [DllImport("kernel32.dll")]
    private static extern UInt32 WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern Int32 WTSEnumerateSessions(IntPtr hServer, Int32 Reserved, Int32 Version, ref IntPtr ppSessionInfo, ref Int32 pCount);

    [DllImport("wtsapi32.dll", SetLastError = false)]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern Boolean WTSQueryUserToken(UInt32 sessionId, out IntPtr Token);

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public readonly UInt32 SessionID;

        [MarshalAs(UnmanagedType.LPStr)]
        public readonly String pWinStationName;

        public readonly WTS_CONNECTSTATE_CLASS State;
    }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Boolean bInheritHandle, Int32 dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean OpenProcessToken(IntPtr ProcessHandle, Int32 DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean DuplicateTokenEx(IntPtr hExistingToken, UInt32 dwDesiredAccess, ref SECURITY_ATTRIBUTES lpTokenAttributes, Int32 ImpersonationLevel, Int32 TokenType, out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, Boolean bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean CreateProcessAsUser(IntPtr hToken, String lpApplicationName, String lpCommandLine, ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes, Boolean bInheritHandles, UInt32 dwCreationFlags, IntPtr lpEnvironment, String lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Boolean CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean SetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, ref UInt32 TokenInformation, UInt32 TokenInformationLength);

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    private enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
        TokenGroups,
        TokenPrivileges,
        TokenOwner,
        TokenPrimaryGroup,
        TokenDefaultDacl,
        TokenSource,
        TokenType,
        TokenImpersonationLevel,
        TokenStatistics,
        TokenRestrictedSids,
        TokenSessionId,
        TokenGroupsAndPrivileges,
        TokenSessionReference,
        TokenSandBoxInert,
        TokenAuditPolicy,
        TokenOrigin,
        TokenElevationType,
        TokenLinkedToken,
        TokenElevation,
        TokenHasRestrictions,
        TokenAccessInformation,
        TokenVirtualizationAllowed,
        TokenVirtualizationEnabled,
        TokenIntegrityLevel,
        TokenUIAccess,
        TokenMandatoryPolicy,
        TokenLogonSid,
        MaxTokenInfoClass
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public Int32 Length;
        public IntPtr SecurityDescriptor;
        public Boolean InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public Int32 cb;
        public String lpReserved;
        public String lpDesktop;
        public String lpTitle;
        public Int32 dwX;
        public Int32 dwY;
        public Int32 dwXSize;
        public Int32 dwYSize;
        public Int32 dwXCountChars;
        public Int32 dwYCountChars;
        public Int32 dwFillAttribute;
        public Int32 dwFlags;
        public Int16 wShowWindow;
        public Int16 cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    private enum SW : Int32
    {
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_NORMAL = 1,
        SW_SHOWMINIMIZED = 2,
        SW_SHOWMAXIMIZED = 3,
        SW_MAXIMIZE = 3,
        SW_SHOWNOACTIVATE = 4,
        SW_SHOW = 5,
        SW_MINIMIZE = 6,
        SW_SHOWMINNOACTIVE = 7,
        SW_SHOWNA = 8,
        SW_RESTORE = 9,
        SW_SHOWDEFAULT = 10,
        SW_FORCEMINIMIZE = 11,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public Int32 dwProcessId;
        public Int32 dwThreadId;
    }
    #endregion
}
