using System.Runtime.Versioning;
using Microsoft.Win32;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>Windows自启动</summary>
/// <remarks>
/// 桌面登录自启动
/// </remarks>
public class WindowsAutorun : DefaultHost
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
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description"></param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean Install(String serviceName, String displayName, String fileName, String arguments, String description)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", GetType().Name, serviceName, displayName, fileName, arguments, description);

        if (!WindowsService.IsAdministrator()) return WindowsService.RunAsAdministrator("-i");

        //// 当前用户的启动目录
        //var dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        //if (dir.IsNullOrEmpty()) return false;

        //dir.EnsureDirectory(false);

#if NET40_OR_GREATER || NET5_0_OR_GREATER
        // 在注册表中写入启动配置
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", true);

        // 添加应用程序到自启动项
        key.SetValue(serviceName, $"{fileName} {arguments}");
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

        if (!WindowsService.IsAdministrator()) return WindowsService.RunAsAdministrator("-uninstall");

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
}