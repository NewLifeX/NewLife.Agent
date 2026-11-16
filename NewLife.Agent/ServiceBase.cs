using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Security;
using NewLife.Agent.Command;
using NewLife.Agent.Models;
using NewLife.Agent.Windows;
using NewLife.Log;
using NewLife.Reflection;
#if !NET40
using NewLife.Agent.WebPanel;
using NewLife;
#endif

[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[module: UnverifiableCode]

namespace NewLife.Agent;

/// <summary>服务程序基类</summary>
public abstract class ServiceBase : DisposeBase
{
    #region 属性
    /// <summary>主机</summary>
    public IHost Host { get; set; }

    /// <summary>服务名</summary>
    public String ServiceName { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>用于界面显示的服务标题。按当前语言翻译</summary>
    /// <remarks>只用于显示与安装注册，不改变原始 DisplayName；服务名仍是原始值</remarks>
    public String DisplayTitle => DisplayName.IsNullOrEmpty() ? DisplayName : L.T(DisplayName);

    /// <summary>用于界面显示和安装注册的服务描述。按当前语言翻译</summary>
    /// <remarks>只用于显示与安装注册，不改变原始 Description</remarks>
    public String DisplayDescription => Description.IsNullOrEmpty() ? Description : L.T(Description);

    /// <summary>描述</summary>
    public String Description { get; set; }

    /// <summary>是否使用自启动。自启动需要用户登录桌面，默认false使用系统服务</summary>
    public Boolean UseAutorun { get; set; }

    private volatile Boolean _running;

    /// <summary>运行中</summary>
    /// <remarks>停止线程与巡检线程跨线程读写，必须用 volatile 支撑字段保证可见性</remarks>
    public Boolean Running { get => _running; set => _running = value; }

    /// <summary>命令工厂</summary>
    public CommandFactory Command { get; }

#if !NET40
    /// <summary>Web管理面板</summary>
    protected AgentWebPanel _webPanel;

    /// <summary>创建Web管理面板。子类可重写以返回自定义面板实例（如注入扩展了嵌入式资源和路由的子类面板）</summary>
    /// <param name="service">所属服务</param>
    /// <returns>Web管理面板实例</returns>
    protected virtual AgentWebPanel CreateWebPanel(ServiceBase service)
    {
        return new AgentWebPanel(service);
    }
#endif
    #endregion

    #region 构造
    /// <summary>初始化</summary>
    public ServiceBase()
    {
        InitService();

        var set = Setting.Current;
        UseAutorun = set.UseAutorun;

        Command = new CommandFactory(this);
    }

    /// <summary>初始化服务。Agent组件内部使用</summary>
    public static void InitService()
    {
        // 以服务方式启动时，不写控制台日志，修正当前目录，帮助用户处理路径问题
        var args = Environment.GetCommandLineArgs();
        var isService = args != null && "-s".EqualIgnoreCase(args);
        if (!isService)
            XTrace.UseConsole();

        // 提前设置好当前目录，避免后续各种问题
        Environment.CurrentDirectory = ".".GetBasePath();
        XTrace.WriteLine("CurrentDirectory: {0}", Environment.CurrentDirectory);

        typeof(ServiceBase).Assembly.WriteVersion();
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        //StopWork(disposing ? "Dispose" : "GC");
        StopLoop();
    }
    #endregion

    #region 主函数
    /// <summary>服务主函数</summary>
    /// <param name="args"></param>
    public void Main(String[] args)
    {
        if (args == null || args.Length == 0) args = Environment.GetCommandLineArgs();

        if ("-Autorun".EqualIgnoreCase(args)) UseAutorun = true;

        Init();

        var cmd = args?.FirstOrDefault(e => !e.IsNullOrEmpty() && e.Length > 1 && e[0] == '-');
        if (!cmd.IsNullOrEmpty())
        {
            try
            {
                ProcessCommand(cmd, args);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                Environment.ExitCode = 1;
            }
        }
        else
        {
            if (!DisplayName.IsNullOrEmpty()) Console.Title = DisplayTitle;

            Command.Handle(CommandConst.ShowStatus, args);

            // 输出状态，菜单循环
            ProcessMenu(args);
        }

        // 释放文本文件日志对象，确保日志队列内容写入磁盘
        if (XTrace.Log is CompositeLog compositeLog)
        {
            var log = compositeLog.Get<TextFileLog>();
            log.TryDispose();
        }
    }

    /// <summary>
    /// 初始化服务
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    protected virtual void Init()
    {
        Log = XTrace.Log;

        if (Host == null)
        {
            if (Runtime.Windows)
            {
                if (UseAutorun)
                    Host = new WindowsAutorun { Service = this };
                else
                    Host = new WindowsService { Service = this };
            }
            else if (Runtime.OSX)
                Host = new OSXLaunch { Service = this };
            else if (Systemd.Available)
                Host = new Systemd { Service = this };
            //else if (SysVinit.Available)
            //    Host = new SysVinit { Service = this };
            else if (Procd.Available)
                Host = new Procd { Service = this };
            else if (RcInit.Available)
                Host = new RcInit { Service = this };
            else
            {
                //throw new NotSupportedException($"不支持该操作系统！");
                Host = new DefaultHost { Service = this };
            }

            WriteLog("Host: {0}", Host.Name);
        }

        // 初始化配置
        var set = Setting.Current;
        set.UseAutorun = UseAutorun;
        if (set.ServiceName.IsNullOrEmpty()) set.ServiceName = ServiceName;
        if (set.DisplayName.IsNullOrEmpty()) set.DisplayName = DisplayName;
        if (set.Description.IsNullOrEmpty()) set.Description = Description;

        // 从程序集构造配置
        var asm = AssemblyX.Entry;
        if (set.ServiceName.IsNullOrEmpty()) set.ServiceName = asm.Name;
        if (set.DisplayName.IsNullOrEmpty()) set.DisplayName = asm.Title;
        if (set.Description.IsNullOrEmpty()) set.Description = asm.Description;

        // 用配置覆盖
        ServiceName = set.ServiceName;
        DisplayName = set.DisplayName;
        Description = set.Description;

        try
        {
            set.Save();
        }
        catch (Exception ex)
        {
            // 只读目录或容器镜像中配置不可写时仍应允许服务启动，使用内存配置即可
            WriteLog("Failed to save the configuration: {0}", ex.Message);
        }
    }

    /// <summary>显示状态</summary>
    protected virtual void ProcessMenu(String[] args)
    {
        var service = this;
        var name = ServiceName;
        //var args = Environment.GetCommandLineArgs();
        while (true)
        {
            //输出菜单
            ShowMenu();
            Console.Write(L.T("Enter a command number: "));
            //Console.WriteLine();

            //读取命令
            var key = ReadMenuKey();
            Console.WriteLine();

            if (key.KeyChar == '0') break;
            if (key.KeyChar == '\r' || key.KeyChar == '\n') continue;
            try
            {
                var result = Command.Handle(key.KeyChar, args);
                if (!result)
                {
                    // 兼容旧版本自定义菜单，相关代码已过时
                    var menu = _Menus.FirstOrDefault(e => e.Key == key.KeyChar);
                    if (menu != null)
                    {
                        menu.Callback();
                    }
                    else
                    {
                        Console.WriteLine(L.T("Invalid command number [{0}]; please try again."), key.KeyChar);
                    }
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            Console.WriteLine();
            //Thread.Sleep(1000);
        }
    }

    /// <summary>读取菜单按键。可重写以便单元测试注入按键序列</summary>
    /// <returns>控制台按键信息</returns>
    protected virtual ConsoleKeyInfo ReadMenuKey() => Console.ReadKey();

    /// <summary>显示菜单</summary>
    protected virtual void ShowMenu()
    {
        //var name = ServiceName;

        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.WriteLine();
        Console.WriteLine(L.T("No.\tFunction\tCommand line"));
        var menus = Command.GetShortcutMenu();
        foreach (var menu in menus)
        {
            Console.WriteLine(L.T(" {0}. {1}\t{2}"), menu.Key, menu.Name, menu.Cmd);
        }

        //兼容旧版本菜单，相关代码已过时
        if (_Menus.Count > 0)
        {
            //foreach (var item in _Menus)
            //{
            //    Console.WriteLine("{0} {1}", item.Key, item.Value.Name);
            //}
            foreach (var menu in _Menus)
            {
                Console.WriteLine(L.T(" {0}. {1}\t"), menu.Key, menu.Name);
            }
        }

#if !NET40
        // 显示Web面板地址
        if (_webPanel != null && _webPanel.Active)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(L.T(" Web management: http://localhost:{0}/"), _webPanel.Port);
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
#endif

        Console.WriteLine(L.T(" 0. Exit\t"));
        Console.WriteLine();
        Console.ForegroundColor = color;
    }

    private readonly List<Menu> _Menus = [];
    /// <summary>添加菜单</summary>
    /// <param name="key"></param>
    /// <param name="name"></param>
    /// <param name="callbak"></param>
    [Obsolete("建议定义命令处理类，并继承 BaseCommandHandler")]
    public void AddMenu(Char key, String name, Action callbak)
    {
        //if (!_Menus.ContainsKey(key))
        //{
        _Menus.RemoveAll(e => e.Key == key);
        _Menus.Add(new Menu(key, name, null, callbak));
        //}
    }

    /// <summary>处理命令。可继承以拦截安装卸载等命令</summary>
    /// <param name="cmd"></param>
    /// <param name="args"></param>
    protected virtual void ProcessCommand(String cmd, String[] args)
    {
        WriteLog("ProcessCommand cmd={0} args={1}", cmd, args.Join(" "));
        cmd = cmd.ToLowerInvariant();
        if (!Command.Handle(cmd, args))
        {
            WriteLog("Unknown command: {0}", cmd);
            Environment.ExitCode = 1;
        }
        WriteLog("ProcessFinished cmd={0}", cmd);
    }
    #endregion

    #region 服务控制
    private AutoResetEvent _event;
    private Process _process;

    /// <summary>停止请求标记。停止请求可能早于巡检循环启动（服务处于启动挂起状态时被停止），必须独立记录</summary>
    private volatile Boolean _stopRequested;

    /// <summary>业务逻辑是否已启动。用于与 StopWork 配对，避免未启动就调用停止逻辑</summary>
    private volatile Boolean _workStarted;

    /// <summary>运行计时器。用单调时钟计算运行时长，避免系统时间调整导致定时重启被推迟或提前</summary>
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    /// <summary>运行时长偏移。仅供测试回拨启动时间构造到期场景，生产环境始终为零</summary>
    internal TimeSpan UptimeOffset { get; set; }

    /// <summary>已运行时长</summary>
    private TimeSpan Uptime => _uptime.Elapsed + UptimeOffset;

    /// <summary>重启冷却毫秒数。阈值持续超标时避免每轮巡检都触发一次重启</summary>
    private const Int32 RestartCooldown = 60_000;

    /// <summary>上次触发重启的运行计时（毫秒）</summary>
    private Int64 _lastRestart = -1;

    /// <summary>巡检间隔上下限，单位为秒</summary>
    private const Int32 MinWatchInterval = 1;
    private const Int32 MaxWatchInterval = 3600;

    /// <summary>主循环</summary>
    internal void DoLoop()
    {
        // 停止请求可能早于巡检循环启动（服务处于启动挂起阶段时被停止），此时不执行业务后置动作与巡检
        if (_stopRequested)
        {
            Running = false;
            return;
        }

        // 启动后命令，服务启动后执行的命令
        var set = Setting.Current;
        if (!set.AfterStart.IsNullOrEmpty())
        {
            try
            {
                // 复用 ServiceHelper 解析命令行为程序路径与参数，支持引号包裹路径
                var parts = ServiceHelper.SplitCommandLine(set.AfterStart);
                if (parts.Length > 0)
                {
                    var file = parts[0];
                    var args = parts.Length > 1 ? parts[1] : "";
                    WriteLog("Run after start: FileName={0} Args={1}", file, args);

                    var si = new ProcessStartInfo(file.GetFullPath(), args)
                    {
                        WorkingDirectory = ".".GetFullPath()
                    };
                    _process = Process.Start(si);
                    // Start 在复用现有进程时可能返回 null，直接取值会 NRE 掩盖真正的启动问题
                    if (_process != null) WriteLog("Process: [{0}]{1}", _process.Id, _process.ProcessName);
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        // 巡检间隔必须落在合理区间：0 会空转，负数或过大值（秒转毫秒溢出）会让 WaitOne 抛异常并杀掉巡检线程
        var interval = set.WatchInterval;
        if (interval < MinWatchInterval) interval = MinWatchInterval;
        if (interval > MaxWatchInterval) interval = MaxWatchInterval;

        _event = new AutoResetEvent(false);
        Running = true;

        // 启动期间可能已收到停止请求，此时不能再进入巡检，否则会把 Running 重新置为 true 导致服务停不下来
        while (Running && !_stopRequested)
        {
            try
            {
                DoCheck(null);
            }
            catch (ThreadAbortException) { }
            catch (ThreadInterruptedException) { }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }

            try
            {
                _event?.WaitOne(interval * 1000);
            }
            catch (ObjectDisposedException)
            {
                // 事件对象已被停止线程释放，等同收到停止信号
                break;
            }
        }

        var evt = _event;
        _event = null;
        evt?.Dispose();
    }

    /// <summary>开始循环</summary>
    /// <remarks>
    /// 调用 StartWork 启动业务逻辑，等待至多 3 秒。
    /// StartWork 只执行一次，不允许多次重试，避免业务初始化重入导致的资源冲突。
    /// 若超时，StartWork 仍在后台运行，调用方应确保 StartWork 为非阻塞设计。
    /// </remarks>
    protected internal void StartLoop()
    {
        // 新一轮启动：清除上一轮遗留的停止请求与业务标记，保证启动前到达的停止请求不会被上一轮状态吞掉
        _stopRequested = false;
        _workStarted = false;

        Model.Host.RegisterExit(OnProcessExit);

        // StartWork 只执行一次，不允许重入
#if NET40
        var task = Task.Factory.StartNew(() =>
#else
        var task = Task.Run(() =>
#endif
        {
            try
            {
                _workStarted = true;
                StartWork("StartLoop");
            }
            catch (Exception ex)
            {
                WriteLog("StartWork failed: {0}", ex.Message);
                Log?.Error(ex.ToString());
            }
        });
        if (!task.Wait(3_000))
        {
            XTrace.WriteLine("StartWork took too long; avoid blocking operations in StartWork.");
        }
    }

    /// <summary>停止循环</summary>
    /// <remarks>
    /// 无论巡检循环是否已经进入，都先记录停止请求：服务在启动挂起阶段被停止时，循环随后才启动，
    /// 若此处直接返回，DoLoop 会把 Running 重新置为 true，出现「SCM 认为已停止、进程仍在跑且停不下来」。
    /// </remarks>
    protected internal void StopLoop()
    {
        _stopRequested = true;

        try
        {
            if (_workStarted)
            {
                _workStarted = false;
                StopWork("StopLoop");
            }
        }
        catch (Exception ex)
        {
            // 派生类停止逻辑抛异常时，后续清理与标记复位必须照常执行，否则服务会挂死并等 SCM 强杀
            WriteLog("StopWork failed: {0}", ex.Message);
            Log?.Error(ex.ToString());
        }
        finally
        {
            Running = false;

            try
            {
                _event?.Set();
            }
            catch (ObjectDisposedException) { }
        }

        try
        {
            _process?.Kill();
        }
        catch { }
        finally
        {
            // Kill 失败（进程已自行退出）时同样必须释放句柄，否则每轮启停泄漏一个 Process 句柄
            var process = _process;
            _process = null;
            if (process != null)
            {
                try { process.Dispose(); } catch { }
            }
        }

        FreeMemory();
    }

    /// <summary>申请重启服务。带冷却时间，阈值持续超标时不会每轮巡检都触发重启</summary>
    /// <param name="message">重启原因，写入日志</param>
    /// <returns>是否真正发起了重启；false 表示处于冷却期或重启失败</returns>
    protected virtual Boolean RequestRestart(String message)
    {
        var now = (Int64)Uptime.TotalMilliseconds;
        if (_lastRestart >= 0 && now - _lastRestart < RestartCooldown) return false;

        WriteLog("{0}", message);

        // 只有真正发起成功才进入冷却；失败时下一轮巡检继续尝试
        var result = Host.Restart(ServiceName);
        if (result) _lastRestart = now;

        return result;
    }

    /// <summary>开始工作</summary>
    /// <remarks>基类实现用于输出日志</remarks>
    /// <param name="reason"></param>
    public virtual void StartWork(String reason)
    {
        WriteLog("Service started: {0}", reason);

#if !NET40
        // 启动Web管理面板
        var set = Setting.Current;
        if (set.EnableWebPanel)
        {
            try
            {
                _webPanel = CreateWebPanel(this);
                _webPanel.Start();
            }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
            {
                // 端口被占用或保留时不打印完整堆栈，避免看起来像服务本身崩溃
                WriteLogPrefixed(LogLevel.Warn, "[WebPanel] ", "Web panel failed to start on port {0}: {1}. The port is already in use or reserved; stop the other instance or change WebPort/WebBind.", _webPanel?.Port ?? set.WebPort, ex.SocketErrorCode);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
#endif
    }

    private void OnProcessExit(Object sender, EventArgs e)
    {
        WriteLog("{0}.OnProcessExit", sender?.GetType().Name);
        if (Running) StopWork("ProcessExit");
        //Environment.ExitCode = 0;

        if (XTrace.Log is CompositeLog compositeLog)
        {
            var log = compositeLog.Get<TextFileLog>();
            log.TryDispose();
        }

        Running = false;
        _event?.Set();
    }

    /// <summary>停止服务</summary>
    /// <remarks>基类实现用于输出日志</remarks>
    /// <param name="reason"></param>
    public virtual void StopWork(String reason)
    {
        WriteLog("Service stopped: {0}", reason);

#if !NET40
        // 停止Web管理面板
        if (_webPanel != null)
        {
            try
            {
                _webPanel.Stop(reason);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            _webPanel = null;
        }
#endif
    }

    #endregion

    #region 服务维护
    /// <summary>服务管理线程封装。定期执行健康检查，超标时触发重启</summary>
    /// <param name="data">传入数据（未使用，保留扩展）</param>
    protected virtual void DoCheck(Object data)
    {
        // 如果某一项检查需要重启服务，则返回true，这里跳出循环，等待服务重启
        if (CheckMemory()) return;
        if (CheckThread()) return;
        if (CheckHandle()) return;
        if (CheckAutoRestart()) return;

        // 检查看门狗
        Command.Handle(CommandConst.WatchDog, null);
    }

    private DateTime _nextCollect;
    /// <summary>检查内存是否超标</summary>
    /// <returns>是否超标重启</returns>
    protected virtual Boolean CheckMemory()
    {
        var set = Setting.Current;
        if (set.FreeMemoryInterval > 0 && _nextCollect < DateTime.Now)
        {
            _nextCollect = DateTime.Now.AddSeconds(set.FreeMemoryInterval);

            FreeMemory();
        }

        using var p = Process.GetCurrentProcess();
        var memory = p.WorkingSet64 / 1024 / 1024;
        if (!HealthMonitor.IsMemoryOver(set, memory)) return false;

        return RequestRestart(L.T("Process memory usage {0:n0} MB exceeds the threshold {1:n0} MB; restarting.", memory, set.MaxMemory));
    }

    /// <summary>释放内存。GC回收后再释放虚拟内存</summary>
    public void FreeMemory()
    {
        var max = GC.MaxGeneration;
        var mode = GCCollectionMode.Forced;
        //#if NET7_0_OR_GREATER
#if NET8_0_OR_GREATER
        mode = GCCollectionMode.Aggressive;
#endif
#if NET451_OR_GREATER || NETSTANDARD || NETCOREAPP
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
#endif
        GC.Collect(max, mode);
        GC.WaitForPendingFinalizers();
        GC.Collect(max, mode);

        if (Runtime.Windows)
        {
            using var p = Process.GetCurrentProcess();
            NativeMethods.EmptyWorkingSet(p.Handle);
        }
    }

    /// <summary>检查服务进程的总线程数是否超标。超过 MaxThread 时触发重启</summary>
    /// <returns>是否触发重启</returns>
    protected virtual Boolean CheckThread()
    {
        var set = Setting.Current;
        using var p = Process.GetCurrentProcess();
        var count = p.Threads.Count;
        if (!HealthMonitor.IsThreadOver(set, count)) return false;

        return RequestRestart(L.T("Process thread count {0:n0} exceeds the threshold {1:n0}; restarting.", count, set.MaxThread));
    }

    /// <summary>检查服务进程的句柄数是否超标。超过 MaxHandle 时触发重启（Windows）</summary>
    /// <returns>是否触发重启</returns>
    protected virtual Boolean CheckHandle()
    {
        var set = Setting.Current;
        using var p = Process.GetCurrentProcess();
        var count = p.HandleCount;
        if (!HealthMonitor.IsHandleOver(set, count)) return false;

        return RequestRestart(L.T("Process handle count {0:n0} exceeds the threshold {1:n0}; restarting.", count, set.MaxHandle));
    }

    /// <summary>检查自动重启。达到 AutoRestart 分钟数且在 RestartTimeRange 内时触发重启</summary>
    /// <returns>是否触发重启</returns>
    protected virtual Boolean CheckAutoRestart()
    {
        var set = Setting.Current;
        var elapsed = Uptime;
        if (!HealthMonitor.IsAutoRestartDue(set, elapsed, DateTime.Now, out var inTimeRange)) return false;

        var message = inTimeRange
            ? L.T("Service has run for {0:n0} minutes, reaching the preset restart interval ({1:n0} minutes) within the configured time range ({2}); restarting!", elapsed.TotalMinutes, set.AutoRestart, set.RestartTimeRange)
            : L.T("Service has run for {0:n0} minutes, reaching the preset restart interval ({1:n0} minutes); restarting!", elapsed.TotalMinutes, set.AutoRestart);

        // 冷却期或重启失败时不重置计时，下一轮巡检继续尝试
        if (!RequestRestart(message)) return false;

        // 重置开始时间，防止检测时间过快，重启时间较长时，导致多次重复重启
        _uptime.Restart();
        UptimeOffset = TimeSpan.Zero;

        // 冷却基准与运行计时同源，必须一起归零。否则运行计时归零后，下一轮巡检用旧基准算出差值为负，
        // 冷却判据恒成立，内存/句柄/线程超限触发的重启会被屏蔽到运行时长重新超过旧值为止（可能长达数小时）
        _lastRestart = -1;

        if (Host is DefaultHost host && !host.InService) StopLoop();

        return true;
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);

    /// <summary>写日志。前缀在翻译之后拼接，不参与本地化</summary>
    /// <param name="prefix">日志前缀，可为空</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    public void WriteLogPrefixed(String prefix, String format, params Object[] args) => Log?.WriteLogPrefixed(prefix, format, args);

    /// <summary>写日志。前缀在翻译之后拼接，不参与本地化</summary>
    /// <param name="level">日志等级</param>
    /// <param name="prefix">日志前缀，可为空</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    public void WriteLogPrefixed(LogLevel level, String prefix, String format, params Object[] args) => Log?.WriteLogPrefixed(level, prefix, format, args);
    #endregion
}