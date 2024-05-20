using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 重启服务命令处理类
/// </summary>
public class Restart : BaseCommandHandler
{
    /// <summary>
    /// 重启服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Restart(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Restart;
        Description = "重启服务";
        ShortcutKey = '4';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => Service.Host.IsRunning(Service.ServiceName);

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Restart(Service.ServiceName);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}