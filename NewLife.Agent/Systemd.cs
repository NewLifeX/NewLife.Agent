using System.Diagnostics;
using System.Text;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>systemd版进程守护</summary>
/// <remarks>
/// ServiceBase子类应用可重载Init后，借助Host修改Systemd配置。
/// </remarks>
public class Systemd : Host
{
    #region 静态
    private static String _path;
    //private ServiceBase _service;

    /// <summary>是否可用</summary>
    public static Boolean Available => !_path.IsNullOrEmpty();

    /// <summary>实例化</summary>
    static Systemd()
    {
        var ps = new[] {
            "/etc/systemd/system",
            "/lib/systemd/system",
            "/run/systemd/system",
            "/usr/lib/systemd/system",
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
    /// <summary>用于执行服务的用户</summary>
    public String User { get; set; }

    /// <summary>用于执行服务的用户组</summary>
    public String Group { get; set; }

    /// <summary>是否依赖于网络。在网络就绪后才启动服务</summary>
    public Boolean DependOnNetwork { get; set; }
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
        var file = _path.CombinePath($"{serviceName}.service");

        return File.Exists(file);
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsRunning(String serviceName)
    {
        var file = _path.CombinePath($"{serviceName}.service");
        if (!File.Exists(file)) return false;

        var str = Execute("systemctl", $"status {serviceName}", false);
        if (!str.IsNullOrEmpty() && str.Contains("running")) return true;

        return false;
    }

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="binPath">文件路径</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public override Boolean Install(String serviceName, String displayName, String binPath, String description)
    {
        if (User.IsNullOrEmpty())
        {
            // 从命令行参数加载用户设置 -user
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 1)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].EqualIgnoreCase("-user") && i + 1 < args.Length)
                    {
                        User = args[i + 1];
                        break;
                    }
                    if (args[i].EqualIgnoreCase("-group") && i + 1 < args.Length)
                    {
                        Group = args[i + 1];
                        break;
                    }
                }
                if (!User.IsNullOrEmpty() && Group.IsNullOrEmpty()) Group = User;
            }
        }

        return Install(_path, serviceName, displayName, binPath, description, User, Group, DependOnNetwork);
    }

    /// <summary>安装服务</summary>
    /// <param name="systemdPath">systemd目录有</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="binPath">文件路径</param>
    /// <param name="description">描述信息</param>
    /// <param name="user">用户</param>
    /// <param name="group">用户组</param>
    /// <param name="network"></param>
    /// <returns></returns>
    public static Boolean Install(String systemdPath, String serviceName, String displayName, String binPath, String description, String user, String group, Boolean network)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", typeof(Systemd).Name, serviceName, displayName, binPath, description);

        var file = systemdPath.CombinePath($"{serviceName}.service");
        XTrace.WriteLine(file);

        //var asm = Assembly.GetEntryAssembly();
        var des = !displayName.IsNullOrEmpty() ? displayName : description;

        var sb = new StringBuilder();
        sb.AppendLine("[Unit]");
        sb.AppendLine($"Description={des}");

        if (network)
            sb.AppendLine($"After=network.target");
        //sb.AppendLine("StartLimitIntervalSec=0");

        sb.AppendLine();
        sb.AppendLine("[Service]");
        sb.AppendLine("Type=simple");
        //sb.AppendLine($"ExecStart=/usr/bin/dotnet {asm.Location}");
        sb.AppendLine($"ExecStart={binPath}");
        sb.AppendLine($"WorkingDirectory={".".GetFullPath()}");
        if (!user.IsNullOrEmpty()) sb.AppendLine($"User={user}");
        if (!group.IsNullOrEmpty()) sb.AppendLine($"Group={group}");

        // no 表示服务退出时，服务不会自动重启，默认值。
        // on-failure 表示当进程以非零退出代码退出，由信号终止；当操作(如服务重新加载)超时；以及何时触发配置的监视程序超时时，服务会自动重启。
        // always 表示只要服务退出，则服务将自动重启。
        sb.AppendLine("Restart=always");

        // RestartSec 重启间隔，比如某次异常后，等待3(s)再进行启动，默认值0.1(s)
        sb.AppendLine("RestartSec=3");

        // StartLimitInterval: 无限次重启，默认是10秒内如果重启超过5次则不再重启，设置为0表示不限次数重启
        sb.AppendLine("StartLimitInterval=0");

        sb.AppendLine("KillSignal=SIGINT");

        sb.AppendLine();
        sb.AppendLine("[Install]");
        sb.AppendLine("WantedBy=multi-user.target");

        File.WriteAllText(file, sb.ToString());

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
        XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

        var file = _path.CombinePath($"{serviceName}.service");
        if (File.Exists(file)) File.Delete(file);

        return true;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

        return Process.Start("systemctl", $"start {serviceName}") != null;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", GetType().Name, serviceName);

        return Process.Start("systemctl", $"stop {serviceName}") != null;
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", GetType().Name, serviceName);

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
        var file = _path.CombinePath($"{serviceName}.service");
        if (!File.Exists(file)) return null;

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
}