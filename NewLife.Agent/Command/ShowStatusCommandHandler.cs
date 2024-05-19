using NewLife.Log;
using NewLife.Reflection;
using System.Reflection;

namespace NewLife.Agent.Command;

/// <summary>
/// 显示状态命令处理类
/// </summary>
public class ShowStatusCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 显示状态构造函数
    /// </summary>
    /// <param name="service"></param>
    public ShowStatusCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.ShowStatus;

    /// <inheritdoc />
    public override String Description { get; set; } = "显示状态";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; } = '1';

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return true;
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Console.WriteLine();
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;

        var name = Service.ServiceName;
        if (name != Service.DisplayName)
            Console.WriteLine("服务：{0}({1})", Service.DisplayName, name);
        else
            Console.WriteLine("服务：{0}", name);
        Console.WriteLine("描述：{0}", Description);
        Console.Write("状态：{0} ", Service.Host.Name);

        String status;
        var installed = Service.Host.IsInstalled(name);
        if (!installed)
            status = "未安装";
        else if (Service.Host.IsRunning(name))
            status = "运行中";
        else
            status = "未启动";

        if (Runtime.Windows) status += $"（{(WindowsService.IsAdministrator() ? "管理员" : "普通用户")}）";

        Console.WriteLine(status);

        // 执行文件路径
        if (installed)
        {
            try
            {
                var cfg = Service.Host.QueryConfig(name);
                if (cfg != null) Console.WriteLine("路径：{0}", cfg.FilePath);
            }
            catch (Exception ex)
            {
                if (XTrace.Log.Level <= LogLevel.Debug) XTrace.Log.Debug("", ex);
            }
        }

        var asm = AssemblyX.Create(Assembly.GetExecutingAssembly());
        Console.WriteLine();
        Console.WriteLine("{0}\t版本：{1}\t发布：{2:yyyy-MM-dd HH:mm:ss}", asm.Name, asm.FileVersion, asm.Compile);

        var asm2 = AssemblyX.Create(Assembly.GetEntryAssembly());
        if (asm2 != asm)
            Console.WriteLine("{0}\t版本：{1}\t发布：{2:yyyy-MM-dd HH:mm:ss}", asm2.Name, asm2.FileVersion, asm2.Compile);

        Console.ForegroundColor = color;
    }
}