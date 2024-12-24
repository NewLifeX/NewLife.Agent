using NewLife.Agent.Command;
using NewLife.Agent.Models;

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
        //if (args == null || args.Length == 0) args = Environment.GetCommandLineArgs();
        // 外部传入的args来自Main方法，不包含dll参数。
        // 参考：https://newlifex.com/core/command_line_args
        // 参考：https://newlifex.com/tech/dotnet_args
        var args2 = Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0) args = args2;
        if (args2 != null && args2.Length >= 1)
        {
            var fileName = Path.GetFileName(exe);
            if (exe.Contains(' ')) exe = $"\"{exe}\"";

            var dll = args2[0].GetFullPath();
            if (!dll.Contains(".dll"))//没有获得到主程的dll
            {
                dll = Environment.CommandLine?.Split(' ')[0];//Assembly.GetExecutingAssembly().Location;
            }

            if (dll.Contains(' ')) dll = $"\"{dll}\"";

            if (fileName.IsRuntime())
                exe += " " + dll;
            else if (fileName.EqualIgnoreCase("mono", "mono.exe", "mono-sgen"))
                exe = dll;
        }

        var service = new ServiceModel
        {
            ServiceName = Service.ServiceName,
            DisplayName = Service.DisplayName,
            Description = Service.Description,
            FileName = exe
        };

        // 从文件名中分析工作目录
        if (service.WorkingDirectory.IsNullOrEmpty())
            service.WorkingDirectory = service.FileName.GetWorkingDirectory(service.Arguments);

        //var arg = UseAutorun ? "-run" : "-s";
        var arg = "-s";

        // 兼容更多参数做为服务启动，譬如：--urls
        if (args.Length > 1)
        {
            // 跳过系统内置参数
            var list = new List<String>();
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i].EqualIgnoreCase("-server", "-user", "-group"))
                    i++;
                else if (args[i].Contains(' '))
                    list.Add($"\"{args[i]}\"");
                else
                    list.Add(args[i]);
            }
            if (list.Count > 0) arg += " " + list.Join(" ");

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].EqualIgnoreCase("-user") && i + 1 < args.Length)
                {
                    service.User = args[i + 1];
                }
                if (args[i].EqualIgnoreCase("-group") && i + 1 < args.Length)
                {
                    service.Group = args[i + 1];
                }
            }
        }

        service.Arguments = arg;
        //Service.Host.Install(Service.ServiceName, Service.DisplayName, exe, arg, Service.Description);
        Service.Host.Install(service);

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