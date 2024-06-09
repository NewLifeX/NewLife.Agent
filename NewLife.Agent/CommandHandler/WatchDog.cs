using NewLife.Agent.Command;
using NewLife.Log;

namespace NewLife.Agent.CommandHandler;

/// <summary>
/// 看门狗命令处理类
/// </summary>
public class WatchDog : BaseCommandHandler
{
    /// <summary>
    /// 看门狗构造函数
    /// </summary>
    /// <param name="service"></param>
    public WatchDog(ServiceBase service) : base(service)
    {
        Cmd = CommandConst.WatchDog;
        Description = "看门狗保护服务";
        ShortcutKey = '7';
    }

    /// <inheritdoc />
    public override Boolean IsShowMenu() => WatchDogs.Length > 0;

    /// <summary>看门狗要保护的服务</summary>
    private String[] WatchDogs => Setting.Current.WatchDog.Split(",", ";");

    /// <inheritdoc/>
    public override void Process(String[] args)
    {
        CheckWatchDog();
    }

    /// <summary>检查看门狗。</summary>
    /// <remarks>
    /// XAgent看门狗功能由管理线程完成。
    /// 检查指定的任务是否已经停止，如果已经停止，则启动它。
    /// </remarks>
    public virtual void CheckWatchDog()
    {
        foreach (var item in WatchDogs)
        {
            var host = Service.Host;
            if (Service.Host is Systemd)
            {
                // 优先使用Systemd
                host = GetHost(item);
            }
            // 已安装未运行
            if (!host.IsInstalled(item))
                XTrace.WriteLine("未发现服务{0}，是否已安装？", item);
            else if (!host.IsRunning(item))
            {
                XTrace.WriteLine("发现服务{0}被关闭，准备启动！", item);

                host.Start(item);
            }
        }
    }


    /// <summary>
    /// 获取服务配置文件的路径
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns></returns>
    private IHost GetHost(String serviceName)
    {
        // Check for systemd service
        foreach (var path in Systemd.SystemdPaths)
        {
            var file = Path.Combine(path, $"{serviceName}.service");
            if (File.Exists(file))
            {
                return new Systemd(){Service = this.Service};
            }
        }

        // Check for SysVinit service
        var sysVinitFile = Path.Combine(SysVinit.ServicePath, serviceName);
        if (File.Exists(sysVinitFile))
        {
            return new SysVinit();
        }
        return Service.Host;
    }

}