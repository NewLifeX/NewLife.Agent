using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Security;
using NewLife.Agent.Command;
using NewLife.Agent.Windows;
using NewLife.Log;
using NewLife.Reflection;

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

    /// <summary>描述</summary>
    public String Description { get; set; }

    /// <summary>是否使用自启动。自启动需要用户登录桌面，默认false使用系统服务</summary>
    public Boolean UseAutorun { get; set; }

    /// <summary>运行中</summary>
    public Boolean Running { get; set; }

    /// <summary>命令工厂</summary>
    public CommandFactory Command { get; }
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
        args ??= Environment.GetCommandLineArgs();

        if ("-Autorun".EqualIgnoreCase(args)) UseAutorun = true;

        Init();

        var cmd = args?.FirstOrDefault(e => !e.IsNullOrEmpty() && e.Length > 1 && e[0] == '-');
        if (!cmd.IsNullOrEmpty())
        {
            try
            {
                WriteLog("ProcessCommand cmd={0} args={1}", cmd, args.Join(" "));
                cmd = cmd.ToLower();
                Command.Handle(cmd, args);
                WriteLog("ProcessFinished cmd={0}", cmd);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
        else
        {
            if (!DisplayName.IsNullOrEmpty()) Console.Title = DisplayName;

            Command.Handle(CommandConst.ShowStatus, args);
            // 输出状态，菜单循环
            ProcessMenu();
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

        set.Save();
    }

    /// <summary>显示状态</summary>
    protected virtual void ProcessMenu()
    {
        var service = this;
        var name = ServiceName;
        var args = Environment.GetCommandLineArgs();
        while (true)
        {
            //输出菜单
            ShowMenu();
            Console.Write("请输入命令序号：");
            Console.WriteLine();

            //读取命令
            var key = Console.ReadKey();
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
                        Console.WriteLine($"您输入的命令序号 [{key.KeyChar}] 无效，请重新输入！");
                    }
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            Console.WriteLine();
            Thread.Sleep(1000);
        }
    }

    /// <summary>显示菜单</summary>
    protected virtual void ShowMenu()
    {
        var name = ServiceName;

        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.WriteLine();
        Console.WriteLine($"序号 功能名称\t命令行参数");
        var menus = Command.GetShortcutMenu();
        foreach (var menu in menus)
        {
            Console.WriteLine($" {menu.Key}、 {menu.Name}\t{menu.Cmd}");
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
                Console.WriteLine($" {menu.Key}、 {menu.Name}\t");
            }
        }

        Console.WriteLine($" 0、 退出\t");
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

    #endregion

    #region 服务控制
    private AutoResetEvent _event;
    private Process _process;

    /// <summary>主循环</summary>
    internal void DoLoop()
    {
        // 启动后命令，服务启动后执行的命令
        var set = Setting.Current;
        if (!set.AfterStart.IsNullOrEmpty())
        {
            try
            {
                var file = set.AfterStart;
                var args = "";
                var p = file.IndexOf(" ");
                if (p > 0)
                {
                    args = file.Substring(p + 1);
                    file = file.Substring(0, p);
                }
                WriteLog("启动后执行：FileName={0} Args={1}", file, args);

                var si = new ProcessStartInfo(file.GetFullPath(), args)
                {
                    WorkingDirectory = ".".GetFullPath()
                };
                _process = Process.Start(si);
                WriteLog("进程：[{0}]{1}", _process.Id, _process.ProcessName);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }

        _event = new AutoResetEvent(false);
        Running = true;
        while (Running)
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

            _event.WaitOne(set.WatchInterval * 1000);
        }

        _event.Dispose();
        _event = null;
    }

    /// <summary>开始循环</summary>
    protected internal void StartLoop()
    {
        NewLife.Model.Host.RegisterExit(OnProcessExit);

        //GetType().Assembly.WriteVersion();

        //StartWork("StartLoop");

        var task = Task.Factory.StartNew(() => StartWork("StartLoop"));
        if (!task.Wait(3_000)) XTrace.WriteLine("服务启动函数StartWork耗时过长，建议优化，StartWork应该避免阻塞操作！");
    }

    /// <summary>停止循环</summary>
    protected internal void StopLoop()
    {
        if (!Running) return;

        StopWork("StopLoop");

        Running = false;
        _event?.Set();

        try
        {
            _process?.Kill();
            _process = null;
        }
        catch { }

        ReleaseMemory();
    }

    /// <summary>开始工作</summary>
    /// <remarks>基类实现用于输出日志</remarks>
    /// <param name="reason"></param>
    public virtual void StartWork(String reason) => WriteLog("服务启动 {0}", reason);

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
    public virtual void StopWork(String reason) => WriteLog("服务停止 {0}", reason);

    #endregion

    #region 服务维护
    /// <summary>服务管理线程封装</summary>
    /// <param name="data"></param>
    protected virtual void DoCheck(Object data)
    {
        // 如果某一项检查需要重启服务，则返回true，这里跳出循环，等待服务重启
        if (CheckMemory()) return;
        if (CheckThread()) return;
        if (CheckHandle()) return;
        if (CheckAutoRestart()) return;

        // 检查看门狗
        Command.Handle(CommandConst.WatchDog);
    }

    private DateTime _nextCollect;
    /// <summary>检查内存是否超标</summary>
    /// <returns>是否超标重启</returns>
    protected virtual Boolean CheckMemory()
    {
        var max = Setting.Current.MaxMemory;
        if (max <= 0) return false;

        if (_nextCollect < DateTime.Now)
        {
            _nextCollect = DateTime.Now.AddSeconds(600);

            ReleaseMemory();
        }

        var cur = GC.GetTotalMemory(false);
        cur = cur / 1024 / 1024;
        if (cur < max) return false;

        //        // 执行一次GC回收
        //#if NETFRAMEWORK
        //        GC.Collect(2, GCCollectionMode.Forced);
        //#else
        //        GC.Collect(2, GCCollectionMode.Forced, false);
        //#endif

        //        // 再次判断内存
        //        cur = GC.GetTotalMemory(true);
        //        cur = cur / 1024 / 1024;
        //        if (cur < max) return false;

        WriteLog("当前进程占用内存 {0:n0}M，超过阀值 {1:n0}M，准备重新启动！", cur, max);

        Host.Restart(ServiceName);

        return true;
    }

    /// <summary>释放内存。GC回收后再释放虚拟内存</summary>
    public void ReleaseMemory()
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
            var p = Process.GetCurrentProcess();
            NativeMethods.EmptyWorkingSet(p.Handle);
        }
    }

    /// <summary>检查服务进程的总线程数是否超标</summary>
    /// <returns></returns>
    protected virtual Boolean CheckThread()
    {
        var max = Setting.Current.MaxThread;
        if (max <= 0) return false;

        var p = Process.GetCurrentProcess();
        if (p.Threads.Count < max) return false;

        WriteLog("当前进程总线程 {0:n0}个，超过阀值 {1:n0}个，准备重新启动！", p.Threads.Count, max);

        Host.Restart(ServiceName);

        return true;
    }

    /// <summary>检查服务进程的句柄数是否超标</summary>
    /// <returns></returns>
    protected virtual Boolean CheckHandle()
    {
        var max = Setting.Current.MaxHandle;
        if (max <= 0) return false;

        var p = Process.GetCurrentProcess();
        if (p.HandleCount < max) return false;

        WriteLog("当前进程句柄 {0:n0}个，超过阀值 {1:n0}个，准备重新启动！", p.HandleCount, max);

        Host.Restart(ServiceName);

        return true;
    }

    /// <summary>服务开始时间</summary>
    private readonly DateTime Start = DateTime.Now;

    /// <summary>检查自动重启</summary>
    /// <returns></returns>
    protected virtual Boolean CheckAutoRestart()
    {
        var auto = Setting.Current.AutoRestart;
        if (auto <= 0) return false;

        var ts = DateTime.Now - Start;
        if (ts.TotalMinutes < auto) return false;

        var timeRange = Setting.Current.RestartTimeRange?.Split('-');
        if (timeRange?.Length == 2
            && TimeSpan.TryParse(timeRange[0], out var startTime) && startTime <= DateTime.Now.TimeOfDay
            && TimeSpan.TryParse(timeRange[1], out var endTime) && endTime >= DateTime.Now.TimeOfDay)
        {
            WriteLog("服务已运行 {0:n0}分钟，达到预设重启时间（{1:n0}分钟），并且当前时间在预设时间范围之内（{2}），准备重启！", ts.TotalMinutes, auto, Setting.Current.RestartTimeRange);
        }
        else
        {
            WriteLog("服务已运行 {0:n0}分钟，达到预设重启时间（{1:n0}分钟），准备重启！", ts.TotalMinutes, auto);
        }

        Host.Restart(ServiceName);

        if (Host is DefaultHost host && !host.InService) StopLoop();

        return true;
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}