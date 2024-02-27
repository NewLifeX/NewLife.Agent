using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
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
    #endregion

    #region 构造
    /// <summary>初始化</summary>
    public ServiceBase() =>
        //#if NETSTANDARD2_0
        //MachineInfo.RegisterAsync();
        //#endif

        InitService();

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
            }
        }
        else
        {
            if (!DisplayName.IsNullOrEmpty()) Console.Title = DisplayName;

            // 输出状态，菜单循环
            ShowStatus();
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

        Log = XTrace.Log;

        // 初始化配置
        var set = Setting.Current;
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
    protected virtual void ShowStatus()
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;

        var name = ServiceName;
        if (name != DisplayName)
            Console.WriteLine("服务：{0}({1})", DisplayName, name);
        else
            Console.WriteLine("服务：{0}", name);
        Console.WriteLine("描述：{0}", Description);
        Console.Write("状态：{0} ", Host.Name);

        String status;
        var installed = Host.IsInstalled(name);
        if (!installed)
            status = "未安装";
        else if (Host.IsRunning(name))
            status = "运行中";
        else
            status = "未启动";

        if (Runtime.Windows) status += $"（{(WindowsService.IsAdministrator() ? "管理员" : "普通用户")}）";

        Console.WriteLine(status);

        // 执行文件路径
        if (installed)
        {
            var cfg = Host.QueryConfig(name);
            if (cfg != null) Console.WriteLine("路径：{0}", cfg.FilePath);
        }

        var asm = AssemblyX.Create(Assembly.GetExecutingAssembly());
        Console.WriteLine();
        Console.WriteLine("{0}\t版本：{1}\t发布：{2:yyyy-MM-dd HH:mm:ss}", asm.Name, asm.FileVersion, asm.Compile);

        var asm2 = AssemblyX.Create(Assembly.GetEntryAssembly());
        if (asm2 != asm)
            Console.WriteLine("{0}\t版本：{1}\t发布：{2:yyyy-MM-dd HH:mm:ss}", asm2.Name, asm2.FileVersion, asm2.Compile);

        Console.ForegroundColor = color;
    }

    /// <summary>处理菜单</summary>
    protected virtual void ProcessMenu()
    {
        var service = this;
        var name = ServiceName;
        while (true)
        {
            //输出菜单
            ShowMenu();
            Console.Write("请选择操作（-x是命令行参数）：");

            //读取命令
            var key = Console.ReadKey();
            if (key.KeyChar == '0') break;
            Console.WriteLine();
            Console.WriteLine();

            try
            {
                switch (key.KeyChar)
                {
                    case '1':
                        //输出状态
                        ShowStatus();

                        break;
                    case '2':
                        if (Host.IsInstalled(name))
                            Host.Remove(name);
                        else
                            Install();
                        break;
                    case '3':
                        if (Host.IsRunning(name))
                            Host.Stop(name);
                        else
                            Host.Start(name);
                        // 稍微等一下状态刷新
                        Thread.Sleep(500);
                        break;
                    case '4':
                        if (Host.IsRunning(name))
                            Host.Restart(name);
                        // 稍微等一下状态刷新
                        Thread.Sleep(500);
                        break;
                    case '5':
                        #region 模拟运行
                        try
                        {
                            Console.WriteLine("正在模拟运行……");
                            StartWork("模拟运行开始");

                            // 开始辅助循环，检查状态
                            ThreadPool.QueueUserWorkItem(s => DoLoop());

                            Console.WriteLine("任意键结束模拟运行！");
                            Console.ReadKey(true);

                            _running = false;
                            StopWork("模拟运行停止");
                            ReleaseMemory();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                        #endregion
                        break;
                    //case '6':
                    //    InstallAutorun();
                    //    break;
                    case '7':
                        if (WatchDogs.Length > 0) CheckWatchDog();
                        break;
                    default:
                        // 自定义菜单
                        var menu = _Menus.FirstOrDefault(e => e.Key == key.KeyChar);
                        menu?.Callback();
                        break;
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
        }
    }

    /// <summary>显示菜单</summary>
    protected virtual void ShowMenu()
    {
        var name = ServiceName;

        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.WriteLine();
        Console.WriteLine("1 显示状态");

        var run = false;
        if (Host.IsInstalled(name))
        {
            if (Host.IsRunning(name))
            {
                run = true;
                Console.WriteLine("3 停止服务 -stop");
                Console.WriteLine("4 重启服务 -restart");
            }
            else
            {
                Console.WriteLine("2 卸载服务 -u");
                Console.WriteLine("3 启动服务 -start");
            }
        }
        else
        {
            Console.WriteLine("2 安装服务 -i");
        }

        if (!run)
        {
            Console.WriteLine("5 模拟运行 -run");
        }

        //if (Runtime.Windows)
        //{
        //    Console.WriteLine("6 安装开机自启 -autorun");
        //}

        var dogs = WatchDogs;
        if (dogs.Length > 0)
        {
            Console.WriteLine("7 看门狗保护服务 {0}", dogs.Join());
        }

        if (_Menus.Count > 0)
        {
            //foreach (var item in _Menus)
            //{
            //    Console.WriteLine("{0} {1}", item.Key, item.Value.Name);
            //}
            OnShowMenu(_Menus);
        }

        Console.WriteLine("0 退出");

        Console.ForegroundColor = color;
    }

    /// <summary>
    /// 显示自定义菜单
    /// </summary>
    /// <param name="menus"></param>
    protected virtual void OnShowMenu(IList<Menu> menus)
    {
        foreach (var item in menus)
        {
            Console.WriteLine("{0} {1}", item.Key, item.Name);
        }
    }

    private readonly List<Menu> _Menus = new();
    /// <summary>添加菜单</summary>
    /// <param name="key"></param>
    /// <param name="name"></param>
    /// <param name="callbak"></param>
    public void AddMenu(Char key, String name, Action callbak)
    {
        //if (!_Menus.ContainsKey(key))
        //{
        _Menus.RemoveAll(e => e.Key == key);
        _Menus.Add(new Menu(key, name, callbak));
        //}
    }

    /// <summary>菜单项</summary>
    public class Menu
    {
        /// <summary>按键</summary>
        public Char Key { get; set; }

        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>回调方法</summary>
        public Action Callback { get; set; }

        /// <summary>
        /// 实例化
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="callback"></param>
        public Menu(Char key, String name, Action callback)
        {
            Key = key;
            Name = name;
            Callback = callback;
        }
    }

    /// <summary>处理命令</summary>
    /// <param name="cmd"></param>
    /// <param name="args"></param>
    protected virtual void ProcessCommand(String cmd, String[] args)
    {
        var name = ServiceName;
        WriteLog("ProcessCommand cmd={0} args={1}", cmd, args.Join(" "));

        cmd = cmd.ToLower();
        switch (cmd)
        {
            case "-s":
                Host.Run(this);
                break;
            case "-i":
                Install();
                break;
            case "-u":
                Host.Remove(name);
                break;
            case "-start":
                Host.Start(name);
                break;
            case "-stop":
                Host.Stop(name);
                break;
            case "-restart":
                Host.Restart(name);
                break;
            case "-install":
                // 可能服务已存在，安装时报错，但不要影响服务启动
                try
                {
                    Install();
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
                // 稍微等待
                for (var i = 0; i < 50; i++)
                {
                    if (Host.IsInstalled(name)) break;
                    Thread.Sleep(100);
                }
                Host.Start(name);
                break;
            case "-uninstall":
                try
                {
                    Host.Stop(name);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
                Host.Remove(name);
                break;
            case "-reinstall":
                Reinstall(name);
                break;
            case "-run":
                if ("-delay".EqualIgnoreCase(args)) Thread.Sleep(5_000);
                StartLoop();
                DoLoop();
                StopLoop();
                break;
            default:
                // 快速调用自定义菜单
                if (cmd.Length == 2 && cmd[0] == '-')
                {
                    var menu = _Menus.FirstOrDefault(e => e.Key == cmd[1]);
                    menu?.Callback();
                }
                break;
        }

        WriteLog("ProcessFinished cmd={0}", cmd);
    }
    #endregion

    #region 服务控制
    private Boolean _running;
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
        _running = true;
        while (_running)
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

            _event.WaitOne(10_000);
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
        if (!_running) return;

        StopWork("StopLoop");

        _running = false;
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
    protected virtual void StartWork(String reason) => WriteLog("服务启动 {0}", reason);

    private void OnProcessExit(Object sender, EventArgs e)
    {
        WriteLog("OnProcessExit");
        if (_running) StopWork("ProcessExit");
        //Environment.ExitCode = 0;

        if (XTrace.Log is CompositeLog compositeLog)
        {
            var log = compositeLog.Get<TextFileLog>();
            log.TryDispose();
        }

        _running = false;
        _event?.Set();
    }

    /// <summary>停止服务</summary>
    /// <remarks>基类实现用于输出日志</remarks>
    /// <param name="reason"></param>
    protected virtual void StopWork(String reason) => WriteLog("服务停止 {0}", reason);

    private void Install()
    {
        var exe = GetExeName();

        // 兼容dotnet
        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 1)
        {
            var fileName = Path.GetFileName(exe);
            if (fileName.EqualIgnoreCase("dotnet", "dotnet.exe"))
                exe += " " + args[0].GetFullPath();
            else if (fileName.EqualIgnoreCase("mono", "mono.exe", "mono-sgen"))
                exe = args[0].GetFullPath();
        }

        var arg = UseAutorun ? "-run" : "-s";

        // 兼容更多参数做为服务启动，譬如：--urls
        if (args.Length > 2)
        {
            // 跳过系统内置参数
            var list = new List<String>();
            for (var i = 2; i < args.Length; i++)
            {
                if (args[i].EqualIgnoreCase("-server", "-user", "-group"))
                    i++;
                else
                    list.Add(args[i]);
            }
            if (list.Count > 0) arg += " " + list.Join(" ");
        }

        Host.Install(ServiceName, DisplayName, exe, arg, Description);
    }

    /// <summary>Exe程序名</summary>
    public virtual String GetExeName()
    {
        var p = Process.GetCurrentProcess();
        var filename = p.MainModule.FileName;
        //filename = Path.GetFileName(filename);
        filename = filename.Replace(".vshost.", ".");

        return filename;
    }

    private void Reinstall(String name)
    {
        try
        {
            Host.Stop(name);
            Host.Remove(name);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        Install();
        // 稍微等待
        for (var i = 0; i < 50; i++)
        {
            if (Host.IsInstalled(name)) break;
            Thread.Sleep(100);
        }
        Host.Start(name);
    }
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
        CheckWatchDog();
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
    protected void ReleaseMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

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

        WriteLog("服务已运行 {0:n0}分钟，达到预设重启时间（{1:n0}分钟），准备重启！", ts.TotalMinutes, auto);

        Host.Restart(ServiceName);

        if (Host is DefaultHost host && !host.InService) StopLoop();

        return true;
    }
    #endregion

    #region 看门狗
    /// <summary>看门狗要保护的服务</summary>
    public static String[] WatchDogs => Setting.Current.WatchDog.Split(",", ";");

    /// <summary>检查看门狗。</summary>
    /// <remarks>
    /// XAgent看门狗功能由管理线程完成，每分钟一次。
    /// 检查指定的任务是否已经停止，如果已经停止，则启动它。
    /// </remarks>
    public void CheckWatchDog()
    {
        var ss = WatchDogs;
        if (ss == null || ss.Length < 1) return;

        foreach (var item in ss)
        {
            // 已安装未运行
            if (!Host.IsInstalled(item))
                XTrace.WriteLine("未发现服务{0}，是否已安装？", item);
            else if (!Host.IsRunning(item))
            {
                XTrace.WriteLine("发现服务{0}被关闭，准备启动！", item);

                Host.Start(item);
            }
        }
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