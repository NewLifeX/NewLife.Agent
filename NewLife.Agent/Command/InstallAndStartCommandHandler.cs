using NewLife.Log;

namespace NewLife.Agent.Command;

/// <summary>
/// 安装并启动服务命令处理类
/// </summary>
public class InstallAndStartCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 安装并启动服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public InstallAndStartCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.InstallAndStart;

    /// <inheritdoc />
    public override String Description { get; set; } = "安装并启动服务";

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
        // 可能服务已存在，安装时报错，但不要影响服务启动
        try
        {
            new InstallCommandHandler(Service).Process(args);
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