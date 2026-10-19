using NewLife.Agent.Command;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 执行服务命令处理类
/// </summary>
public class RunService : BaseCommandHandler
{
    /// <summary>
    /// 执行服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public RunService(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.RunService;
        Description = L.T("Run service");
    }

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        Service.Host.Run(Service);
    }
}