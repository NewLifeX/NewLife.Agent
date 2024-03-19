using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>Linux版进程守护</summary>
public class RcInit : DefaultHost
{
    #region 静态
    private static readonly String _path;

    /// <summary>是否可用</summary>
    public static Boolean Available => !_path.IsNullOrEmpty();

    /// <summary>实例化</summary>
    static RcInit()
    {
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
        var p = GetProcess(serviceName, out _);

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
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", typeof(RcInit).Name, serviceName, displayName, fileName, arguments, description);

        //var file = systemdPath.CombinePath($"{serviceName}");
        var file = $"{serviceName}.sh".GetFullPath();
        XTrace.WriteLine(file);

        var des = !displayName.IsNullOrEmpty() ? displayName : description;

        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine("# chkconfig: 2345 10 90");
        sb.AppendLine($"# description: {des}");

        sb.AppendLine();
        //sb.AppendLine($"cd {".".GetFullPath()}");
        sb.AppendLine("case \"$1\" in");
        sb.AppendLine("  start)");
        sb.AppendLine($"        nohup {fileName} {arguments} >/dev/null 2>&1 &");
        sb.AppendLine("        ;;");
        sb.AppendLine("  stop)");
        sb.AppendLine($"        {fileName} {arguments.TrimEnd("-s")} -stop");
        sb.AppendLine("        ;;");
        sb.AppendLine("  restart)");
        sb.AppendLine($"        $0 stop");
        sb.AppendLine($"        $0 start");
        sb.AppendLine("        ;;");
        sb.AppendLine("  *)");
        sb.AppendLine("        echo \"Usage: $0 {start|stop|restart}\"");
        sb.AppendLine("        exit 1");
        sb.AppendLine("esac");
        sb.AppendLine();
        sb.AppendLine($"exit $?");

        //File.WriteAllText(file, sb.ToString());
        //File.WriteAllBytes(file, sb.ToString().GetBytes());

        //!! init.d目录中的rcS会扫描当前目录所有S开头的文件并执行，刚好StarAgent命中，S50StarAgent也命中，造成重复执行
        // 因此把启动引导脚本放在当前目录，如StarAgent.sh，然后在rc.d目录中创建链接文件
        File.WriteAllBytes(file, sb.ToString().GetBytes());

        // 给予可执行权限
        Process.Start("chmod", $"+x {file}");

        var flag = false;

        // 创建链接文件
        for (var i = 0; i < 7; i++)
        {
            var dir = $"/etc/rc{i}.d/";
            if (Directory.Exists(dir))
            {
                if (i is 0 or 1 or 6)
                    CreateLink(file, $" {dir}K50{serviceName}");
                else
                    CreateLink(file, $" {dir}S50{serviceName}");

                flag = true;
            }
        }
        // OpenWrt
        {
            var dir = "/etc/rc.d/";
            if (Directory.Exists(dir))
            {
                CreateLink(file, $"{dir}S50{serviceName}");

                flag = true;
            }
        }

        // init.d 目录存在其它Sxx文件时，才创建同级链接文件
        if (!flag)
        {
            // 创建同级链接文件 [解决某些linux启动必须以Sxx开头的启动文件]
            var fis = systemPath.AsDirectory().GetFiles();
            if (fis.Any(e => e.Name[0] == 'S'))
            {
                CreateLink(file, $"{systemPath}/S50{serviceName}");
                //if (fis.Any(e => e.Name[0] == 'K'))
                CreateLink(file, $"{systemPath}/K50{serviceName}");
            }
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

        //var file = _path.CombinePath($"{serviceName}");
        var file = $"{serviceName}.sh".GetFullPath();
        if (File.Exists(file)) File.Delete(file);

        // 兼容旧版，删除同级文件，如/etd/init.d/StarAgent
        file = _path.CombinePath($"{serviceName}");
        if (File.Exists(file)) File.Delete(file);

        // 删除同级链接文件
        file = _path.CombinePath($"S50{serviceName}");
        if (File.Exists(file)) File.Delete(file);
        file = _path.CombinePath($"K50{serviceName}");
        if (File.Exists(file)) File.Delete(file);

        // 删除链接文件
        for (var i = 0; i < 7; i++)
        {
            var dir = $"/etc/rc{i}.d/";
            if (Directory.Exists(dir))
            {
                file = $"{dir}S50{serviceName}";
                if (File.Exists(file)) File.Delete(file);

                file = $"{dir}K50{serviceName}";
                if (File.Exists(file)) File.Delete(file);
            }
        }
        // OpenWrt
        {
            var dir = "/etc/rc.d/";
            if (Directory.Exists(dir))
            {
                file = dir.CombinePath($"S50{serviceName}");
                if (File.Exists(file)) File.Delete(file);
            }
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
        var p = GetProcess(serviceName, out _);
        if (p != null && !GetHasExited(p)) return false;

        //var file = _path.CombinePath($"{serviceName}");
        var file = $"{serviceName}.sh".GetFullPath();
        if (!File.Exists(file)) return false;

        //Process.Start("bash", file);
        //file.ShellExecute("start");
        Process.Start(new ProcessStartInfo("sh", $"{file} start") { UseShellExecute = true });

        return true;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", Name, serviceName);

        var p = GetProcess(serviceName, out var pid);
        if (p == null) return false;

        try
        {
            // 发命令让服务自己退出
            "kill".ShellExecute($"{p.Id}");

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

    private Process GetProcess(String serviceName, out String pid)
    {
        var id = 0;
        pid = $"{serviceName}.pid".GetFullPath();
        if (File.Exists(pid)) id = File.ReadAllText(pid).Trim().ToInt();
        if (id <= 0) return null;

        var p = GetProcessById(id);
        //if (p == null || GetHasExited(p)) return null;

        return p;
    }
}