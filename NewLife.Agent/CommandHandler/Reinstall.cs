using System.Xml.Linq;
using NewLife.Agent.Command;
using NewLife.Log;
using NewLife.Model;

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
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Reinstall;

    /// <inheritdoc />
    public override String Description { get; set; } = "重新安装服务";

    /// <inheritdoc />
    public override Char? ShortcutKey { get; set; }

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