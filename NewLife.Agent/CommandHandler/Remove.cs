using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 卸载服务命令处理类
/// </summary>
public class Remove : BaseCommandHandler
{
    /// <summary>
    /// 卸载服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Remove(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Remove;
        Description = "卸载服务";
        ShortcutKey = '2';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => Service.Host.IsInstalled(Service.ServiceName);

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Remove(Service.ServiceName);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}