using System.Xml.Linq;

namespace NewLife.Agent.Command;

/// <summary>
/// 模拟运行命令处理类
/// </summary>
public class RunSimulationCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 模拟运行构造函数
    /// </summary>
    /// <param name="service"></param>
    public RunSimulationCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.RunSimulation;

    /// <inheritdoc />
    public override String Description { get; set; } = "模拟运行";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; } = '5';

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return !Service.Host.IsRunning(Service.ServiceName);
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        if ("-delay".EqualIgnoreCase(args)) Thread.Sleep(5_000);
        try
        {
            Console.WriteLine("正在模拟运行……");
            Service.StartWork("模拟运行开始");

            // 开始辅助循环，检查状态
            ThreadPool.QueueUserWorkItem(s => Service.DoLoop());

            Console.WriteLine("任意键结束模拟运行！");
            Console.ReadKey(true);

            Service.Running = false;
            Service.StopWork("模拟运行停止");
            Service.ReleaseMemory();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        //Service.StartLoop();
        //Service.DoLoop();
        //Service.StopLoop();
    }
}