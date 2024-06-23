using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NewLife.Log;

namespace NewLife.Agent.Windows;

/// <summary>桌面助手</summary>
#if NET5_0_OR_GREATER
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public class Desktop
{
    /// <summary>在用户桌面上启动进程</summary>
    /// <param name="fileName"></param>
    /// <param name="commandLine"></param>
    /// <param name="workDir"></param>
    /// <param name="noWindow"></param>
    /// <param name="minimize"></param>
    /// <exception cref="ApplicationException"></exception>
    public UInt32 StartProcess(String fileName, String commandLine = null, String workDir = null, Boolean noWindow = false, Boolean minimize = false)
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
            // 复制令牌
            var sa = new SecurityAttributes();
            sa.Length = Marshal.SizeOf(sa);
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, ref sa, SecurityImpersonationLevel.SecurityIdentification, TokenType.TokenPrimary, out duplicateToken))
                throw new ApplicationException("Could not duplicate token.");

            // 创建环境块（检索该用户的环境变量）
            if (!CreateEnvironmentBlock(out environmentBlock, duplicateToken, false))
                throw new ApplicationException("Could not create environment block.");

            Boolean theCommandIsInPath;
            // 如果文件名不包含路径分隔符，则尝试先在workDir参数中查找。如果找不到，再在指定用户会话的PATH环境变量中查找。如果还是找不到，则抛出异常
            if ((!fileName.Contains('/') && !fileName.Contains('\\')))
            {
                if (!workDir.IsNullOrEmpty())
                {
                    if (File.Exists(Path.Combine(workDir, fileName)))
                    {
                        // 在指定的工作目录中找到可执行命令文件
                        theCommandIsInPath = false;
                    }
                    else
                    {
                        // 在指定的工作目录(workDir)中找不到可执行命令文件，再在指定用户会话的PATH环境变量中查找。如果还是找不到，则抛出异常
                        if (!InPathOfSpecificUserEnvironment(in duplicateToken, in environmentBlock, fileName))
                        {
                            throw new ApplicationException($"The file '{fileName}' was not found in the specified directory '{workDir}' or in the PATH environment variable.");
                        }
                        else
                        {
                            // 在指定用户会话的PATH环境变量中找到可执行命令文件
                            theCommandIsInPath = true;
                        }
                    }
                }
                else
                {
                    // 在指定用户会话的PATH环境变量中查找，如果找不到，则抛出异常
                    if (!InPathOfSpecificUserEnvironment(in duplicateToken, in environmentBlock, fileName))
                    {
                        throw new ApplicationException($"The file '{fileName}' was not found in the PATH environment variable.");
                    }
                    // 在指定用户会话的PATH环境变量中找到可执行命令文件
                    theCommandIsInPath = true;
                }
            }
            else
            {
                theCommandIsInPath = false;
            }

            String file;
            if (!theCommandIsInPath && !Path.IsPathRooted(fileName))
            {
                file = !workDir.IsNullOrEmpty() ? Path.Combine(workDir, fileName).GetFullPath() : fileName.GetFullPath();
            }
            else
            {
                file = fileName;
            }

            if (workDir.IsNullOrWhiteSpace()) workDir = theCommandIsInPath ? Environment.CurrentDirectory : Path.GetDirectoryName(file);

            if (commandLine.IsNullOrWhiteSpace()) commandLine = "";

            // 启动信息
            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = file + ' ' + commandLine,
                Arguments = commandLine,
                WorkingDirectory = workDir!,
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
    /// 使用win32api实现在指定用户身份的环境变量中查找命令(command参数)是否存在。
    /// </summary>
    private Boolean InPathOfSpecificUserEnvironment(in IntPtr userToken, in IntPtr environmentBlock, in String command)
    {
        // 在指定用户会话环境中执行命令，并且获得控制台标准输出内容
        String commandLine = $"cmd.exe /c chcp 65001 && where {command}";
        String output = ExecuteCommandAsUserAndReturnStdOutput(userToken, environmentBlock, commandLine, Encoding.UTF8);

        // OperatingSystem.IsOSPlatform("WINDOWS") 该方法仅在 .NET Core及以上版本可用，在 .NET Standard 和 .NET Framework 中不可用。
        // 现有操作系统中，Windows 操作系统的目录分隔符为 '\'，而 Unix 操作系统的目录分隔符为 '/'，因此可以用它来判断和区分操作系统。
        // 如果是Windows操作系统，则不区分大小写
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return output.IndexOf(command, comparison) >= 0;
    }

    /// <summary>
    /// 在指定用户会话环境中执行命令，并且返回控制台标准输出内容
    /// </summary>
    private String ExecuteCommandAsUserAndReturnStdOutput(in IntPtr userToken, in IntPtr environmentBlock, String commandLine, Encoding encoding)
    {
        // 创建匿名管道
        var saPipeAttributes = new SecurityAttributes();
        saPipeAttributes.Length = Marshal.SizeOf(saPipeAttributes);
        saPipeAttributes.InheritHandle = true; // 允许句柄被继承
        //saPipeAttributes.SecurityDescriptor = IntPtr.Zero;
        if (!CreatePipe(out IntPtr readPipe, out IntPtr writePipe, ref saPipeAttributes, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        // 确保管道句柄有效
        if (readPipe == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create read pipe.");
        }
        if (writePipe == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create write pipe.");
        }

        try
        {
            // 确保读取句柄不被子进程继承
            SetHandleInformation(readPipe, 0x00000001/*HANDLE_FLAG_INHERIT*/, 0);

            var startInfo = new StartupInfo();
            startInfo.cb = Marshal.SizeOf(startInfo);
            // 设置子进程的标准输出为管道的写入端
            startInfo.hStdError = writePipe;
            startInfo.hStdOutput = writePipe;
            startInfo.dwFlags = StartupInfoFlags.STARTF_USESTDHANDLES;

            // 在用户会话中创建进程
            const CreateProcessFlags createProcessFlags = CreateProcessFlags.CREATE_NEW_CONSOLE | CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT;
            var success = CreateProcessAsUser(
                userToken,
                null,
                commandLine,
                ref saPipeAttributes,
                ref saPipeAttributes,
                true,
                createProcessFlags,
                environmentBlock,
                null,
                ref startInfo,
                out ProcessInformation pi);
            if (!success)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // 关闭管道的写入端句柄，因为它已经被子进程继承
            CloseHandle(writePipe);
            writePipe = IntPtr.Zero;

            // 从管道的读取端读取数据
            String output;
            using (var streamReader = new StreamReader(new FileStream(new SafeFileHandle(readPipe, true), FileAccess.Read, 4096, false), encoding))
            {
                // 读取控制台标准输出内容
                output = streamReader.ReadToEnd();
                Log?.Debug($"The commandLine [{commandLine}] std output -> {output}");
            }

            // 关闭进程和线程句柄
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);

            // 返回控制台标准输出内容
            return output;
        }
        finally
        {
            if (readPipe != IntPtr.Zero) CloseHandle(readPipe);
            if (writePipe != IntPtr.Zero) CloseHandle(writePipe);
        }
    }

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
            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref pSessionInfo, ref sessionCount) == 0)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to enumerate sessions. Error code: {errorCode}");
            }

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

            return UInt32.MaxValue;
        }
        finally
        {
            // 清理资源
            if (pSessionInfo != IntPtr.Zero)
            {
                WTSFreeMemory(pSessionInfo);
                //如果没有判断是否为IntPtr.Zero，会导致引发SEHException异常："external component has thrown an exception"
                CloseHandle(pSessionInfo);
            }
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
    private static extern Boolean CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SecurityAttributes lpPipeAttributes, UInt32 nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern Boolean CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean SetTokenInformation(IntPtr TokenHandle, TokenInformationClass TokenInformationClass, ref UInt32 TokenInformation, UInt32 TokenInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern Boolean SetHandleInformation(IntPtr hObject, UInt32 dwMask, UInt32 dwFlags);

    private const UInt32 TOKEN_DUPLICATE = 0x0002;
    private const UInt32 MAXIMUM_ALLOWED = 0x2000000;

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
        public UInt32 dwX;
        public UInt32 dwY;
        public UInt32 dwXSize;
        public UInt32 dwYSize;
        public UInt32 dwXCountChars;
        public UInt32 dwYCountChars;
        public UInt32 dwFillAttribute;
        public StartupInfoFlags dwFlags;
        public UInt16 wShowWindow;
        public UInt16 cbReserved2;
        public unsafe Byte* lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public UInt32 dwProcessId;
        public UInt32 dwThreadId;
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

    /// <summary>
    /// 指定创建进程时的窗口工作站、桌面、标准句柄和main窗口的外观。<br/>
    /// More：https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfoa
    /// </summary>
    [Flags]
    private enum StartupInfoFlags : UInt32
    {
        /// <summary>
        /// 强制反馈光标显示，即使用户没有启用。
        /// </summary>
        STARTF_FORCEONFEEDBACK = 0x00000040,
        /// <summary>
        /// 强制反馈光标不显示，即使用户启用了它。
        /// </summary>
        STARTF_FORCEOFFFEEDBACK = 0x00000080,
        /// <summary>
        /// 防止应用程序被固定在任务栏或开始菜单。
        /// </summary>
        STARTF_PREVENTPINNING = 0x00002000,
        /// <summary>
        /// 不再支持，原用于强制控制台应用程序全屏运行。
        /// </summary>
        STARTF_RUNFULLSCREEN = 0x00000020,
        /// <summary>
        /// lpTitle成员是一个AppUserModelID。
        /// </summary>
        STARTF_TITLEISAPPID = 0x00001000,
        /// <summary>
        /// lpTitle成员是一个链接名。
        /// </summary>
        STARTF_TITLEISLINKNAME = 0x00000800,
        /// <summary>
        /// 启动程序来自不受信任的源，可能会显示警告。
        /// </summary>
        STARTF_UNTRUSTEDSOURCE = 0x00008000,
        /// <summary>
        /// 使用dwXCountChars和dwYCountChars成员。
        /// </summary>
        STARTF_USECOUNTCHARS = 0x00000008,
        /// <summary>
        /// 使用dwFillAttribute成员。
        /// </summary>
        STARTF_USEFILLATTRIBUTE = 0x00000010,
        /// <summary>
        /// 使用hStdInput成员指定热键。
        /// </summary>
        STARTF_USEHOTKEY = 0x00000200,
        /// <summary>
        /// 使用dwX和dwY成员。
        /// </summary>
        STARTF_USEPOSITION = 0x00000004,
        /// <summary>
        /// 使用wShowWindow成员。
        /// </summary>
        STARTF_USESHOWWINDOW = 0x00000001,
        /// <summary>
        /// 使用dwXSize和dwYSize成员。
        /// </summary>
        STARTF_USESIZE = 0x00000002,
        /// <summary>
        /// 使用hStdInput、hStdOutput和hStdError成员。
        /// </summary>
        STARTF_USESTDHANDLES = 0x00000100
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
