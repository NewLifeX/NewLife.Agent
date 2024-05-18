using System.Text;

namespace NewLife.Agent;

/// <summary>Systemd服务设置</summary>
public class SystemdSetting
{
    #region 属性
    /// <summary>服务名</summary>
    public String ServiceName { get; set; }

    /// <summary>中文名</summary>
    public String DisplayName { get; set; }

    /// <summary>描述</summary>
    public String Description { get; set; }

    /// <summary>文件名</summary>
    public String FileName { get; set; }

    /// <summary>参数</summary>
    public String Arguments { get; set; }

    /// <summary>工作目录</summary>
    public String WorkingDirectory { get; set; }

    /// <summary>类型。simple/forking/oneshot/dbus/notify/idle</summary>
    public String Type { get; set; } = "simple";

    /// <summary>环境变量。可以指定服务会用到的环境变量</summary>
    public String Environment { get; set; }

    /// <summary>用户</summary>
    public String User { get; set; }

    /// <summary>组</summary>
    public String Group { get; set; }

    /// <summary>重启策略。no/on-success/on-failure/on-abnormal/on-watchdog/on-abort/always</summary>
    public String Restart { get; set; } = "always";

    /// <summary>重启间隔。默认3秒</summary>
    public Single RestartSec { get; set; } = 3f;

    /// <summary>指定的时间里重试失败。默认0</summary>
    public Int32 StartLimitInterval { get; set; } = 0;

    /// <summary>失败重试次数。默认5</summary>
    public Int32 StartLimitBurst { get; set; } = 5;

    /// <summary>杀服务信号</summary>
    /// <remarks>
    /// 设置杀死进程的第一步使用什么信号，所有可用的信号详见 signal(7) 手册。
    /// 默认值为 SIGTERM 信号。
    /// 注意，systemd 会无条件的紧跟此信号之后再发送一个 SIGCONT 信号，以确保干净的杀死已挂起(suspended)的进程
    /// </remarks>
    public String KillSignal { get; set; } = "SIGINT";

    /// <summary>杀服务模式</summary>
    /// <remarks>
    /// control-group：会干掉主进程及子进程（默认）
    /// mixed: SIGTERM 信号被发送到主进程，而随后的 SIGKILL 信号被发送到单元控制组的所有剩余进程，可以通过KillSignal设置关闭主进程的信号
    /// process: 仅关闭主进程
    /// none: 什么也不干
    /// </remarks>
    public String KillMode { get; set; }

    /// <summary>设置进程因内存不足而被杀死的优先级</summary>
    /// <remarks>可设为 -1000(禁止被杀死) 到 1000(最先被杀死)之间的整数值</remarks>
    public Int32 OOMScoreAdjust { get; set; }

    /// <summary>是否依赖于网络。在网络就绪后才启动服务</summary>
    public Boolean Network { get; set; }
    #endregion

    #region 方法
    /// <summary>构建配置文件</summary>
    public String Build()
    {
        //var asm = Assembly.GetEntryAssembly();
        var des = !DisplayName.IsNullOrEmpty() ? DisplayName : Description;

        var sb = new StringBuilder();
        sb.AppendLine("[Unit]");
        sb.AppendLine($"Description={des}");

        if (Network)
            sb.AppendLine($"After=network.target");
        //sb.AppendLine("StartLimitIntervalSec=0");

        sb.AppendLine();
        sb.AppendLine("[Service]");
        sb.AppendLine($"Type={Type}");
        if (!Environment.IsNullOrEmpty()) sb.AppendLine($"Environment={Environment}");
        //sb.AppendLine($"ExecStart=/usr/bin/dotnet {asm.Location}");
        sb.AppendLine($"ExecStart={FileName} {Arguments}");
        sb.AppendLine($"WorkingDirectory={(!WorkingDirectory.IsNullOrEmpty() ? WorkingDirectory : Path.GetDirectoryName(FileName).GetFullPath())}");
        if (!User.IsNullOrEmpty()) sb.AppendLine($"User={User}");
        if (!Group.IsNullOrEmpty()) sb.AppendLine($"Group={Group}");

        // no 表示服务退出时，服务不会自动重启，默认值。
        // on-failure 表示当进程以非零退出代码退出，由信号终止；当操作(如服务重新加载)超时；以及何时触发配置的监视程序超时时，服务会自动重启。
        // always 表示只要服务退出，则服务将自动重启。
        sb.AppendLine($"Restart={Restart}");

        // RestartSec 重启间隔，比如某次异常后，等待3(s)再进行启动，默认值0.1(s)
        sb.AppendLine($"RestartSec={RestartSec}");

        // StartLimitInterval: 无限次重启，默认是10秒内如果重启超过5次则不再重启，设置为0表示不限次数重启
        sb.AppendLine($"StartLimitInterval={StartLimitInterval}");
        if (StartLimitInterval > 0 && StartLimitBurst > 0)
            sb.AppendLine($"StartLimitBurst={StartLimitBurst}");

        if (!KillSignal.IsNullOrEmpty()) sb.AppendLine($"KillSignal={KillSignal}");
        if (!KillMode.IsNullOrEmpty()) sb.AppendLine($"KillMode={KillMode}");

        if (OOMScoreAdjust != 0) sb.AppendLine($"OOMScoreAdjust={OOMScoreAdjust}");

        sb.AppendLine();
        sb.AppendLine("[Install]");
        sb.AppendLine("WantedBy=multi-user.target");

        return sb.ToString();
    }
    #endregion
}
