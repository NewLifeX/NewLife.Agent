#if !__CORE__
using System.ComponentModel;
using NewLife.Configuration;

namespace NewLife.Agent;

/// <summary>服务设置</summary>
[DisplayName("服务设置")]
[Config("Agent.config")]
public class Setting : Config<Setting>
{
    #region 属性
    /// <summary>服务名</summary>
    [Description("服务名")]
    public String ServiceName { get; set; } = "";

    /// <summary>显示名</summary>
    [Description("显示名")]
    public String DisplayName { get; set; } = "";

    /// <summary>服务描述</summary>
    [Description("服务描述")]
    public String Description { get; set; } = "";

    /// <summary>使用自动运行。在Windows系统上，自动运行是登录后在桌面运行，而不使用后台服务。默认false</summary>
    [Description("使用自动运行。在Windows系统上，自动运行是登录后在桌面运行，而不使用后台服务。默认false")]
    public Boolean UseAutorun { get; set; } 

    /// <summary>最大占用内存。超过最大占用时，整个服务进程将会重启，以释放资源。默认0M</summary>
    [Description("最大占用内存。超过最大占用时，整个服务进程将会重启，以释放资源。默认0M")]
    public Int32 MaxMemory { get; set; }

    /// <summary>最大线程数。超过最大占用时，整个服务进程将会重启，以释放资源。默认1000个</summary>
    [Description("最大线程数。超过最大占用时，整个服务进程将会重启，以释放资源。默认1000个")]
    public Int32 MaxThread { get; set; } = 1000;

    /// <summary>最大句柄数。超过最大占用时，整个服务进程将会重启，以释放资源。默认10000</summary>
    [Description("最大句柄数。超过最大占用时，整个服务进程将会重启，以释放资源。默认10000个")]
    public Int32 MaxHandle { get; set; } = 10000;

    /// <summary>自动重启时间。到达自动重启时间时，整个服务进程将会重启，以释放资源。默认0分，表示无限</summary>
    [Description("自动重启时间。到达自动重启时间时，整个服务进程将会重启，以释放资源。默认0分，表示无限")]
    public Int32 AutoRestart { get; set; }

    /// <summary>自动重启时间的范围。限制服务进程只能在这个时间范围内重启，格式为：00:00-06:00。默认为空，表示不限</summary>
    [Description("自动重启时间的范围。限制服务进程只能在这个时间范围内重启，格式为：00:00-06:00。默认为空，表示不限")]
    public String RestartTimeRange { get; set; }

    /// <summary>看门狗，保护其它服务，每分钟检查一次。多个服务名逗号分隔</summary>
    [Description("看门狗，保护其它服务，每分钟检查一次。多个服务名逗号分隔")]
    public String WatchDog { get; set; } = "";

    /// <summary>启动后命令，服务启动后执行的命令</summary>
    [Description("启动后命令，服务启动后执行的命令")]
    public String AfterStart { get; set; } = "";
    #endregion
}
#endif