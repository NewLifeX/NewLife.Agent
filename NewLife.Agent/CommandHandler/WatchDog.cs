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
            // 已安装未运行
            if (!Service.Host.IsInstalled(item))
                XTrace.WriteLine("未发现服务{0}，是否已安装？", item);
            else if (!Service.Host.IsRunning(item))
            {
                XTrace.WriteLine("发现服务{0}被关闭，准备启动！", item);

                Service.Host.Start(item);
            }
        }
    }
}