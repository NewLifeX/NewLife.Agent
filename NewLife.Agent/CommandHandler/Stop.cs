using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 停止服务命令处理类
/// </summary>
public class Stop : BaseCommandHandler
{
    /// <summary>
    /// 停止服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Stop(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Stop;
        Description = "Stop service";
        ShortcutKey = '3';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => Service.Host.IsRunning(Service.ServiceName);

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        // 失败时设置退出码，脚本/CI 才能感知停止失败
        if (!Service.Host.Stop(Service.ServiceName)) Environment.ExitCode = 1;

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}