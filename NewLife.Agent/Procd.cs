using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>OpenWRT版procd进程守护</summary>
public class Procd : DefaultHost
{
    #region 静态
    private static readonly String _path;

    /// <summary>是否可用</summary>
    public static Boolean Available => !_path.IsNullOrEmpty();

    /// <summary>实例化</summary>
    static Procd()
    {
        // 获取1号进程的名字，如果是procd，则表示当前系统是OpenWRT
        var process = Process.GetProcessById(1);
        if (process.ProcessName != "procd") return;

        var ps = new[] {
            "/etc/init.d",
            "/etc/rc.d/init.d",
        };
        foreach (var p in ps)
        {
            if (Directory.Exists(p))
            {
                _path = p;
                break;
            }
        }
    }
    #endregion

    #region 属性
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public Procd() => Name = "procd";
    #endregion

    /// <summary>启动服务</summary>
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
    public override Boolean IsInstalled(String serviceName)
    {
        //var file = _path.CombinePath($"{serviceName}");
        var file = $"{serviceName}.sh".GetFullPath();

        return File.Exists(file);
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsRunning(String serviceName)
    {
        var file = $"{serviceName}.pid".GetFullPath();
        if (!File.Exists(file)) return false;

        var pid = File.ReadAllText(file).Trim().ToInt();
        if (pid <= 0) return false;

        var p = GetProcessById(pid);

        return p != null && !GetHasExited(p);
    }

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public override Boolean Install(String serviceName, String displayName, String fileName, String arguments, String description) => Install(_path, serviceName, fileName, arguments, displayName, description);

    /// <summary>安装服务</summary>
    /// <param name="systemPath">system目录</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public static Boolean Install(String systemPath, String serviceName, String fileName, String arguments, String displayName, String description)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", typeof(Procd).Name, serviceName, displayName, fileName, arguments, description);

        var file = $"{serviceName}.sh".GetFullPath();
        XTrace.WriteLine(file);

        var des = !displayName.IsNullOrEmpty() ? displayName : description;

        var sb = new StringBuilder();
        if (File.Exists("/etc/rc.common"))
            sb.AppendLine("#!/bin/sh /etc/rc.common");
        else
            sb.AppendLine("#!/bin/sh");

        sb.AppendLine();
        sb.AppendLine("START=50");
        sb.AppendLine("STOP=50");
        sb.AppendLine("USE_PROCD=1");

        sb.AppendLine();
        sb.AppendLine("start_service() {");
        sb.AppendLine($"  nohup {fileName} {arguments} >/dev/null 2>&1 &");
        sb.AppendLine("  return 0");
        sb.AppendLine("}");

        sb.AppendLine();
        sb.AppendLine("stop_service() {");
        sb.AppendLine($"  {fileName} {arguments.TrimEnd("-s")} -stop");
        sb.AppendLine("  return 0");
        sb.AppendLine("}");

        sb.AppendLine();
        sb.AppendLine("reload_service() {");
        sb.AppendLine($"  {fileName} {arguments.TrimEnd("-s")} -restart");
        sb.AppendLine("  return 0");
        sb.AppendLine("}");

        sb.AppendLine();
        sb.AppendLine("help() {");
        sb.AppendLine("  echo \"Usage: $0 {start|stop|restart}\"");
        sb.AppendLine("  return 0");
        sb.AppendLine("}");

        //!! init.d目录中的rcS会扫描当前目录所有S开头的文件并执行，刚好StarAgent命中，S50StarAgent也命中，造成重复执行
        // 因此把启动引导脚本放在当前目录，如StarAgent.sh，然后在rc.d目录中创建链接文件
        File.WriteAllBytes(file, sb.ToString().GetBytes());

        // 给予可执行权限
        Process.Start("chmod", $"+x {file}");

        // 创建链接文件，OpenWrt
        var dir = "/etc/rc.d/";
        if (Directory.Exists(dir))
        {
            CreateLink(file, $"{dir}S50{serviceName}");
        }

        return true;
    }

    static void CreateLink(String source, String target)
    {
        if (File.Exists(target)) File.Delete(target);

        Process.Start("ln", $"-s {source} {target}");
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", Name, serviceName);

        var file = $"{serviceName}.sh".GetFullPath();
        if (File.Exists(file)) File.Delete(file);

        // 兼容旧版，删除同级文件，如/etd/init.d/StarAgent
        file = _path.CombinePath($"{serviceName}");
        if (File.Exists(file)) File.Delete(file);

        // 删除链接文件，OpenWrt
        var dir = "/etc/rc.d/";
        if (Directory.Exists(dir))
        {
            file = dir.CombinePath($"S50{serviceName}");
            if (File.Exists(file)) File.Delete(file);
        }

        return true;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", Name, serviceName);

        // 判断服务是否已启动
        var id = 0;
        var pid = $"{serviceName}.pid".GetFullPath();
        if (File.Exists(pid)) id = File.ReadAllText(pid).Trim().ToInt();
        if (id > 0)
        {
            var p = GetProcessById(id);
            if (p != null && !GetHasExited(p)) return false;
        }

        var file = $"{serviceName}.sh".GetFullPath();
        if (!File.Exists(file)) return false;

        //Process.Start(new ProcessStartInfo("sh", $"{file} start") { UseShellExecute = true });
        //"sh".ShellExecute($"{file} start");
        file.ShellExecute("start");

        return true;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", Name, serviceName);

        var id = 0;
        var pid = $"{serviceName}.pid".GetFullPath();
        if (File.Exists(pid)) id = File.ReadAllText(pid).Trim().ToInt();
        if (id <= 0) return false;

        // 杀进程
        var p = GetProcessById(id);
        if (p == null || GetHasExited(p)) return false;

        try
        {
            // 发命令让服务自己退出
            "kill".ShellExecute($"{id}");

            var n = 30;
            while (!p.HasExited && n-- > 0) Thread.Sleep(100);

            if (!p.HasExited) p.Kill();

            File.Delete(pid);

            return true;
        }
        catch { }

        return false;
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", Name, serviceName);

        Stop(serviceName);
        Start(serviceName);

        return true;
    }
}