using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 模拟运行命令处理类
/// </summary>
public class RunSimulation : BaseCommandHandler
{
    /// <summary>
    /// 模拟运行构造函数
    /// </summary>
    /// <param name="service"></param>
    public RunSimulation(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.RunSimulation;
        Description = "模拟运行";
        ShortcutKey = '5';
    }

    ///// <inheritdoc />
    //public override Boolean IsShowMenu() => !Service.Host.IsRunning(Service.ServiceName);

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