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
                /*
                 * @zero
                 * 在Linux中，同一台服务器，可能同时存在Systemd和SysVinit服务。
                 * 例如，Ubuntu 22系统，默认用的是Systemd服务，
                 * 如果在服务器上安装NewLife.Agent，启动时，将是以Systemd服务运行
                 * 如果在服务器上，再安装一个宝塔面板，在宝塔面板中，安装nginx、mysql、redis等等，都是以SysVinit服务运行
                 * 现在如果在NewLife.Agent中，配置监控nginx、mysql、redis，由于主服务是以Systemd服务运行，
                 * 在 Systemd 的 Host 中，无法读取到nginx、mysql、redis对应的SysVinit服务配置文件
                 * 最终将被认为服务未安装，也就造成监控无用，所以，这里需要进行调用GetHost，判断到底是哪种服务方式，以方便后续服务的状态检测
                 */
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
        // 优先使用Systemd
        // 检测Systemd服务配置文件
        var servicePath = Systemd.GetServicePath(serviceName);
        if (servicePath != null)
        {
            return new Systemd() { Service = Service };
        }
        // 检测SysVinit服务配置文件
        servicePath = SysVinit.GetServicePath(serviceName);
        var sysVinitFile = Path.Combine(SysVinit.ServicePath, serviceName);
        if (servicePath != null)
        {
            return new SysVinit() { Service = Service };
        }
        //依然找不到时，则默认当前Host
        return Service.Host;
    }
}