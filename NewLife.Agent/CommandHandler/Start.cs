using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 启动服务命令处理类
/// </summary>
public class Start : BaseCommandHandler
{
    /// <summary>
    /// 启动服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Start(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Start;
        Description = "启动服务";
        ShortcutKey = '3';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => Service.Host.IsInstalled(Service.ServiceName) && !Service.Host.IsRunning(Service.ServiceName);

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Start(Service.ServiceName);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}