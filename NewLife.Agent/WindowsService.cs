using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using NewLife.Agent.Windows;
using NewLife.Log;
using static NewLife.Agent.Advapi32;

namespace NewLife.Agent;

/// <summary>Windows服务</summary>
/// <remarks>
/// 特别注意，主线程和服务内部线程，不要调用任何系统WinApi
/// </remarks>
public class WindowsService : DefaultHost
{
    //private ServiceBase _service;
    private SERVICE_STATUS _status;
    private ControlsAccepted _acceptedCommands;
    private IntPtr _statusHandle;
    private SERVICE_TABLE_ENTRY _table;
    private ServiceControlCallbackEx _commandHandler;

    /// <summary>开始执行服务</summary>
    /// <param name="service"></param>
    public override void Run(ServiceBase service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        // 以服务运行
        InService = true;

        var num = Marshal.SizeOf(typeof(SERVICE_TABLE_ENTRY));
        var table = Marshal.AllocHGlobal((IntPtr)((1 + 1) * num));
        var handleName = Marshal.StringToHGlobalUni(service.ServiceName);
        try
        {
            // Win32OwnProcess/StartPending
            _status.serviceType = ServiceType.Win32OwnProcess;
            _status.currentState = ServiceControllerStatus.StartPending;
            _status.controlsAccepted = 0;
            _status.win32ExitCode = 0;
            _status.serviceSpecificExitCode = 0;
            _status.checkPoint = 0;
            _status.waitHint = 0;

            // 正常运行后可接受的命令
#if NETSTANDARD2_0 || NETCOREAPP
            _acceptedCommands = ControlsAccepted.CanStop
                | ControlsAccepted.CanShutdown
                | ControlsAccepted.CanPauseAndContinue
                | ControlsAccepted.ParamChange
                | ControlsAccepted.NetBindChange
                | ControlsAccepted.HardwareProfileChange
                | ControlsAccepted.CanHandlePowerEvent
                | ControlsAccepted.CanHandleSessionChangeEvent
                | ControlsAccepted.PreShutdown
                | ControlsAccepted.TimeChange
                | ControlsAccepted.TriggerEvent
                //| ControlsAccepted.UserModeReboot
                ;
#else
            _acceptedCommands = ControlsAccepted.CanStop
                | ControlsAccepted.CanShutdown
                | ControlsAccepted.CanPauseAndContinue
                | ControlsAccepted.ParamChange
                | ControlsAccepted.NetBindChange
                | ControlsAccepted.HardwareProfileChange
                | ControlsAccepted.CanHandlePowerEvent
                | ControlsAccepted.CanHandleSessionChangeEvent
                | ControlsAccepted.PreShutdown
                | ControlsAccepted.TimeChange
                | ControlsAccepted.TriggerEvent
                //| ControlsAccepted.UserModeReboot
                ;
#endif

            //!!! 函数委托必须引着，避免GC回收导致PInvoke内部报错
            _table = new SERVICE_TABLE_ENTRY
            {
                callback = ServiceMainCallback,
                name = handleName
            };
            Marshal.StructureToPtr(_table, table, true);

            var result2 = new SERVICE_TABLE_ENTRY
            {
                callback = null,
                name = IntPtr.Zero
            };
            Marshal.StructureToPtr(result2, table + num, true);

            /*
             * 如果StartServiceCtrlDispatcher函数执行成功，调用线程（也就是服务进程的主线程）不会返回，直到所有的服务进入到SERVICE_STOPPED状态。
             * 调用线程扮演着控制分发的角色，干这样的事情：
             * 1、在新的服务启动时启动新线程去调用服务主函数（主意：服务的任务是在新线程中做的）；
             * 2、当服务有请求时（注意：请求是由SCM发给它的），调用它对应的处理函数（主意：这相当于主线程“陷入”了，它在等待控制消息并对消息做处理）。
             */

            //XTrace.WriteLine("运行服务 {0}", service.ServiceName);

            var flag = StartServiceCtrlDispatcher(table);
            if (!flag)
            {
                XTrace.WriteLine(new Win32Exception().Message);
            }

            XTrace.WriteLine("退出服务 {0} CtrlDispatcher={1}", service.ServiceName, flag);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            XTrace.WriteLine("运行服务 {0} 出错，{1}", Service.ServiceName, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }
        finally
        {
            if (table != IntPtr.Zero) Marshal.FreeHGlobal(table);
            if (handleName != IntPtr.Zero) Marshal.FreeHGlobal(handleName);

            //_service.TryDispose();
        }
    }

    [ComVisible(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    private void ServiceMainCallback(Int32 argCount, IntPtr argPointer)
    {
        //!!! 函数委托必须引着，避免GC回收导致PInvoke内部报错
        _commandHandler = ServiceCommandCallbackEx;
        _statusHandle = RegisterServiceCtrlHandlerEx(Service.ServiceName, _commandHandler, IntPtr.Zero);

        if (ReportStatus(ServiceControllerStatus.StartPending, 3000))
        {
            //!!! 不要在ServiceMain里面调用系统函数
            ThreadPool.QueueUserWorkItem(ServiceQueuedMainCallback);
        }
    }

    private void ServiceQueuedMainCallback(Object state)
    {
        try
        {
            // 启动初始化
            Service.StartLoop();

            ReportStatus(ServiceControllerStatus.Running);

            // 阻塞
            Service.DoLoop();

            if (_status.currentState == ServiceControllerStatus.Running)
            {
                ReportStatus(ServiceControllerStatus.StopPending);

                // 停止
                Service.StopLoop();

                ReportStatus(ServiceControllerStatus.Stopped);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            XTrace.WriteLine("运行服务{0}失败，{1}", Service.ServiceName, new Win32Exception(Marshal.GetLastWin32Error()).Message);

            ReportStatus(ServiceControllerStatus.Stopped);
        }
    }

    private Int32 ServiceCommandCallbackEx(ControlOptions command, Int32 eventType, IntPtr eventData, IntPtr eventContext)
    {
        if (command is not ControlOptions.PowerEvent and
            not ControlOptions.SessionChange and
            not ControlOptions.Interrogate)
            XTrace.WriteLine("ServiceCommandCallbackEx(command={0}, eventType={1}, eventData={2:x}, eventContext={3:x})", command, eventType, eventData, eventContext);

        switch (command)
        {
            case ControlOptions.Interrogate:
                ReportStatus(_status.currentState, 0, false);
                break;
            case ControlOptions.Stop:
            case ControlOptions.Shutdown:
                ReportStatus(ServiceControllerStatus.StopPending);
                ThreadPool.QueueUserWorkItem(s =>
                {
                    try
                    {
                        Service.StopLoop();
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteException(ex);
                    }
                    ReportStatus(ServiceControllerStatus.Stopped);
                });
                break;
            case ControlOptions.PowerEvent:
                var power = new PowerStatus();
                XTrace.WriteLine("PowerEvent: {0}, LineStatus={1}, LifePercent={2:p0}, ChargeStatus={3}", (PowerBroadcastStatus)eventType, power.PowerLineStatus, power.BatteryLifePercent, power.BatteryChargeStatus);
                break;
            case ControlOptions.SessionChange:
                var sessionNotification = new WTSSESSION_NOTIFICATION();
                Marshal.PtrToStructure(eventData, sessionNotification);
                XTrace.WriteLine("SessionChange {0}, sessionId={1}", (SessionChangeReason)eventType, sessionNotification.sessionId);
                break;
            case ControlOptions.TimeChange:
                var time = new SERVICE_TIMECHANGE_INFO();
                Marshal.PtrToStructure(eventData, time);
                XTrace.WriteLine("TimeChange {0}=>{1}", DateTime.FromFileTime(time.OldTime), DateTime.FromFileTime(time.NewTime));
                break;
            default:
                ReportStatus(_status.currentState);
                break;
        }

        return 0;
    }

    private Int32 _checkPoint = 1;
    private unsafe Boolean ReportStatus(ServiceControllerStatus state, Int32 waitHint = 0, Boolean showLog = true)
    {
        if (showLog)
        {
            if (waitHint > 0)
                XTrace.WriteLine("ReportStatus {0}, {1}", state, waitHint);
            else
                XTrace.WriteLine("ReportStatus {0}", state);
        }

        fixed (SERVICE_STATUS* status = &_status)
        {
            // 开始挂起时，不接受任何命令；其它状态下允许停止
            if (state == ServiceControllerStatus.StartPending)
                _status.controlsAccepted = 0;
            else
                //_status.controlsAccepted = ControlsAccepted.CanStop;
                _status.controlsAccepted = _acceptedCommands;

            // 正在运行和已经停止，检查点为0；其它状态累加检查点
            _status.checkPoint = state is ServiceControllerStatus.Running or
                ServiceControllerStatus.Stopped
                ? 0
                : _checkPoint++;

            _status.waitHint = waitHint;
            _status.currentState = state;

            return SetServiceStatus(_statusHandle, status);
        }
    }

    #region 服务状态和控制
    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean IsInstalled(String serviceName)
    {
        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_CONNECT));
        if (manager == null || manager.IsInvalid) return false;

        using var service = new SafeServiceHandle(OpenService(manager, serviceName, ServiceOptions.SERVICE_QUERY_CONFIG));
        return service != null && !service.IsInvalid;
    }

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override unsafe Boolean IsRunning(String serviceName)
    {
        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_CONNECT));
        if (manager == null || manager.IsInvalid) return false;

        using var service = new SafeServiceHandle(OpenService(manager, serviceName, ServiceOptions.SERVICE_QUERY_STATUS));
        if (service == null || service.IsInvalid) return false;

        SERVICE_STATUS status = default;
        return !QueryServiceStatus(service, &status)
            ? throw new Win32Exception(Marshal.GetLastWin32Error())
            : status.currentState == ServiceControllerStatus.Running;
    }

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName"></param>
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description"></param>
    /// <returns></returns>
    public override Boolean Install(String serviceName, String displayName, String fileName, String arguments, String description)
    {
        XTrace.WriteLine("{0}.Install {1}, {2}, {3}, {4}", GetType().Name, serviceName, displayName, fileName, arguments, description);

        if (!IsAdministrator()) return RunAsAdministrator("-i");

        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_CREATE_SERVICE));
        if (manager.IsInvalid)
        {
            XTrace.WriteLine("安装Windows服务要求以管理员运行");
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var binPath = fileName;
        if (!arguments.IsNullOrEmpty()) binPath += " " + arguments;

        using var service = new SafeServiceHandle(CreateService(manager, serviceName, displayName, ServiceOptions.SERVICE_ALL_ACCESS, 0x10, 2, 1, binPath, null, 0, null, null, null));
        if (service.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

        // 设置描述信息
        if (!description.IsNullOrEmpty())
        {
            SERVICE_DESCRIPTION sd;
            sd.Description = Marshal.StringToHGlobalUni(description);
            var lpInfo = Marshal.AllocHGlobal(Marshal.SizeOf(sd));

            try
            {
                Marshal.StructureToPtr(sd, lpInfo, false);

                const Int32 SERVICE_CONFIG_DESCRIPTION = 1;
                ChangeServiceConfig2(service, SERVICE_CONFIG_DESCRIPTION, lpInfo);
            }
            finally
            {
                Marshal.FreeHGlobal(lpInfo);
                Marshal.FreeHGlobal(sd.Description);
            }
        }

        return true;
    }

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override unsafe Boolean Remove(String serviceName)
    {
        XTrace.WriteLine("{0}.Remove {1}", GetType().Name, serviceName);

        if (!IsAdministrator()) return RunAsAdministrator("-uninstall");

        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_ALL));
        if (manager.IsInvalid)
        {
            XTrace.WriteLine("卸载Windows服务要求以管理员运行");
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        using var service = new SafeServiceHandle(OpenService(manager, serviceName, ServiceOptions.SERVICE_STOP | ServiceOptions.STANDARD_RIGHTS_DELETE));
        if (service.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

        SERVICE_STATUS status = default;
        ControlService(service, ControlOptions.Stop, &status);

        return DeleteService(service) == 0 ? throw new Win32Exception(Marshal.GetLastWin32Error()) : true;
    }

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override Boolean Start(String serviceName)
    {
        XTrace.WriteLine("{0}.Start {1}", GetType().Name, serviceName);

        if (!IsAdministrator()) return RunAsAdministrator("-start");

        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_CONNECT));
        if (manager.IsInvalid)
        {
            XTrace.WriteLine("启动Windows服务要求以管理员运行");
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        using var service = new SafeServiceHandle(OpenService(manager, serviceName, ServiceOptions.SERVICE_START));
        return service.IsInvalid
            ? throw new Win32Exception(Marshal.GetLastWin32Error())
            : !StartService(service, 0, IntPtr.Zero) ? throw new Win32Exception(Marshal.GetLastWin32Error()) : true;
    }

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public override unsafe Boolean Stop(String serviceName)
    {
        XTrace.WriteLine("{0}.Stop {1}", GetType().Name, serviceName);

        if (!IsAdministrator()) return RunAsAdministrator("-stop");

        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_ALL));
        if (manager.IsInvalid)
        {
            XTrace.WriteLine("停止Windows服务要求以管理员运行");
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        using var service = new SafeServiceHandle(OpenService(manager, serviceName, ServiceOptions.SERVICE_STOP));
        if (service.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

        SERVICE_STATUS status = default;
        return !ControlService(service, ControlOptions.Stop, &status) ? throw new Win32Exception(Marshal.GetLastWin32Error()) : true;
    }

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public override Boolean Restart(String serviceName)
    {
        XTrace.WriteLine("{0}.Restart {1}", GetType().Name, serviceName);

        if (!IsAdministrator()) return RunAsAdministrator("-restart");

        //if (InService)
        {
            var cmd = $"/c net stop {serviceName} & ping 127.0.0.1 -n 5 & net start {serviceName}";
            Process.Start("cmd.exe", cmd);
        }
        //else
        //{
        //    Process.Start(Service.GetExeName(), "-run -delay");
        //}

        //// 在临时目录生成重启服务的批处理文件
        //var filename = "重启.bat".GetFullPath();
        //if (File.Exists(filename)) File.Delete(filename);

        //File.AppendAllText(filename, "net stop " + serviceName);
        //File.AppendAllText(filename, Environment.NewLine);
        //File.AppendAllText(filename, "ping 127.0.0.1 -n 5 > nul ");
        //File.AppendAllText(filename, Environment.NewLine);
        //File.AppendAllText(filename, "net start " + serviceName);

        ////执行重启服务的批处理
        ////RunCmd(filename, false, false);
        //var p = new Process();
        //var si = new ProcessStartInfo
        //{
        //    FileName = filename,
        //    UseShellExecute = true,
        //    CreateNoWindow = true
        //};
        //p.StartInfo = si;

        //p.Start();

        ////if (File.Exists(filename)) File.Delete(filename);

        return true;
    }

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    public override unsafe ServiceConfig QueryConfig(String serviceName)
    {
        using var manager = new SafeServiceHandle(OpenSCManager(null, null, ServiceControllerOptions.SC_MANAGER_CONNECT));
        if (manager == null || manager.IsInvalid) return null;

        using var service = new SafeServiceHandle(OpenService(manager, serviceName, ServiceOptions.SERVICE_QUERY_CONFIG));
        if (service == null || service.IsInvalid) return null;

        Int32 bytesNeeded;
        if (!QueryServiceConfig(service, IntPtr.Zero, 0, &bytesNeeded))
        {
            const Int32 ERROR_INSUFFICIENT_BUFFER = 122;
            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_INSUFFICIENT_BUFFER) throw new Win32Exception(error);
        }

        var ptr = Marshal.AllocHGlobal(bytesNeeded);
        try
        {
            if (!QueryServiceConfig(service, ptr, bytesNeeded, &bytesNeeded))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            //SERVICE_CONFIG config = default;
            var config = (SERVICE_CONFIG)Marshal.PtrToStructure(ptr, typeof(SERVICE_CONFIG));

            var cfg = new ServiceConfig
            {
                Name = serviceName,
                DisplayName = Marshal.PtrToStringUni(config.DisplayName),
                FilePath = Marshal.PtrToStringUni(config.BinaryPathName),
                AutoStart = config.startType == StartType.AutoStart,
            };

            return cfg;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>以管理员身份运行进程</summary>
    /// <param name="argument"></param>
    /// <returns></returns>
    public static Boolean RunAsAdministrator(String argument)
    {
        var exe = ExecutablePath;
        if (exe.IsNullOrEmpty()) return false;

        if (exe.EndsWithIgnoreCase(".dll"))
        {
            var exe2 = Path.ChangeExtension(exe, ".exe");
            if (File.Exists(exe2)) exe = exe2;
        }

        var startInfo = exe.EndsWithIgnoreCase(".dll") ?
            new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"{Path.GetFileName(exe)} {argument}",
                WorkingDirectory = Path.GetDirectoryName(exe),
                Verb = "runas",
                UseShellExecute = true,
            } :
            new ProcessStartInfo
            {
                FileName = exe,
                Arguments = argument,
                Verb = "runas",
                UseShellExecute = true,
            };

        var p = Process.Start(startInfo);
        return !p.WaitForExit(5_000) || p.ExitCode == 0;
    }

    static String _executablePath;
    static String ExecutablePath
    {
        get
        {
            if (_executablePath == null)
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
#if NETFRAMEWORK
                    var codeBase = entryAssembly.CodeBase;
#else
                    var codeBase = entryAssembly.Location;
#endif
                    var uri = new Uri(codeBase);
                    _executablePath = uri.IsFile ? uri.LocalPath + Uri.UnescapeDataString(uri.Fragment) : uri.ToString();
                }
                else
                {
                    var moduleFileNameLongPath = GetModuleFileNameLongPath(new HandleRef(null, IntPtr.Zero));
                    _executablePath = moduleFileNameLongPath.ToString().GetFullPath();
                }
            }

            return _executablePath;
        }
    }

    static StringBuilder GetModuleFileNameLongPath(HandleRef hModule)
    {
        var sb = new StringBuilder(260);
        var num = 1;
        var num2 = 0;
        while ((num2 = GetModuleFileName(hModule, sb, sb.Capacity)) == sb.Capacity && Marshal.GetLastWin32Error() == 122 && sb.Capacity < 32767)
        {
            num += 2;
            var capacity = (num * 260 < 32767) ? (num * 260) : 32767;
            sb.EnsureCapacity(capacity);
        }
        sb.Length = num2;
        return sb;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern Int32 GetModuleFileName(HandleRef hModule, StringBuilder buffer, Int32 length);

    /// <summary>当前进程是否管理员角色</summary>
    /// <returns></returns>
    public static Boolean IsAdministrator()
    {
#if NETFRAMEWORK
        if (Runtime.Windows)
        {
            var current = WindowsIdentity.GetCurrent();
            var windowsPrincipal = new WindowsPrincipal(current);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }
#elif NETCOREAPP
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var current = WindowsIdentity.GetCurrent();
            var windowsPrincipal = new WindowsPrincipal(current);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }
#endif
        return true;
    }
    #endregion
}