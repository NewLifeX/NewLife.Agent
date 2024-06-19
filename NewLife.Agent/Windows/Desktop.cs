using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NewLife.Log;

namespace NewLife.Agent.Windows;

/// <summary>桌面助手</summary>
//[SupportedOSPlatform("windows")]
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

        var duplicateToken = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;
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
                    file = !workDir.IsNullOrEmpty() ? Path.Combine(workDir, fileName).GetFullPath() : fileName.GetFullPath();
                }
                if (workDir.IsNullOrWhiteSpace()) workDir = Path.GetDirectoryName(file);
            }

            if (commandLine.IsNullOrWhiteSpace()) commandLine = "";

            // 复制令牌
            var sa = new SecurityAttributes();
            sa.Length = Marshal.SizeOf(sa);
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, ref sa, SecurityImpersonationLevel.SecurityIdentification, TokenType.TokenPrimary, out duplicateToken))
                throw new ApplicationException("Could not duplicate token.");

            // 创建环境块（检索该用户的环境变量）
            if (!CreateEnvironmentBlock(out environmentBlock, duplicateToken, false))
                throw new ApplicationException("Could not create environment block.");

            // 启动信息
            var psi = new ProcessStartInfo
            {
                UseShellExecute = shell,
                FileName = file + ' ' + commandLine,
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
            var saProcessAttributes = new SecurityAttributes();
            var saThreadAttributes = new SecurityAttributes();
            var createProcessFlags = (noWindow ? CreateProcessFlags.CREATE_NO_WINDOW : CreateProcessFlags.CREATE_NEW_CONSOLE) | CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT;
            var success = CreateProcessAsUser(duplicateToken, null, file + ' ' + commandLine, ref saProcessAttributes, ref saThreadAttributes, false, createProcessFlags, environmentBlock, null, ref psi, out ProcessInformation pi);
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
            if (duplicateToken != IntPtr.Zero) CloseHandle(duplicateToken);
            if (environmentBlock != IntPtr.Zero) DestroyEnvironmentBlock(environmentBlock);
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
                var arrayElementSize = Marshal.SizeOf(typeof(WtsSessionInfo));
                var current = pSessionInfo;

                for (var i = 0; i < sessionCount; i++)
                {
                    var si = (WtsSessionInfo)Marshal.PtrToStructure(current, typeof(WtsSessionInfo));
                    current += arrayElementSize;

                    if (si.State == WtsConnectStateClass.WTSActive)
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Boolean bInheritHandle, Int32 dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean OpenProcessToken(IntPtr ProcessHandle, Int32 DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean DuplicateTokenEx(IntPtr hExistingToken, UInt32 dwDesiredAccess, ref SecurityAttributes lpTokenAttributes, SecurityImpersonationLevel impersonationLevel, TokenType tokenType, out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, Boolean bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean CreateProcessAsUser(IntPtr hToken, String lpApplicationName, String lpCommandLine, ref SecurityAttributes lpProcessAttributes, ref SecurityAttributes lpThreadAttributes, Boolean bInheritHandles, CreateProcessFlags dwCreationFlags, IntPtr lpEnvironment, String lpCurrentDirectory, ref StartupInfo lpStartupInfo, out ProcessInformation lpProcessInformation);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean CreateProcessAsUser(
        IntPtr hToken,
        String lpApplicationName,
        String lpCommandLine,
        ref SecurityAttributes lpProcessAttributes,
        ref SecurityAttributes lpThreadAttributes,
        Boolean bInheritHandles,
        CreateProcessFlags dwCreationFlags,
        IntPtr lpEnvironment,
        String lpCurrentDirectory,
        ref ProcessStartInfo lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Boolean CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean SetTokenInformation(IntPtr TokenHandle, TokenInformationClass TokenInformationClass, ref UInt32 TokenInformation, UInt32 TokenInformationLength);

    private const UInt32 TOKEN_DUPLICATE = 0x0002;
    private const UInt32 MAXIMUM_ALLOWED = 0x2000000;
    private const UInt32 STARTF_USESHOWWINDOW = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public Int32 Length;
        public IntPtr SecurityDescriptor;
        public Boolean InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
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

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public Int32 dwProcessId;
        public Int32 dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WtsSessionInfo
    {
        public readonly UInt32 SessionID;

        [MarshalAs(UnmanagedType.LPStr)]
        public readonly String pWinStationName;

        public readonly WtsConnectStateClass State;
    }

    /// <summary>
    /// Process Creation Flags。<br/>
    /// More：https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags
    /// </summary>
    [Flags]
    private enum CreateProcessFlags : UInt32
    {
        DEBUG_PROCESS = 0x00000001,
        DEBUG_ONLY_THIS_PROCESS = 0x00000002,
        CREATE_SUSPENDED = 0x00000004,
        DETACHED_PROCESS = 0x00000008,
        /// <summary>
        /// The new process has a new console, instead of inheriting its parent's console (the default). For more information, see Creation of a Console. <br />
        /// This flag cannot be used with <see cref="DETACHED_PROCESS"/>.
        /// </summary>
        CREATE_NEW_CONSOLE = 0x00000010,
        NORMAL_PRIORITY_CLASS = 0x00000020,
        IDLE_PRIORITY_CLASS = 0x00000040,
        HIGH_PRIORITY_CLASS = 0x00000080,
        REALTIME_PRIORITY_CLASS = 0x00000100,
        CREATE_NEW_PROCESS_GROUP = 0x00000200,
        /// <summary>
        /// If this flag is set, the environment block pointed to by lpEnvironment uses Unicode characters. Otherwise, the environment block uses ANSI characters.
        /// </summary>
        CREATE_UNICODE_ENVIRONMENT = 0x00000400,
        CREATE_SEPARATE_WOW_VDM = 0x00000800,
        CREATE_SHARED_WOW_VDM = 0x00001000,
        CREATE_FORCEDOS = 0x00002000,
        BELOW_NORMAL_PRIORITY_CLASS = 0x00004000,
        ABOVE_NORMAL_PRIORITY_CLASS = 0x00008000,
        INHERIT_PARENT_AFFINITY = 0x00010000,
        INHERIT_CALLER_PRIORITY = 0x00020000,
        CREATE_PROTECTED_PROCESS = 0x00040000,
        EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
        PROCESS_MODE_BACKGROUND_BEGIN = 0x00100000,
        PROCESS_MODE_BACKGROUND_END = 0x00200000,
        CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
        CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
        CREATE_DEFAULT_ERROR_MODE = 0x04000000,
        /// <summary>
        /// The process is a console application that is being run without a console window. Therefore, the console handle for the application is not set. <br />
        /// This flag is ignored if the application is not a console application, or if it is used with either <see cref="CREATE_NEW_CONSOLE"/> or <see cref="DETACHED_PROCESS"/>.
        /// </summary>
        CREATE_NO_WINDOW = 0x08000000,
        PROFILE_USER = 0x10000000,
        PROFILE_KERNEL = 0x20000000,
        PROFILE_SERVER = 0x40000000,
        CREATE_IGNORE_SYSTEM_DEFAULT = 0x80000000,
    }

    private enum WtsConnectStateClass
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

    private enum SecurityImpersonationLevel
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation
    }

    private enum TokenType
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    private enum TokenInformationClass
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
    #endregion
}
