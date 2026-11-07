using System.ComponentModel;
using NewLife.Configuration;

namespace NewLife.Agent;

/// <summary>服务设置</summary>
[DisplayName("Service settings")]
[Config("Agent.config")]
public class Setting : Config<Setting>
{
    #region 属性
    /// <summary>服务名</summary>
    [Description("Service name")]
    public String ServiceName { get; set; } = "";

    /// <summary>显示名</summary>
    [Description("Display name")]
    public String DisplayName { get; set; } = "";

    /// <summary>服务描述</summary>
    [Description("Service description")]
    public String Description { get; set; } = "";

    /// <summary>使用自动运行。在Windows系统上，自动运行是登录后在桌面运行，而不使用后台服务。默认false</summary>
    [Description("Use autorun. On Windows, autorun starts on the desktop after login instead of running as a background service. Default false.")]
    public Boolean UseAutorun { get; set; }

    /// <summary>监控时间间隔。服务资源占用检测周期，默认10秒</summary>
    [Description("Watch interval. Resource usage check period in seconds. Default 10.")]
    public Int32 WatchInterval { get; set; } = 10;

    /// <summary>释放内存间隔。定时执行GC并释放虚拟内存，默认600秒</summary>
    [Description("Free memory interval. Periodically run GC and release virtual memory, in seconds. Default 600.")]
    public Int32 FreeMemoryInterval { get; set; } = 600;

    /// <summary>最大占用内存。超过该值时自动重启进程以释放资源，默认0表示不限</summary>
    [Description("Max memory. Restart the process when usage exceeds this value to release resources. Default 0 means unlimited.")]
    public Int32 MaxMemory { get; set; }

    /// <summary>最大线程数。超过该值时自动重启进程以释放资源，默认1000</summary>
    [Description("Max thread count. Restart the process when the thread count exceeds this value to release resources. Default 1000.")]
    public Int32 MaxThread { get; set; } = 1000;

    /// <summary>最大句柄数。超过该值时自动重启进程以释放资源，默认10000</summary>
    [Description("Max handle count. Restart the process when the handle count exceeds this value to release resources. Default 10000.")]
    public Int32 MaxHandle { get; set; } = 10000;

    /// <summary>自动重启时间。到达设定时间时自动重启进程，默认0分表示不限</summary>
    [Description("Auto restart. Automatically restart the process when this interval is reached, in minutes. Default 0 means unlimited.")]
    public Int32 AutoRestart { get; set; }

    /// <summary>自动重启时间范围。限制重启只能在此时间段内执行，格式00:00-06:00，默认不限</summary>
    [Description("Auto restart time range. Restrict restarts to this time window, format 00:00-06:00. Default unlimited.")]
    public String RestartTimeRange { get; set; }

    /// <summary>看门狗。保护其它服务，每分钟检查一次。多个服务名逗号分隔</summary>
    [Description("Watchdog. Protect other services and check every minute. Separate multiple service names with commas.")]
    public String WatchDog { get; set; } = "";

    /// <summary>启动后命令。服务启动后执行的命令</summary>
    [Description("After start. Command to run after the service starts.")]
    public String AfterStart { get; set; } = "";

    /// <summary>是否启用Web管理面板。默认true</summary>
    [Description("Enable the web management panel. Default true.")]
    public Boolean EnableWebPanel { get; set; } = true;

    /// <summary>Web管理面板端口。默认5580</summary>
    [Description("Web management panel port. Default 5580.")]
    public Int32 WebPort { get; set; } = 5580;

    /// <summary>Web面板绑定地址。默认127.0.0.1仅本机访问，设为*监听所有网卡，也可指定具体IP</summary>
    [Description("Web panel bind address. Default 127.0.0.1 for local access only; set to * to listen on all interfaces, or specify an IP address.")]
    public String WebBind { get; set; } = "127.0.0.1";

    /// <summary>Web面板鉴权级别。已废弃并忽略，所有请求统一要求登录，不再支持本地免鉴权</summary>
    [Description("Web panel authentication level. Deprecated and ignored; every request requires sign-in.")]
    public String WebAuthLevel { get; set; } = "LocalOnly";

    /// <summary>Web面板用户名</summary>
    [Description("Web panel user name.")]
    public String WebUserName { get; set; } = "admin";

    /// <summary>Web面板口令。保存为加盐哈希，历史明文会在首次登录成功后自动升级；为空时首次启动生成随机口令</summary>
    [Description("Web panel password.")]
    public String WebPassword { get; set; } = "";

    /// <summary>Web面板认证模式。Password本地口令（哈希保存），OsAccount操作系统账号（仅管理员组）。默认Password</summary>
    [Description("Web panel authentication mode. Password: local password stored as a salted hash; OsAccount: operating system account limited to the administrator group. Default Password.")]
    public String WebAuthMode { get; set; } = "Password";

    /// <summary>Web面板管理员组。操作系统账号认证模式下允许登录的组，逗号分隔，留空使用平台默认值（Windows: Administrators；Linux: root,sudo,wheel,admin）</summary>
    [Description("Web panel administrator groups for the OS account mode, separated by commas. Empty uses the platform default (Administrators on Windows; root, sudo, wheel, admin on Linux).")]
    public String WebAdminGroups { get; set; } = "";

    /// <summary>Web面板传输安全。Auto按绑定地址自动决定（本机关闭，其它自签证书），Off关闭TLS，SelfSigned自签证书，Pfx使用WebCertificate指定证书</summary>
    [Description("Web panel transport security. Auto: off for loopback and self-signed otherwise; Off: no TLS; SelfSigned: generate a self-signed certificate; Pfx: use the certificate specified by WebCertificate.")]
    public String WebTls { get; set; } = "Auto";

    /// <summary>Web面板PFX证书文件。为空时自动生成到配置目录</summary>
    [Description("Web panel PFX certificate file. Empty means a self-signed certificate is generated in the configuration directory.")]
    public String WebCertificate { get; set; } = "";

    /// <summary>Web面板PFX证书口令</summary>
    [Description("Web panel PFX certificate password.")]
    public String WebCertificatePassword { get; set; } = "";

    /// <summary>允许非本机监听且不启用TLS。默认false，此时面板拒绝在没有TLS的情况下暴露到网络</summary>
    [Description("Allow listening on non-loopback addresses without TLS. Default false, in which case the panel refuses to expose itself to the network without TLS.")]
    public Boolean WebAllowInsecureRemote { get; set; }
    #endregion
}