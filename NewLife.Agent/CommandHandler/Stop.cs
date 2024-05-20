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
        Description = "停止服务";
        ShortcutKey = '3';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => Service.Host.IsRunning(Service.ServiceName);

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Stop(Service.ServiceName);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}