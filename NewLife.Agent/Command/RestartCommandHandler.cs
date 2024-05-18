namespace NewLife.Agent.Command;

/// <summary>
/// 重启服务命令处理类
/// </summary>
public class RestartCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 重启服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public RestartCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Restart;

    /// <inheritdoc />
    public override String Description { get; set; } = "重启服务";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; } = '4';

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return Service.Host.IsRunning(Service.ServiceName);
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Restart(Service.ServiceName);
        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}