using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NewLife.Agent.Command;
using NewLife.Agent.Models;
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

        var pid = $"{service.ServiceName}.pid".GetFullPath();
        try
        {
            // 用pid文件记录进程id，方便后面杀进程
            using (var p = Process.GetCurrentProcess())
            {
                File.WriteAllText(pid, p.Id.ToString());
            }

            // 启动初始化
            service.StartLoop();

            // 阻塞
            service.DoLoop();

            // 停止
            service.StopLoop();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
        finally
        {
            // 异常退出时同样要清理 pid 文件，避免残留 PID 被误判为运行中的服务
            try { File.Delete(pid); } catch { }
        }
    }

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否已安装</returns>
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
    /// <returns>服务是否已启动</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean IsRunning(String serviceName)
    {
        using var p = GetProcess(serviceName, out _);

        return p != null && !GetHasExited(p);
    }

    /// <summary>安装服务</summary>
    /// <param name="service">服务</param>
    /// <returns>是否安装成功</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean Install(ServiceModel service)
    {
        var serviceName = service.ServiceName;
        var displayName = service.DisplayName;
        var description = service.Description;
        var fileName = service.FileName;
        var arguments = service.Arguments;
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", GetType().Name, serviceName, displayName, fileName, arguments, description);

        if (!WindowsService.IsAdministrator()) return WindowsService.RunAsAdministrator(WindowsService.BuildElevationCommand(CommandConst.Install));

        //// 当前用户的启动目录
        //var dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        //if (dir.IsNullOrEmpty()) return false;

        //dir.EnsureDirectory(false);

#if NET40_OR_GREATER || NET5_0_OR_GREATER
        // 在注册表中写入启动配置
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", true);
        if (key == null)
        {
            XTrace.WriteLine("Failed to open the autorun registry key; installation aborted.");
            return false;
        }

        // 路径可能包含空格，必须加引号，否则登录自启动会把路径截断；
        // FileName 也可能是 dotnet + dll 的复合命令行，此时已是合法命令文本，不能整串再包引号
        key.SetValue(serviceName, $"{ServiceHelper.QuotePath(fileName)} {arguments}".TrimEnd());
#endif

        return true;
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否卸载成功</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

        if (!WindowsService.IsAdministrator()) return WindowsService.RunAsAdministrator(WindowsService.BuildElevationCommand(CommandConst.Uninstall));

#if NET40_OR_GREATER || NET5_0_OR_GREATER
        // 在注册表中写入启动配置
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", true);
        if (key == null) return false;

        key.DeleteValue(serviceName, false);
#endif

        return true;
    }

    /// <summary>查询服务配置，从注册表自启动项中读取</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>服务配置，未找到时返回 null</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe ServiceConfig QueryConfig(String serviceName)
    {
#if NET40_OR_GREATER || NET5_0_OR_GREATER
        using var key = Registry.LocalMachine.OpenSubKey("""SOFTWARE\Microsoft\Windows\CurrentVersion\Run""", false);
        if (key == null) return null;

        var v = key.GetValue(serviceName)?.ToString();
        if (v.IsNullOrEmpty()) return null;

        // 注册表值可能是 "带空格路径" + 参数，必须按引号规则拆分，不能简单按首个空格截断
        var parts = ServiceHelper.SplitCommandLine(v);
        if (parts.Length == 0) return null;

        var fileName = parts[0];
        var args = parts.Length > 1 ? parts[1] : "";

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
    /// <returns>是否启动成功</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

        // 判断服务是否已启动
        using (var p = GetProcess(serviceName, out _))
        {
            if (p != null && !GetHasExited(p)) return false;
        }

        if (!WindowsService.IsAdministrator()) return WindowsService.RunAsAdministrator(WindowsService.BuildElevationCommand(CommandConst.Start));

        var config = QueryConfig(serviceName);
        if (config == null || config.FilePath.IsNullOrEmpty() || !File.Exists(config.FilePath))
        {
            XTrace.WriteLine("Service {0} was not found; is it installed?", serviceName);
            return false;
        }

        //var process = Process.Start(fileName, args);
        Process.Start(new ProcessStartInfo(config.FilePath, config.Arguments) { UseShellExecute = true });

        return true;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否停止成功</returns>
#if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
#endif
    public override unsafe Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", GetType().Name, serviceName);

        using var p = GetProcess(serviceName, out var pid);
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