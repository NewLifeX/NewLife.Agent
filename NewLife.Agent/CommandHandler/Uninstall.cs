using NewLife.Agent.Command;
using NewLife.Log;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 停止并卸载服务命令处理类
/// </summary>
public class Uninstall : BaseCommandHandler
{
    /// <summary>
    /// 停止并卸载服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Uninstall(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Uninstall;
        Description = "停止并卸载服务";
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