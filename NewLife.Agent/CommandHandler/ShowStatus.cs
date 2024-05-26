using NewLife.Agent.Command;
using NewLife.Log;
using NewLife.Reflection;
using System.Reflection;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 显示状态命令处理类
/// </summary>
public class ShowStatus : BaseCommandHandler
{
    /// <summary>
    /// 显示状态构造函数
    /// </summary>
    /// <param name="service"></param>
    public ShowStatus(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.ShowStatus;
        Description = "显示状态";
        ShortcutKey = '1';
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
        Console.WriteLine("描述：{0}", Service.Description);
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