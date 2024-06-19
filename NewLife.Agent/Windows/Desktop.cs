using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
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
    public Int32 StartProcess(String fileName, String commandLine = null, String workDir = null, Boolean noWindow = false, Boolean minimize = false)
    {
        if (fileName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(fileName));

        WriteLog("StartProcess: {0}", fileName);

        // 获取当前活动的控制台会话ID和安全的用户访问令牌
        var userToken = GetSessionUserToken();
        if (userToken == IntPtr.Zero)
            throw new ApplicationException("Failed to get user token for the active session.");

        try
        {
            var file = fileName;
            var shell = workDir.IsNullOrEmpty() && (!fileName.Contains('/') && !fileName.Contains('\\'));
            if (shell)
            {
                if (workDir.IsNullOrWhiteSpace()) workDir = Environment.CurrentDirectory;
            }
            else
            {
                if (!Path.IsPathRooted(fileName))
                {
                    if (!workDir.IsNullOrEmpty())
                        file = Path.Combine(workDir, fileName).GetFullPath();
                    else
                        file = fileName.GetFullPath();
                }
                if (workDir.IsNullOrWhiteSpace()) workDir = Path.GetDirectoryName(file);
            }

            if (commandLine.IsNullOrWhiteSpace()) commandLine = "";

            // 启动信息
            var psi = new ProcessStartInfo
            {
                UseShellExecute = shell,
                FileName = file,
                Arguments = commandLine,
                WorkingDirectory = workDir,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                RedirectStandardInput = false,
                CreateNoWindow = noWindow,
                WindowStyle = minimize ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
            };

            /*
             * 2024-6-19 CreateProcessAsUser不能直接传递workDir，否则进程无法启动
             */

            // 在用户会话中创建进程
            var pi = new PROCESS_INFORMATION();
            var success = CreateProcessAsUser(userToken, null, file, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref psi, out pi);
            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
                //throw new ApplicationException("Could not create process as user.");
            }

            return pi.dwProcessId;
        }
        finally
        {
            // 清理资源
            if (userToken != IntPtr.Zero) CloseHandle(userToken);
        }
    }

    /// <summary>日志</summary>
    public ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);

    /// <summary>
    /// 获取活动会话的用户访问令牌
    /// </summary>
    /// <exception cref="Win32Exception"></exception>
    private static IntPtr GetSessionUserToken()
    {
        // 获取当前活动的控制台会话ID
        var sessionId = WTSGetActiveConsoleSessionId();

        // 获取活动会话的用户访问令牌
        var success = WTSQueryUserToken(sessionId, out var hToken);
        // 如果失败，则从会话列表中获取第一个活动的会话ID，并再次尝试获取用户访问令牌
        if (!success)
        {
            sessionId = GetFirstActiveSessionOfEnumerateSessions();
            success = WTSQueryUserToken(sessionId, out hToken);
            if (!success)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return hToken;
    }

    /// <summary>
    /// 枚举所有用户会话，获取第一个活动的会话ID
    /// </summary>
    private static UInt32 GetFirstActiveSessionOfEnumerateSessions()
    {
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

    #region PInvoke调用
    private const Int32 TOKEN_DUPLICATE = 0x0002;
    private const UInt32 MAXIMUM_ALLOWED = 0x2000000;
    private const Int32 CREATE_NEW_CONSOLE = 0x00000010;
    private const Int32 CREATE_NO_WINDOW = 0x08000000;
    private const Int32 CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const Int32 NORMAL_PRIORITY_CLASS = 0x20;
    private const Int32 STARTF_USESHOWWINDOW = 0x00000001;

    /// <summary>
    /// 获取当前活动的控制台会话ID
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UInt32 WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern Int32 WTSEnumerateSessions(IntPtr hServer, Int32 Reserved, Int32 Version, ref IntPtr ppSessionInfo, ref Int32 pCount);

    [DllImport("wtsapi32.dll", SetLastError = false)]
    private static extern void WTSFreeMemory(IntPtr memory);

    /// <summary>
    /// 获取活动会话的用户访问令牌
    /// </summary>
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern Boolean WTSQueryUserToken(UInt32 sessionId, out IntPtr phToken);

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

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean CreateProcessAsUser(
        IntPtr hToken,
        String lpApplicationName,
        String lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        Boolean bInheritHandles,
        UInt32 dwCreationFlags,
        IntPtr lpEnvironment,
        String lpCurrentDirectory,
        ref ProcessStartInfo lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

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
