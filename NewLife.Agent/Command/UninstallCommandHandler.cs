using NewLife.Log;

namespace NewLife.Agent.Command;

/// <summary>
/// 停止并卸载服务命令处理类
/// </summary>
public class UninstallCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 停止并卸载服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public UninstallCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Uninstall;

    /// <inheritdoc />
    public override String Description { get; set; } = "停止并卸载服务";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; }

    /// <inheritdoc />
    public override Boolean IsShowMenu()
    {
        return false;
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        try
        {
            Service.Host.Stop(Service.ServiceName);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
        Service.Host.Remove(Service.ServiceName);
        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}