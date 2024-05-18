namespace NewLife.Agent.Command;

/// <summary>
/// 启动服务命令处理类
/// </summary>
public class StartCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 启动服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public StartCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Start;

    /// <inheritdoc />
    public override String Description { get; set; } = "启动服务";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; } = '3';

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return Service.Host.IsInstalled(Service.ServiceName) && !Service.Host.IsRunning(Service.ServiceName);
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Start(Service.ServiceName);
        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}