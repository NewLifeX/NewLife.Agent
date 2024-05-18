namespace NewLife.Agent.Command;

/// <summary>
/// 停止服务命令处理类
/// </summary>
public class StopCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 停止服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public StopCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Stop;

    /// <inheritdoc />
    public override String Description { get; set; } = "停止服务";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; } = '3';

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return Service.Host.IsRunning(Service.ServiceName);
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Stop(Service.ServiceName);
        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}