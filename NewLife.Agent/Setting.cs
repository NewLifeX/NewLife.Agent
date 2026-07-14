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

    /// <summary>监控时间间隔。服务资源占用检测周期，默认10秒</summary>
    [Description("监控时间间隔。服务资源占用检测周期，默认10秒")]
    public Int32 WatchInterval { get; set; } = 10;

    /// <summary>释放内存间隔。定时执行GC并释放虚拟内存，默认600秒</summary>
    [Description("释放内存间隔。定时执行GC并释放虚拟内存，默认600秒")]
    public Int32 FreeMemoryInterval { get; set; } = 600;

    /// <summary>最大占用内存。超过该值时自动重启进程以释放资源，默认0表示不限</summary>
    [Description("最大占用内存。超过该值时自动重启进程以释放资源，默认0表示不限")]
    public Int32 MaxMemory { get; set; }

    /// <summary>最大线程数。超过该值时自动重启进程以释放资源，默认1000</summary>
    [Description("最大线程数。超过该值时自动重启进程以释放资源，默认1000")]
    public Int32 MaxThread { get; set; } = 1000;

    /// <summary>最大句柄数。超过该值时自动重启进程以释放资源，默认10000</summary>
    [Description("最大句柄数。超过该值时自动重启进程以释放资源，默认10000")]
    public Int32 MaxHandle { get; set; } = 10000;

    /// <summary>自动重启时间。到达设定时间时自动重启进程，默认0分表示不限</summary>
    [Description("自动重启时间。到达设定时间时自动重启进程，默认0分表示不限")]
    public Int32 AutoRestart { get; set; }

    /// <summary>自动重启时间范围。限制重启只能在此时间段内执行，格式00:00-06:00，默认不限</summary>
    [Description("自动重启时间范围。限制重启只能在此时间段内执行，格式00:00-06:00，默认不限")]
    public String RestartTimeRange { get; set; }

    /// <summary>看门狗。保护其它服务，每分钟检查一次。多个服务名逗号分隔</summary>
    [Description("看门狗。保护其它服务，每分钟检查一次。多个服务名逗号分隔")]
    public String WatchDog { get; set; } = "";

    /// <summary>启动后命令。服务启动后执行的命令</summary>
    [Description("启动后命令。服务启动后执行的命令")]
    public String AfterStart { get; set; } = "";

    /// <summary>是否启用Web管理面板。默认true</summary>
    [Description("启用Web面板。默认true")]
    public Boolean EnableWebPanel { get; set; } = true;

    /// <summary>Web管理面板端口。默认5580</summary>
    [Description("Web管理面板端口。默认5580")]
    public Int32 WebPort { get; set; } = 5580;

    /// <summary>Web面板鉴权级别。None不鉴权，LocalOnly本地免鉴权，Full全部鉴权。默认LocalOnly</summary>
    [Description("Web面板鉴权级别。None不鉴权，LocalOnly本地免鉴权，Full全部鉴权。默认LocalOnly")]
    public String WebAuthLevel { get; set; } = "LocalOnly";

    /// <summary>Web面板用户名</summary>
    [Description("Web面板用户名")]
    public String WebUserName { get; set; } = "admin";

    /// <summary>Web面板密码</summary>
    [Description("Web面板密码")]
    public String WebPassword { get; set; } = "admin";
    #endregion
}