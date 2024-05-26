using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 安装服务命令处理类
/// </summary>
public class Install : BaseCommandHandler
{
    /// <summary>
    /// 安装服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Install(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Install;
        Description = "安装服务";
        ShortcutKey = '2';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => !Service.Host.IsInstalled(Service.ServiceName);

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        var exe = GetExeName();

        // 兼容dotnet
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
                if (args[i].EqualIgnoreCase("-server", "-user", "-group"))
                    i++;
                else if (args[i].Contains(' '))
                    list.Add($"\"{args[i]}\"");
                else
                    list.Add(args[i]);
            if (list.Count > 0) arg += " " + list.Join(" ");
        }

        Service.Host.Install(Service.ServiceName, Service.DisplayName, exe, arg, Service.Description);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
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