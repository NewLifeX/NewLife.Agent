using System.ComponentModel;
using System.Globalization;
using NewLife.Configuration;
using NewLife.Log;

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

    /// <summary>Web面板鉴权级别。None不鉴权，LocalOnly本地免鉴权，Full全部鉴权。默认LocalOnly</summary>
    [Description("Web panel authentication level. None: no authentication; LocalOnly: local requests bypass authentication; Full: always authenticate. Default LocalOnly.")]
    public String WebAuthLevel { get; set; } = "LocalOnly";

    /// <summary>Web面板用户名</summary>
    [Description("Web panel user name.")]
    public String WebUserName { get; set; } = "admin";

    /// <summary>Web面板密码</summary>
    [Description("Web panel password.")]
    public String WebPassword { get; set; } = "admin";

    /// <summary>语言。界面与日志语言，如 zh-Hans、en，为空时跟随操作系统</summary>
    [Description("Language. UI and log language, such as zh-Hans or en. Empty follows the operating system.")]
    public String Language { get; set; } = "";
    #endregion

    #region 方法
#if !NET40
    /// <summary>系统语言。进程启动时的 UI 文化，不受请求语言作用域影响</summary>
    private static readonly CultureInfo _SystemCulture = CultureInfo.CurrentUICulture;
#endif

    /// <summary>保存配置后立即应用语言设置</summary>
    public override void Save()
    {
        base.Save();

        ApplyLanguage();
    }

    /// <summary>加载配置后立即应用语言设置</summary>
    protected override void OnLoaded() => ApplyLanguage();

    /// <summary>应用语言设置</summary>
    /// <remarks>语言表缺失或名称无效时回退系统语言，避免进程停留在英文界面。</remarks>
    public void ApplyLanguage()
    {
#if !NET40
        CultureInfo culture = null;
        if (!Language.IsNullOrEmpty())
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(Language.Trim());
            }
            catch (ArgumentException)
            {
                XTrace.WriteLine("Invalid language [{0}], falling back to the system language.", Language);
            }
        }

        if (culture != null && !Localization.IsSupported(culture))
        {
            XTrace.WriteLine("Language [{0}] has no translation table, falling back to the system language.", Language);
            culture = null;
        }

        Localization.Culture = culture;
        // 固定日志语言，避免 Web 请求按 Accept-Language 切换文化时日志跟着变化
        Localization.LogCulture = culture ?? _SystemCulture;
#endif
    }
    #endregion
}