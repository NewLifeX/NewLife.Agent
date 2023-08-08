using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>Windows自启动</summary>
/// <remarks>
/// 桌面登录自启动
/// </remarks>
public class WindowsAutorun : Host
{
    /// <summary>开始执行服务</summary>
    /// <param name="service"></param>
    public override void Run(ServiceBase service) => throw new NotSupportedException();

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean IsInstalled(String serviceName)
    {
#if NET40_OR_GREATER || NET5_0_OR_GREATER
        // 在注册表中写入启动配置
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", false);

        var v = key.GetValue(serviceName)?.ToString();
        return !v.IsNullOrEmpty();
#else
        return false;
#endif
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override unsafe Boolean IsRunning(String serviceName) => false;

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName"></param>
    /// <param name="binPath"></param>
    /// <param name="description"></param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean Install(String serviceName, String displayName, String binPath, String description)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", GetType().Name, serviceName, displayName, binPath, description);

#if !NETSTANDARD
        if (!IsAdministrator()) return RunAsAdministrator("-i");
#endif

        //// 当前用户的启动目录
        //var dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        //if (dir.IsNullOrEmpty()) return false;

        //dir.EnsureDirectory(false);

#if NET40_OR_GREATER || NET5_0_OR_GREATER
        // 在注册表中写入启动配置
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", true);

        // 添加应用程序到自启动项
        key.SetValue(serviceName, binPath);
#endif

        return true;
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

#if !NETSTANDARD
        if (!IsAdministrator()) return RunAsAdministrator("-uninstall");
#endif

#if NET40_OR_GREATER || NET5_0_OR_GREATER
        // 在注册表中写入启动配置
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", true);
        key.DeleteValue(serviceName, false);
#endif

        return true;
    }

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe ServiceConfig QueryConfig(String serviceName)
    {
#if NET40_OR_GREATER || NET5_0_OR_GREATER
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", false);

        var v = key.GetValue(serviceName)?.ToString();
        if (v.IsNullOrEmpty()) return null;

        return new ServiceConfig
        {
            Name = serviceName,
            FilePath = v.TrimEnd(" -run")
        };
#else
        return null;
#endif
    }

#if !NETSTANDARD
    static Boolean RunAsAdministrator(String argument)
    {
        var exe = ExecutablePath;
        if (exe.IsNullOrEmpty()) return false;

        if (exe.EndsWithIgnoreCase(".dll"))
        {
            var exe2 = Path.ChangeExtension(exe, ".exe");
            if (File.Exists(exe2)) exe = exe2;
        }

        var startInfo = exe.EndsWithIgnoreCase(".dll") ?
            new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"{Path.GetFileName(exe)} {argument}",
                WorkingDirectory = Path.GetDirectoryName(exe),
                Verb = "runas",
                UseShellExecute = true,
            } :
            new ProcessStartInfo
            {
                FileName = exe,
                Arguments = argument,
                Verb = "runas",
                UseShellExecute = true,
            };

        var p = Process.Start(startInfo);
        return !p.WaitForExit(5_000) || p.ExitCode == 0;
    }

    static String _executablePath;
    static String ExecutablePath
    {
        get
        {
            if (_executablePath == null)
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    var codeBase = entryAssembly.CodeBase;
                    var uri = new Uri(codeBase);
                    _executablePath = uri.IsFile ? uri.LocalPath + Uri.UnescapeDataString(uri.Fragment) : uri.ToString();
                }
                else
                {
                    var moduleFileNameLongPath = GetModuleFileNameLongPath(new HandleRef(null, IntPtr.Zero));
                    _executablePath = moduleFileNameLongPath.ToString().GetFullPath();
                }
            }

            return _executablePath;
        }
    }

    static StringBuilder GetModuleFileNameLongPath(HandleRef hModule)
    {
        var sb = new StringBuilder(260);
        var num = 1;
        var num2 = 0;
        while ((num2 = GetModuleFileName(hModule, sb, sb.Capacity)) == sb.Capacity && Marshal.GetLastWin32Error() == 122 && sb.Capacity < 32767)
        {
            num += 2;
            var capacity = (num * 260 < 32767) ? (num * 260) : 32767;
            sb.EnsureCapacity(capacity);
        }
        sb.Length = num2;
        return sb;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern Int32 GetModuleFileName(HandleRef hModule, StringBuilder buffer, Int32 length);

#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    private Boolean IsAdministrator()
    {
        var current = WindowsIdentity.GetCurrent();
        var windowsPrincipal = new WindowsPrincipal(current);
        return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
    }
#endif
}