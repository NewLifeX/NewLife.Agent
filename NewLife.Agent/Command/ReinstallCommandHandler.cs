using System.Xml.Linq;
using NewLife.Log;
using NewLife.Model;

namespace NewLife.Agent.Command;

/// <summary>
/// 重新安装服务命令处理类
/// </summary>
public class ReinstallCommandHandler : BaseCommandHandler
{
    /// <summary>
    /// 重新安装服务构造函数
    /// </summary>
    /// <param name="service"></param>
    public ReinstallCommandHandler(ServiceBase service) : base(service)
    {
    }

    /// <inheritdoc/>
    public override String Cmd { get; set; } = CommandConst.Reinstall;

    /// <inheritdoc />
    public override String Description { get; set; } = "重新安装服务";

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
        Reinstall(args);

        // 稍微等一下，以便后续状态刷新
        Thread.Sleep(500);
    }
    private void Reinstall(String[] args)
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

        new InstallCommandHandler(Service).Process(args);

        // 稍微等待
        for (var i = 0; i < 50; i++)
        {
            if (Service.Host.IsInstalled(Service.ServiceName)) break;
            Thread.Sleep(100);
        }
        Service.Host.Start(Service.ServiceName);
    }
}