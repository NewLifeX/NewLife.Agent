using NewLife.Agent.Command;
using NewLife.Log;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 重新安装服务命令处理类
/// </summary>
public class Reinstall : BaseCommandHandler
{
    /// <summary>
    /// 重新安装服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public Reinstall(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.Reinstall;
        Description = "重新安装服务";
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        try
        {
            Service.Host.Stop(Service.ServiceName);
            Service.Host.Remove(Service.ServiceName);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        Service.Command.Handle(CommandConst.Install, args);

        // 稍微等待
        for (var i = 0; i < 50; i++)
        {
            if (Service.Host.IsInstalled(Service.ServiceName)) break;
            Thread.Sleep(100);
        }
        Service.Host.Start(Service.ServiceName);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}