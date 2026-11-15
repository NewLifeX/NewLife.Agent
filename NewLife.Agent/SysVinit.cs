using System.Diagnostics;
using NewLife.Agent.Models;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>SysVinit版进程守护</summary>
public class SysVinit : DefaultHost
{
    /// <summary>服务路径</summary>
    public static String ServicePath { get; set; }

    /// <summary>是否可用</summary>
    public static Boolean Available => !ServicePath.IsNullOrEmpty();

    /// <summary>相关目录</summary>
    private readonly static String[] _paths = [
        "/etc/init.d",
    ];

    static SysVinit()
    {
        foreach (var p in _paths)
        {
            if (Directory.Exists(p))
            {
                ServicePath = p;
                break;
            }
        }
    }

    /// <summary>实例化</summary>
    public SysVinit() => Name = "sysVinit";

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>服务是否已安装</returns>
    public override Boolean IsInstalled(String serviceName)
    {
        var file = GetServicePath(serviceName);
        return file != null;
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>服务是否已启动</returns>
    public override Boolean IsRunning(String serviceName)
    {
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        var status = $"{ServicePath}/{serviceName}".Execute("status", 3_000);

        return IsRunningStatus(status);
    }

    /// <summary>从 status 命令输出判断服务是否运行中</summary>
    /// <remarks>
    /// 旧实现直接对整个输出做 Contains("running")，而停服时输出“not running”同样包含该子串，
    /// 导致状态判断反转（看门狗/状态页认为已停止的服务仍在运行）。改为逐行判断并排除否定行。
    /// </remarks>
    /// <param name="status">status 命令输出</param>
    /// <returns>是否运行中</returns>
    internal static Boolean IsRunningStatus(String status)
    {
        if (status.IsNullOrEmpty()) return false;

        foreach (var line in status.Split('\n'))
        {
            var text = line.Trim();
            if (text.Length == 0) continue;
            if (text.IndexOf("not running", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (text.IndexOf("running", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>安装服务</summary>
    /// <param name="service">服务</param>
    /// <returns>是否安装成功</returns>
    public override Boolean Install(ServiceModel service)
    {
        //暂不实现SysVinit服务安装
        throw new NotImplementedException(L.T("SysVinit installation is not implemented."));
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否卸载成功</returns>
    public override Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", Name, serviceName);

        var file = GetServicePath(serviceName);
        if (file == null) return false;

        // 先让系统工具清理（部分发行版会顺带处理 rcS 等），但它的输出文案随 locale 变化，不能作为成功依据
        try { "update-rc.d".Execute($"-f {serviceName} remove", 3_000); } catch { }

        // 直接删除已知的启动/停止链接并核对，避免非英文 locale 下明明删掉了却报失败
        var dirs = new[] { "/etc/rc.d", "/etc/rc0.d", "/etc/rc1.d", "/etc/rc2.d", "/etc/rc3.d", "/etc/rc4.d", "/etc/rc5.d", "/etc/rc6.d" };
        var failed = false;
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var prefix in new[] { "S50", "K50" })
            {
                var link = Path.Combine(dir, prefix + serviceName);
                try
                {
                    if (File.Exists(link) || Directory.Exists(link)) File.Delete(link);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                    failed = true;
                }
            }
        }

        return !failed;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否启动成功</returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", Name, serviceName);
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return RunScript(file, "start");
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否停止成功</returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", Name, serviceName);
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return RunScript(file, "stop");
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", Name, serviceName);
        var file = GetServicePath(serviceName);
        if (file == null) return false;

        return RunScript(file, "restart");
    }

    /// <summary>执行 init 脚本并检查退出码</summary>
    /// <param name="file">脚本路径</param>
    /// <param name="command">start/stop/restart</param>
    /// <returns>命令是否成功</returns>
    private static Boolean RunScript(String file, String command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(10_000)) return true;

            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            return false;
        }
    }

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    public override ServiceConfig QueryConfig(String serviceName)
    {
        var file = GetServicePath(serviceName);
        if (file == null) return null;

        //  /etc/init.d 目录下的服务配置文件不是systemd格式，特殊处理
        // systemd-sysv-generator 在系统启动时自动运行，它会扫描 /etc/init.d 目录，
        // 并为每个脚本生成一个对应的 systemd 服务单元文件。
        // 这些生成的单元文件存放在 /run/systemd/generator.late/ 目录中。
        file = "/run/systemd/generator.late".CombinePath($"{serviceName}.service");
        if (!File.Exists(file))
        {
            return null;
        }

        var txt = File.ReadAllText(file);
        if (txt != null)
        {
            var dic = txt.SplitAsDictionary("=", "\n", true);

            var cfg = new ServiceConfig { Name = serviceName };
            if (dic.TryGetValue("ExecStart", out var str)) cfg.FilePath = str.Trim();
            if (dic.TryGetValue("WorkingDirectory", out str)) cfg.FilePath = str.Trim().CombinePath(cfg.FilePath);
            if (dic.TryGetValue("Description", out str)) cfg.DisplayName = str.Trim();
            if (dic.TryGetValue("Restart", out str)) cfg.AutoStart = !str.Trim().EqualIgnoreCase("no");

            return cfg;
        }

        return null;
    }

    /// <summary>
    /// 获取服务配置文件的路径
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>服务配置文件路径，不存在时返回 null</returns>
    public static String GetServicePath(String serviceName)
    {
        // 不存在 /etc/init.d（如非 Linux 环境）时安全返回，避免 Path.Combine 传入空路径抛异常
        if (ServicePath.IsNullOrEmpty() || serviceName.IsNullOrEmpty()) return null;

        var file = Path.Combine(ServicePath, serviceName);
        return File.Exists(file) ? file : null;
    }
}
