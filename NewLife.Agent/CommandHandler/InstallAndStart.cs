using NewLife.Agent.Command;
using NewLife.Log;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 安装并启动服务命令处理类
/// </summary>
public class InstallAndStart : BaseCommandHandler
{
    /// <summary>
    /// 安装并启动服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public InstallAndStart(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.InstallAndStart;
        Description = "安装并启动服务";
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        // 可能服务已存在，安装时报错，但不要影响服务启动
        try
        {
            Service.Command.Handle(CommandConst.Install, args);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
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