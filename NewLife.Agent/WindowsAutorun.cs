using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NewLife.Agent.Windows;
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
    public override void Run(ServiceBase service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        // 以服务运行
        InService = true;

        try
        {
            // 用pid文件记录进程id，方便后面杀进程
            var p = Process.GetCurrentProcess();
            var pid = $"{service.ServiceName}.pid".GetFullPath();
            File.WriteAllText(pid, p.Id.ToString());

            // 启动初始化
            service.StartLoop();

            // 阻塞
            service.DoLoop();

            // 停止
            service.StopLoop();

            File.Delete(pid);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean IsInstalled(String serviceName)
    {
        var config = QueryConfig(serviceName);
        return config != null && !config.FilePath.IsNullOrEmpty();
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe Boolean IsRunning(String serviceName)
    {
        var p = GetProcess(serviceName, out _);

        return p != null && !GetHasExited(p);
    }

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

        var fileName = v;
        var args = "";
        var idx = fileName.IndexOf(' ');
        if (idx > 0)
        {
            args = fileName.Substring(idx + 1);
            fileName = fileName.Substring(0, idx);
        }

        return new ServiceConfig
        {
            Name = serviceName,
            FilePath = fileName,
            Arguments = args,
            Command = v,
            AutoStart = true,
        };
#else
        return null;
#endif
    }

#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    private Process GetProcess(String serviceName, out String pid)
    {
        var config = QueryConfig(serviceName);
        var basePath = config?.FilePath;

        var id = 0;
        pid = $"{serviceName}.pid";

        // 在服务目录下查找pid文件，如果没有则在当前目录下查找
        if (!basePath.IsNullOrEmpty()) pid = Path.GetDirectoryName(basePath).CombinePath(pid);
        pid = pid.GetFullPath();
        if (File.Exists(pid)) id = File.ReadAllText(pid).Trim().ToInt();
        if (id <= 0) return null;

        var p = GetProcessById(id);
        //if (p == null || GetHasExited(p)) return null;

        return p;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

        // 判断服务是否已启动
        var p = GetProcess(serviceName, out _);
        if (p != null && !GetHasExited(p)) return false;

        if (!WindowsService.IsAdministrator()) return WindowsService.RunAsAdministrator("-start");

        var config = QueryConfig(serviceName);
        if (config == null || config.FilePath.IsNullOrEmpty() || !File.Exists(config.FilePath))
        {
            XTrace.WriteLine("未找到服务 {0}", serviceName);
            return false;
        }

        //var process = Process.Start(fileName, args);
        Process.Start(new ProcessStartInfo(config.FilePath, config.Arguments) { UseShellExecute = true });

        return true;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", GetType().Name, serviceName);

        var p = GetProcess(serviceName, out var pid);
        if (p == null) return false;

        try
        {
            // 发命令让服务自己退出
            try
            {
                var handle = p.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    // 发送关闭消息
                    //NativeMethods.SendMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    NativeMethods.CloseWindow(handle);

                    var n = 30;
                    while (!p.HasExited && n-- > 0) Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteLine(ex.Message);
            }

            if (!p.HasExited) p.Kill();

            File.Delete(pid);

            return true;
        }
        catch { }

        return false;
    }
}