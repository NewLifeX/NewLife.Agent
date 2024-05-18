using NewLife.Model;

namespace NewLife.Agent.Command;

/// <summary>
/// 安装服务命令处理类
/// </summary>
public class InstallCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 安装服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public InstallCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Install;

    /// <inheritdoc />
    public override String Description { get; set; } = "安装并启动服务";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; } = '2';

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return !Service.Host.IsInstalled(Service.ServiceName);
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Install();
        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }

    private void Install()
    {
        var exe = GetExeName();

        // 兼容dotnet
        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 1)
        {
            var fileName = Path.GetFileName(exe);
            if (exe.Contains(' ')) exe = $"\"{exe}\"";

            var dll = args[0].GetFullPath();
            if (dll.Contains(' ')) dll = $"\"{dll}\"";

            if (fileName.EqualIgnoreCase("dotnet", "dotnet.exe", "java"))
                exe += " " + dll;
            else if (fileName.EqualIgnoreCase("mono", "mono.exe", "mono-sgen"))
                exe = dll;
        }

        //var arg = UseAutorun ? "-run" : "-s";
        var arg = "-s";

        // 兼容更多参数做为服务启动，譬如：--urls
        if (args.Length > 2)
        {
            // 跳过系统内置参数
            var list = new List<String>();
            for (var i = 2; i < args.Length; i++)
            {
                if (args[i].EqualIgnoreCase("-server", "-user", "-group"))
                    i++;
                else if (args[i].Contains(' '))
                    list.Add($"\"{args[i]}\"");
                else
                    list.Add(args[i]);
            }
            if (list.Count > 0) arg += " " + list.Join(" ");
        }

        Service.Host.Install(Service.ServiceName, Service.DisplayName, exe, arg, Description);
    }

    /// <summary>Exe程序名</summary>
    public virtual String GetExeName()
    {
        var p = System.Diagnostics.Process.GetCurrentProcess();
        var filename = p.MainModule.FileName;
        //filename = Path.GetFileName(filename);
        filename = filename.Replace(".vshost.", ".");

        return filename;
    }

}