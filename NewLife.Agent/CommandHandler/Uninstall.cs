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
        Description = "Stop and remove service";
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
        // 失败时设置退出码，脚本/CI 才能感知卸载失败
        if (!Service.Host.Remove(Service.ServiceName)) Environment.ExitCode = 1;

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
}