using System.Diagnostics;
using System.Text;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.Agent;

/// <summary>systemd版进程守护</summary>
/// <remarks>
/// ServiceBase子类应用可重载Init后，借助Host修改Systemd配置。
/// </remarks>
public class Systemd : DefaultHost
{
    #region 静态
    /// <summary>路径</summary>
    public static String ServicePath { get; set; }

    /// <summary>是否可用</summary>
    public static Boolean Available => !ServicePath.IsNullOrEmpty();

    //相关目录，可以参考 systemd 目录优先级
    public readonly static string[] SystemdPaths = new[] {
        "/etc/systemd/system",
        "/run/systemd/system",
        "/usr/local/lib/systemd/system",
        "/lib/systemd/system",
        "/usr/lib/systemd/system",
    };

    /// <summary>实例化</summary>
    static Systemd()
    {
        foreach (var p in SystemdPaths)
        {
            if (Directory.Exists(p))
            {
                ServicePath = p;
                break;
            }
        }
    }
    #endregion

    #region 属性
    /// <summary>服务设置。应用于服务安装</summary>
    public SystemdSetting Setting { get; set; } = new();
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public Systemd() => Name = "systemd";
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
    }

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsInstalled(String serviceName)
    {
        var file = GetServicePath(serviceName);
        return file != null;
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsRunning(String serviceName)
    {
        if (!IsInstalled(serviceName)) return false;
        //检查服务状态
        var status = Execute("systemctl", $"show {serviceName} -p SubState", false);
        if (!status.IsNullOrEmpty())
        {
            //大部分服务状态为running时，表示服务已启动
            if (status.Contains("=running"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public override Boolean Install(String serviceName, String displayName, String fileName, String arguments, String description)
    {
        var set = Setting;
        set.ServiceName = serviceName;
        set.DisplayName = displayName;
        set.Description = description;
        set.FileName = fileName;
        set.Arguments = arguments;

        // 从文件名中分析工作目录
        var ss = fileName.Split(" ");
        if (ss.Length >= 2 && ss[0].EndsWithIgnoreCase("dotnet", "java"))
        {
            var file = ss[1];
            if (File.Exists(file))
                set.WorkingDirectory = Path.GetDirectoryName(file).GetFullPath();
            else
                set.WorkingDirectory = ".".GetFullPath();
        }

        if (set.User.IsNullOrEmpty())
        {
            // 从命令行参数加载用户设置 -user
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 1)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].EqualIgnoreCase("-user") && i + 1 < args.Length)
                    {
                        set.User = args[i + 1];
                        break;
                    }
                    if (args[i].EqualIgnoreCase("-group") && i + 1 < args.Length)
                    {
                        set.Group = args[i + 1];
                        break;
                    }
                }
                if (!set.User.IsNullOrEmpty() && set.Group.IsNullOrEmpty()) set.Group = set.User;
            }
        }

        return Install(ServicePath, set);
    }

    /// <summary>安装服务</summary>
    /// <param name="systemdPath">systemd目录有</param>
    /// <param name="set">服务名</param>
    /// <returns></returns>
    public static Boolean Install(String systemdPath, SystemdSetting set)
    {
        XTrace.WriteLine("{0}.Install {1}", typeof(Systemd).Name, set.ToJson());

        var serviceName = set.ServiceName;
        var file = systemdPath.CombinePath($"{serviceName}.service");
        XTrace.WriteLine(file);

        File.WriteAllText(file, set.Build());

        // sudo systemctl daemon-reload
        // sudo systemctl enable StarAgent
        // sudo systemctl start StarAgent

        Process.Start("systemctl", "daemon-reload");
        Process.Start("systemctl", $"enable {serviceName}");
        //Execute("systemctl", $"start {serviceName}");

        return true;
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", Name, serviceName);

        var file = ServicePath.CombinePath($"{serviceName}.service");
        if (File.Exists(file)) File.Delete(file);

        return true;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", Name, serviceName);
        return Process.Start("systemctl", $"start {serviceName}") != null;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", Name, serviceName);
        return Process.Start("systemctl", $"stop {serviceName}") != null;
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", Name, serviceName);

        //if (InService)
        return Process.Start("systemctl", $"restart {serviceName}") != null;
        //else
        //    return Process.Start(Service.GetExeName(), "-run -delay") != null;
    }

    private static String Execute(String cmd, String arguments, Boolean writeLog = true)
    {
        if (writeLog) XTrace.WriteLine("{0} {1}", cmd, arguments);

        var psi = new ProcessStartInfo(cmd, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true
        };
        var process = Process.Start(psi);
        if (!process.WaitForExit(3_000))
        {
            process.Kill();
            return null;
        }

        return process.StandardOutput.ReadToEnd();
    }

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    public override ServiceConfig QueryConfig(String serviceName)
    {
        var file = GetServicePath(serviceName);
        if (file == null) return null;

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
    /// <returns></returns>
    private String GetServicePath(String serviceName)
    {
        foreach (var p in SystemdPaths)
        {
            var file = p.CombinePath($"{serviceName}.service");
            if (File.Exists(file)) return file;
        }
        return null;
    }
}
