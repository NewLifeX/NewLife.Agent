using System.Diagnostics;
using System.Security;
using System.Text;
using NewLife.Agent.Models;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>MacOSX系统主机</summary>
public class OSXLaunch : DefaultHost
{
    #region 静态
    private static readonly String _path;

    /// <summary>是否可用</summary>
    /// <value>是否可用</value>
    public static Boolean Available => !_path.IsNullOrEmpty();

    /// <summary>实例化</summary>
    static OSXLaunch()
    {
        // “~” 不会被运行时展开：直接判断 "~/Library/LaunchAgents/" 永远为 false，
        // 导致面板把 plist 写进进程当前目录，登录自启动不生效
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.IsNullOrEmpty()) home = Environment.GetEnvironmentVariable("HOME");
        if (home.IsNullOrEmpty()) return;

        var dir = Path.Combine(home, "Library", "LaunchAgents");
        if (Directory.Exists(dir)) _path = dir;
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
    /// <returns>服务是否已安装</returns>
    public override Boolean IsInstalled(String serviceName) => File.Exists(GetFileName(serviceName));

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>服务是否已启动</returns>
    public override Boolean IsRunning(String serviceName)
    {
        var file = GetFileName(serviceName);
        if (!File.Exists(file)) return false;

        var str = "launchctl".Execute($"status {serviceName}", 3_000);
        if (!str.IsNullOrEmpty() && str.Contains("running")) return true;

        return false;
    }

    /// <summary>安装服务</summary>
    /// <param name="service">服务</param>
    /// <returns>是否安装成功</returns>
    public override Boolean Install(ServiceModel service)
    {
        var serviceName = service.ServiceName;
        var displayName = service.DisplayName;
        var description = service.Description;
        var fileName = service.FileName;
        var arguments = service.Arguments;

        if (!service.User.IsNullOrEmpty()) User = service.User;
        if (!service.Group.IsNullOrEmpty()) Group = service.Group;
        if (!User.IsNullOrEmpty() && Group.IsNullOrEmpty()) Group = User;

        return Install(_path, serviceName, displayName, fileName, arguments, description, User, Group, DependOnNetwork);
    }

    /// <summary>生成 LaunchAgent plist 内容</summary>    /// <remarks>
    /// 所有插入值均做 XML 转义，否则参数或路径含 &amp; &lt; &gt; 时 plist 非法，launchctl 会拒绝加载。
    /// </remarks>
    /// <param name="serviceName">服务名</param>
    /// <param name="fileName">可执行文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="user">运行用户，为空则不输出 UserName</param>
    /// <param name="network">是否依赖网络就绪</param>
    /// <returns>plist XML 文本</returns>
    public static String BuildPlist(String serviceName, String fileName, String arguments, String user = null, Boolean network = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        sb.AppendLine("<plist version=\"1.0\">");
        sb.AppendLine("<dict>");
        sb.AppendLine("    <key>Label</key>");
        sb.AppendLine($"    <string>{Escape(serviceName)}</string>");
        sb.AppendLine("    <key>ProgramArguments</key>");
        sb.AppendLine("    <array>");
        sb.AppendLine($"        <string>{Escape(fileName)}</string>");
        // launchd 把数组元素逐个作为 argv 传入：参数必须拆成独立 <string>，
        // 否则 "a b" 会作为一个参数，多参数服务无法启动
        if (!arguments.IsNullOrEmpty())
        {
            foreach (var token in ServiceHelper.ParseArguments(arguments))
            {
                sb.AppendLine($"        <string>{Escape(token)}</string>");
            }
        }
        sb.AppendLine("    </array>");
        sb.AppendLine("    <key>RunAtLoad</key>");
        sb.AppendLine("    <true/>");

        if (!user.IsNullOrEmpty())
        {
            sb.AppendLine("    <key>UserName</key>");
            sb.AppendLine($"    <string>{Escape(user)}</string>");
        }

        // 依赖网络时用 NetworkState 表达，否则无条件保活
        sb.AppendLine("    <key>KeepAlive</key>");
        if (network)
        {
            sb.AppendLine("    <dict>");
            sb.AppendLine("        <key>NetworkState</key>");
            sb.AppendLine("        <true/>");
            sb.AppendLine("    </dict>");
        }
        else
        {
            sb.AppendLine("    <true/>");
        }

        sb.AppendLine("</dict>");
        sb.AppendLine("</plist>");

        return sb.ToString();
    }

    /// <summary>XML 转义</summary>
    /// <param name="value">原始值</param>
    /// <returns>可用于 plist 文本的值</returns>
    private static String Escape(String value) => value.IsNullOrEmpty() ? "" : SecurityElement.Escape(value);

    /// <summary>安装服务</summary>
    /// <param name="basePath">LaunchAgents 目录</param>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <param name="user">用户</param>
    /// <param name="group">用户组</param>
    /// <param name="network">是否联网</param>
    /// <returns>是否安装成功</returns>
    public static Boolean Install(String basePath, String serviceName, String displayName, String fileName, String arguments, String description, String user, String group, Boolean network)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", typeof(OSXLaunch).Name, serviceName, displayName, fileName, arguments, description);

        var file = GetFileName(serviceName, basePath);
        XTrace.WriteLine(file);

        //var des = !displayName.IsNullOrEmpty() ? displayName : description;

        var str = BuildPlist(serviceName, fileName, arguments, user, network);

        try
        {
            // 首次安装时 LaunchAgents 目录可能不存在
            Directory.CreateDirectory(basePath);
            File.WriteAllText(file, str);

            using var p = Process.Start("launchctl", $"load \"{file}\"");
            p?.WaitForExit(5_000);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return false;
        }

        return true;
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否卸载成功</returns>
    public override Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

        var file = GetFileName(serviceName);
        if (File.Exists(file))
        {
            Process.Start("launchctl", $"unload \"{file}\"");

            File.Delete(file);

            return true;
        }

        return false;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否启动成功</returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

        return Process.Start("launchctl", $"start {serviceName}") != null;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否停止成功</returns>
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
