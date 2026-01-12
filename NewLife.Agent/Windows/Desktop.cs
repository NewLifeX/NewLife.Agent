using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using NewLife.Log;

namespace NewLife.Agent.Windows;

/// <summary>桌面助手。用于从Windows服务在用户桌面会话中启动进程</summary>
#if NET5_0_OR_GREATER
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public class Desktop
{
    #region 属性
    /// <summary>日志</summary>
    public ILog Log { get; set; }
    #endregion

    #region 在用户桌面启动进程
    /// <summary>在用户桌面上启动进程</summary>
    /// <remarks>
    /// 在当前登录用户的桌面会话中启动进程，进程将显示在用户桌面上。
    /// 要求必须有用户已登录桌面会话。
    /// </remarks>
    /// <param name="fileName">可执行文件的完整路径，或PATH中的命令名</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="workDir">工作目录，为空则使用文件所在目录</param>
    /// <param name="noWindow">是否隐藏窗口</param>
    /// <param name="minimize">是否最小化窗口</param>
    /// <returns>进程ID</returns>
    /// <exception cref="ArgumentNullException">fileName为空时抛出</exception>
    /// <exception cref="ApplicationException">获取用户令牌失败时抛出</exception>
    /// <exception cref="Win32Exception">创建进程失败时抛出</exception>
    public UInt32 StartProcess(String fileName, String arguments = null, String workDir = null, Boolean noWindow = false, Boolean minimize = false)
    {
        if (fileName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(fileName));

        WriteLog("StartProcess: {0} {1}", fileName, arguments);

        // 获取当前活动的控制台会话ID和安全的用户访问令牌
        var userToken = GetSessionUserToken(true);
        if (userToken == IntPtr.Zero)
            throw new ApplicationException("Failed to get user token for the active session.");

        var duplicateToken = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;
        try
        {
            // 复制令牌
            var sa = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)) };
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, ref sa, SecurityImpersonationLevel.SecurityIdentification, TokenType.TokenPrimary, out duplicateToken))
                throw new ApplicationException("Could not duplicate token.");

            // 创建环境块（检索该用户的环境变量）
            if (!CreateEnvironmentBlock(out environmentBlock, duplicateToken, false))
                throw new ApplicationException("Could not create environment block.");

            // 处理文件路径和工作目录
            var file = ResolveFilePath(fileName, workDir);
            workDir = ResolveWorkDirectory(file, workDir);

            // 构建完整命令行
            var commandLine = arguments.IsNullOrWhiteSpace() ? file : file + ' ' + arguments;

            // 构建启动信息结构
            var startInfo = new StartupInfo
            {
                cb = Marshal.SizeOf(typeof(StartupInfo)),
                lpDesktop = "winsta0\\default" // 设置为用户的交互式桌面
            };

            // 设置窗口显示方式
            if (noWindow || minimize)
            {
                startInfo.dwFlags = STARTF_USESHOWWINDOW;
                startInfo.wShowWindow = noWindow ? SW_HIDE : SW_SHOWMINIMIZED;
            }

            // 在用户会话中创建进程
            var saProcess = new SecurityAttributes();
            var saThread = new SecurityAttributes();
            var createFlags = (noWindow ? CREATE_NO_WINDOW : CREATE_NEW_CONSOLE) | CREATE_UNICODE_ENVIRONMENT;

            var success = CreateProcessAsUser(duplicateToken, null, commandLine, ref saProcess, ref saThread, false, createFlags, environmentBlock, workDir, ref startInfo, out var pi);
            if (!success)
            {
                var error = Marshal.GetLastWin32Error();
                // 如果失败，尝试不传workDir再试一次
                success = CreateProcessAsUser(duplicateToken, null, commandLine, ref saProcess, ref saThread, false, createFlags, environmentBlock, null, ref startInfo, out pi);
                if (!success) throw new Win32Exception(error);
            }

            WriteLog("Process started on desktop: PID={0}", pi.dwProcessId);

            // 关闭进程和线程句柄（不关闭会导致句柄泄漏）
            CloseHandleSafe(pi.hProcess);
            CloseHandleSafe(pi.hThread);

            return pi.dwProcessId;
        }
        finally
        {
            CloseHandleSafe(userToken);
            CloseHandleSafe(duplicateToken);
            if (environmentBlock != IntPtr.Zero) DestroyEnvironmentBlock(environmentBlock);
        }
    }
    #endregion

    #region 模拟用户身份启动进程（无需桌面会话）
    /// <summary>以用户身份启动后台进程（无需用户登录桌面会话）</summary>
    /// <remarks>
    /// 该方法不依赖用户桌面会话，即使没有用户登录也能工作。
    /// 进程将在后台运行，可访问用户的环境变量和配置文件（包括Git凭据）。
    /// 如果完全没有用户会话，将在SYSTEM上下文中运行并注入指定用户的环境变量。
    /// </remarks>
    /// <param name="fileName">可执行文件的完整路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="workDir">工作目录，为空则使用文件所在目录</param>
    /// <param name="userProfilePath">用户配置目录，为空则自动获取</param>
    /// <returns>进程ID</returns>
    /// <exception cref="ArgumentNullException">fileName为空时抛出</exception>
    /// <exception cref="Win32Exception">创建进程失败时抛出</exception>
    public UInt32 StartProcessAsUser(String fileName, String arguments = null, String workDir = null, String userProfilePath = null)
    {
        if (fileName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(fileName));

        WriteLog("StartProcessAsUser: {0} {1}", fileName, arguments);

        // 尝试获取用户令牌（包括断开连接的会话）
        var userToken = GetSessionUserToken(false);
        if (userToken == IntPtr.Zero)
        {
            WriteLog("No user token available, falling back to SYSTEM context");
            return StartProcessInSystemContext(fileName, arguments, workDir, userProfilePath);
        }

        var duplicateToken = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;
        var profileLoaded = false;
        var profileInfo = new ProfileInfo();

        try
        {
            // 复制令牌
            var sa = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)) };
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, ref sa, SecurityImpersonationLevel.SecurityImpersonation, TokenType.TokenPrimary, out duplicateToken))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not duplicate token.");

            // 获取用户配置目录
            if (userProfilePath.IsNullOrEmpty())
                userProfilePath = GetUserProfilePathFromToken(duplicateToken);

            // 加载用户配置文件（确保可以访问USERPROFILE目录和注册表配置）
            if (!userProfilePath.IsNullOrEmpty())
            {
                profileInfo.dwSize = Marshal.SizeOf(typeof(ProfileInfo));
                profileInfo.lpUserName = Path.GetFileName(userProfilePath);
                profileInfo.dwFlags = 1; // PI_NOUI

                if (LoadUserProfile(duplicateToken, ref profileInfo))
                {
                    profileLoaded = true;
                    WriteLog("User profile loaded: {0}", userProfilePath);
                }
            }

            // 创建环境块（包含用户的环境变量）
            if (!CreateEnvironmentBlock(out environmentBlock, duplicateToken, false))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create environment block.");

            // 处理文件路径和工作目录
            var file = Path.IsPathRooted(fileName) ? fileName : fileName.GetFullPath();
            if (workDir.IsNullOrWhiteSpace()) workDir = Path.GetDirectoryName(file);

            // 构建命令行
            var commandLine = arguments.IsNullOrWhiteSpace() ? file : file + ' ' + arguments;

            // 构建启动信息（后台运行，不显示窗口）
            var startInfo = new StartupInfo
            {
                cb = Marshal.SizeOf(typeof(StartupInfo)),
                dwFlags = STARTF_USESHOWWINDOW,
                wShowWindow = SW_HIDE
            };

            var saProcess = new SecurityAttributes();
            var saThread = new SecurityAttributes();
            var createFlags = CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT;

            var success = CreateProcessAsUser(duplicateToken, null, commandLine, ref saProcess, ref saThread, false, createFlags, environmentBlock, workDir, ref startInfo, out var pi);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());

            WriteLog("Process started as user: PID={0}", pi.dwProcessId);

            CloseHandleSafe(pi.hProcess);
            CloseHandleSafe(pi.hThread);

            return pi.dwProcessId;
        }
        finally
        {
            if (profileLoaded) UnloadUserProfile(duplicateToken, profileInfo.hProfile);
            if (environmentBlock != IntPtr.Zero) DestroyEnvironmentBlock(environmentBlock);
            CloseHandleSafe(duplicateToken);
            CloseHandleSafe(userToken);
        }
    }

    /// <summary>在SYSTEM上下文中启动进程，通过环境变量注入用户配置路径</summary>
    /// <param name="fileName">可执行文件路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <param name="workDir">工作目录</param>
    /// <param name="userProfilePath">用户配置目录</param>
    /// <returns>进程ID</returns>
    private UInt32 StartProcessInSystemContext(String fileName, String arguments, String workDir, String userProfilePath)
    {
        // 如果没有指定用户配置路径，尝试查找第一个用户目录
        if (userProfilePath.IsNullOrEmpty())
            userProfilePath = FindFirstUserProfilePath();

        // 处理文件路径和工作目录
        var file = Path.IsPathRooted(fileName) ? fileName : fileName.GetFullPath();
        if (workDir.IsNullOrWhiteSpace()) workDir = Path.GetDirectoryName(file);

        // 构建命令行
        var commandLine = arguments.IsNullOrWhiteSpace() ? file : file + ' ' + arguments;

        // 构建启动信息
        var startInfo = new StartupInfo
        {
            cb = Marshal.SizeOf(typeof(StartupInfo)),
            dwFlags = STARTF_USESHOWWINDOW,
            wShowWindow = SW_HIDE
        };

        var saProcess = new SecurityAttributes();
        var saThread = new SecurityAttributes();
        var createFlags = CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT;

        // 构建包含用户路径的环境块
        var envPtr = BuildEnvironmentBlockPtr(userProfilePath);
        try
        {
            var success = CreateProcess(null, commandLine, ref saProcess, ref saThread, false, createFlags, envPtr, workDir, ref startInfo, out var pi);
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());

            WriteLog("Process started in SYSTEM context: PID={0}", pi.dwProcessId);

            CloseHandleSafe(pi.hProcess);
            CloseHandleSafe(pi.hThread);

            return pi.dwProcessId;
        }
        finally
        {
            if (envPtr != IntPtr.Zero) Marshal.FreeHGlobal(envPtr);
        }
    }
    #endregion

    #region 辅助
    /// <summary>写日志</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">参数</param>
    protected void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion

    #region 私有方法
    /// <summary>解析文件路径</summary>
    /// <param name="fileName">文件名</param>
    /// <param name="workDir">工作目录</param>
    /// <returns>解析后的文件路径</returns>
    private static String ResolveFilePath(String fileName, String workDir)
    {
        // 绝对路径直接返回
        if (Path.IsPathRooted(fileName)) return fileName;

        // 简单命令名（如 notepad.exe），尝试在workDir中查找
        if (!fileName.Contains('/') && !fileName.Contains('\\'))
        {
            if (!workDir.IsNullOrEmpty())
            {
                var fullPath = Path.Combine(workDir, fileName);
                if (File.Exists(fullPath)) return fullPath.GetFullPath();
            }
            // 保持原样让系统在PATH中查找
            return fileName;
        }

        // 相对路径，转换为绝对路径
        return !workDir.IsNullOrEmpty() ? Path.Combine(workDir, fileName).GetFullPath() : fileName.GetFullPath();
    }

    /// <summary>解析工作目录</summary>
    /// <param name="file">文件路径</param>
    /// <param name="workDir">原始工作目录</param>
    /// <returns>解析后的工作目录</returns>
    private static String ResolveWorkDirectory(String file, String workDir)
    {
        if (!workDir.IsNullOrWhiteSpace()) return workDir;
        return Path.IsPathRooted(file) ? Path.GetDirectoryName(file) : Environment.CurrentDirectory;
    }

    /// <summary>获取活动会话的用户访问令牌</summary>
    /// <param name="throwOnFailure">失败时是否抛出异常</param>
    /// <returns>用户令牌句柄，失败返回IntPtr.Zero</returns>
    /// <exception cref="Win32Exception">throwOnFailure为true且获取失败时抛出</exception>
    private static IntPtr GetSessionUserToken(Boolean throwOnFailure)
    {
        // 首先尝试从活动控制台会话获取
        var sessionId = WTSGetActiveConsoleSessionId();
        if (WTSQueryUserToken(sessionId, out var hToken)) return hToken;

        // 枚举所有会话，查找活动或断开连接的会话
        var pSessionInfo = IntPtr.Zero;
        try
        {
            var sessionCount = 0;
            if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref pSessionInfo, ref sessionCount) != 0)
            {
                var elementSize = Marshal.SizeOf(typeof(WtsSessionInfo));
                var current = pSessionInfo;

                for (var i = 0; i < sessionCount; i++)
                {
                    var si = (WtsSessionInfo)Marshal.PtrToStructure(current, typeof(WtsSessionInfo));
                    current += elementSize;

                    // 仅从活动会话获取（throwOnFailure=true）或同时包括断开的会话
                    var isValidState = si.State == WtsConnectStateClass.WTSActive ||
                                       (!throwOnFailure && si.State == WtsConnectStateClass.WTSDisconnected);

                    if (isValidState && WTSQueryUserToken(si.SessionID, out hToken))
                        return hToken;
                }
            }
        }
        finally
        {
            if (pSessionInfo != IntPtr.Zero) WTSFreeMemory(pSessionInfo);
        }

        if (throwOnFailure)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to get user token for active session.");

        return IntPtr.Zero;
    }

    /// <summary>从令牌获取用户配置目录路径</summary>
    /// <param name="hToken">用户令牌</param>
    /// <returns>用户配置目录路径，失败返回null</returns>
    private static String GetUserProfilePathFromToken(IntPtr hToken)
    {
        var bufferSize = 260;
        var buffer = new StringBuilder(bufferSize);
        return GetUserProfileDirectory(hToken, buffer, ref bufferSize) ? buffer.ToString() : null;
    }

    /// <summary>查找第一个用户配置目录</summary>
    /// <returns>用户配置目录路径，未找到返回null</returns>
    private static String FindFirstUserProfilePath()
    {
        // 先尝试当前环境的用户目录
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!userProfile.IsNullOrEmpty() && Directory.Exists(userProfile)) return userProfile;

        // 尝试在系统盘 Users 目录下查找
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (systemRoot.IsNullOrEmpty()) return null;

        var profilesDir = Path.Combine(systemRoot.Substring(0, 3), "Users");
        if (!Directory.Exists(profilesDir)) return null;

        var excludeNames = new[] { "Public", "Default", "Default User", "All Users" };
        foreach (var dir in Directory.GetDirectories(profilesDir))
        {
            var name = Path.GetFileName(dir);
            if (!excludeNames.Any(e => e.EqualIgnoreCase(name)))
                return dir;
        }

        return null;
    }

    /// <summary>构建包含用户路径的环境块指针</summary>
    /// <param name="userProfilePath">用户配置目录</param>
    /// <returns>环境块内存指针，调用方负责释放</returns>
    private static IntPtr BuildEnvironmentBlockPtr(String userProfilePath)
    {
        var env = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        // 复制当前环境变量
        foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
        {
            env[e.Key.ToString()] = e.Value?.ToString();
        }

        // 注入用户相关的环境变量
        if (!userProfilePath.IsNullOrEmpty())
        {
            env["USERPROFILE"] = userProfilePath;
            env["HOME"] = userProfilePath;

            var pathRoot = Path.GetPathRoot(userProfilePath);
            if (!pathRoot.IsNullOrEmpty())
            {
                env["HOMEDRIVE"] = pathRoot.TrimEnd('\\');
                env["HOMEPATH"] = userProfilePath.Substring(pathRoot.Length);
            }

            env["APPDATA"] = Path.Combine(userProfilePath, "AppData", "Roaming");
            env["LOCALAPPDATA"] = Path.Combine(userProfilePath, "AppData", "Local");

            // Git特定的环境变量
            var gitConfigPath = Path.Combine(userProfilePath, ".gitconfig");
            if (File.Exists(gitConfigPath))
                env["GIT_CONFIG_GLOBAL"] = gitConfigPath;
        }

        // 构建环境块字符串（每个变量以\0结尾，整个块以\0\0结尾）
        var sb = new StringBuilder();
        foreach (var kv in env.OrderBy(x => x.Key))
        {
            sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\0');
        }
        sb.Append('\0');

        // 转换为Unicode字节并分配非托管内存
        var str = sb.ToString();
        var bytes = Encoding.Unicode.GetBytes(str);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);

        return ptr;
    }

    /// <summary>安全关闭句柄</summary>
    /// <param name="handle">句柄</param>
    private static void CloseHandleSafe(IntPtr handle)
    {
        if (handle != IntPtr.Zero) CloseHandle(handle);
    }
    #endregion

    #region PInvoke
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UInt32 WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern Int32 WTSEnumerateSessions(IntPtr hServer, Int32 Reserved, Int32 Version, ref IntPtr ppSessionInfo, ref Int32 pCount);

    [DllImport("wtsapi32.dll", SetLastError = false)]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern Boolean WTSQueryUserToken(UInt32 sessionId, out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern Boolean DuplicateTokenEx(IntPtr hExistingToken, UInt32 dwDesiredAccess, ref SecurityAttributes lpTokenAttributes, SecurityImpersonationLevel impersonationLevel, TokenType tokenType, out IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, Boolean bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean LoadUserProfile(IntPtr hToken, ref ProfileInfo lpProfileInfo);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern Boolean UnloadUserProfile(IntPtr hToken, IntPtr hProfile);

    [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean GetUserProfileDirectory(IntPtr hToken, StringBuilder lpProfileDir, ref Int32 lpcchSize);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean CreateProcessAsUser(IntPtr hToken, String lpApplicationName, String lpCommandLine, ref SecurityAttributes lpProcessAttributes, ref SecurityAttributes lpThreadAttributes, Boolean bInheritHandles, UInt32 dwCreationFlags, IntPtr lpEnvironment, String lpCurrentDirectory, ref StartupInfo lpStartupInfo, out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern Boolean CreateProcess(String lpApplicationName, String lpCommandLine, ref SecurityAttributes lpProcessAttributes, ref SecurityAttributes lpThreadAttributes, Boolean bInheritHandles, UInt32 dwCreationFlags, IntPtr lpEnvironment, String lpCurrentDirectory, ref StartupInfo lpStartupInfo, out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern Boolean CloseHandle(IntPtr hObject);

    private const UInt32 MAXIMUM_ALLOWED = 0x2000000;

    // 进程创建标志
    private const UInt32 CREATE_NEW_CONSOLE = 0x00000010;
    private const UInt32 CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const UInt32 CREATE_NO_WINDOW = 0x08000000;

    // 启动信息标志
    private const UInt32 STARTF_USESHOWWINDOW = 0x00000001;

    // 窗口显示方式
    private const UInt16 SW_HIDE = 0;
    private const UInt16 SW_SHOWMINIMIZED = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public Int32 Length;
        public IntPtr SecurityDescriptor;
        public Boolean InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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
        public UInt32 dwFlags;
        public UInt16 wShowWindow;
        public UInt16 cbReserved2;
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
        public UInt32 dwProcessId;
        public UInt32 dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProfileInfo
    {
        public Int32 dwSize;
        public Int32 dwFlags;
        public String lpUserName;
        public String lpProfilePath;
        public String lpDefaultPath;
        public String lpServerName;
        public String lpPolicyPath;
        public IntPtr hProfile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WtsSessionInfo
    {
        public readonly UInt32 SessionID;
        [MarshalAs(UnmanagedType.LPStr)]
        public readonly String pWinStationName;
        public readonly WtsConnectStateClass State;
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
    #endregion
}
