using System.Diagnostics;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>MacOSX系统主机</summary>
public class OSXLaunch : DefaultHost
{
    #region 静态
    private static String _path;

    /// <summary>是否可用</summary>
    public static Boolean Available => !_path.IsNullOrEmpty();

    /// <summary>实例化</summary>
    static OSXLaunch()
    {
        var ps = new[] {
            "~/Library/LaunchAgents/",
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

    String GetFileName(String serviceName) => GetFileName(serviceName, _path);
    static String GetFileName(String serviceName, String basePath) => basePath.CombinePath($"{serviceName}.plist");

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsInstalled(String serviceName) => File.Exists(GetFileName(serviceName));

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsRunning(String serviceName)
    {
        var file = GetFileName(serviceName);
        if (!File.Exists(file)) return false;

        var str = Execute("launchctl", $"status {serviceName}", false);
        if (!str.IsNullOrEmpty() && str.Contains("running")) return true;

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

        return Install(_path, serviceName, displayName, fileName, arguments, description, User, Group, DependOnNetwork);
    }

    static String _template = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>{$Name}</string>
            <key>ProgramArguments</key>
            <array>
                <string>{$FileName}</string>
                <string>{$Arguments}</string>
            </array>
            <key>RunAtLoad</key>
            <true/>
        </dict>
        </plist>

        """;

    /// <summary>安装服务</summary>
    /// <param name="basePath">systemd目录有</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <param name="user">用户</param>
    /// <param name="group">用户组</param>
    /// <param name="network"></param>
    /// <returns></returns>
    public static Boolean Install(String basePath, String serviceName, String displayName, String fileName, String arguments, String description, String user, String group, Boolean network)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", typeof(OSXLaunch).Name, serviceName, displayName, fileName, arguments, description);

        var file = GetFileName(serviceName, basePath);
        XTrace.WriteLine(file);

        //var des = !displayName.IsNullOrEmpty() ? displayName : description;

        var str = _template.Replace("{$Name}", serviceName)
            .Replace("{$FileName}", fileName)
            .Replace("{$Arguments}", arguments);

        File.WriteAllText(file, str);

        Process.Start("launchctl", $"load {file}");

        return true;
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

        var file = GetFileName(serviceName);
        if (File.Exists(file))
        {
            Process.Start("launchctl", $"unload {file}");

            File.Delete(file);

            return true;
        }

        return false;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

        return Process.Start("launchctl", $"start {serviceName}") != null;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", GetType().Name, serviceName);

        return Process.Start("launchctl", $"stop {serviceName}") != null;
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", GetType().Name, serviceName);

        Process.Start("launchctl", $"stop {serviceName}");
        Process.Start("launchctl", $"start {serviceName}");

        return true;
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
        var file = GetFileName(serviceName);
        if (!File.Exists(file)) return null;

        var txt = File.ReadAllText(file);
        if (txt != null)
        {
            //var dic = txt.SplitAsDictionary("=", "\n", true);

            var cfg = new ServiceConfig { Name = serviceName };
            //if (dic.TryGetValue("ExecStart", out var str)) cfg.FilePath = str.Trim();
            //if (dic.TryGetValue("WorkingDirectory", out str)) cfg.FilePath = str.Trim().CombinePath(cfg.FilePath);
            //if (dic.TryGetValue("Description", out str)) cfg.DisplayName = str.Trim();
            //if (dic.TryGetValue("Restart", out str)) cfg.AutoStart = !str.Trim().EqualIgnoreCase("no");

            return cfg;
        }

        return null;
    }
}
