using System;
using System.Runtime.InteropServices;

namespace NewLife.Agent;

internal class SafeServiceHandle : SafeHandle
{
    public override Boolean IsInvalid
    {
        get
        {
            if (!(DangerousGetHandle() == IntPtr.Zero))
            {
                return DangerousGetHandle() == new IntPtr(-1);
            }
            return true;
        }
    }

    internal SafeServiceHandle(IntPtr handle)
        : base(IntPtr.Zero, true) => SetHandle(handle);

    protected override Boolean ReleaseHandle() => Advapi32.CloseServiceHandle(handle);
}

/// <summary>指示系统的电源状态</summary>
public enum PowerBroadcastStatus
{
    /// <summary>电池电量不足</summary>
    BatteryLow = 9,
    /// <summary>发出 APM OEM 事件信号的高级电源管理 (APM) BIOS</summary>
    OemEvent = 11,
    /// <summary>检测到计算机电源状态的更改，如从电池电源切换到交流电源。系统还会在剩余电池电量降至用户指定的阈值以下时或电池电量变化了指定的百分比时广播该事件。</summary>
    PowerStatusChange = 10,
    /// <summary>系统已请求挂起计算机的权限。授予权限的应用程序应在返回之前执行挂起准备。</summary>
    QuerySuspend = 0,
    /// <summary>拒绝授予系统挂起计算机的权限。当任何应用程序或驱动程序拒绝了上一个 QuerySuspend 状态时将广播该状态。</summary>
    QuerySuspendFailed = 2,
    /// <summary>计算机已自动唤醒来处理事件。如果系统在广播 ResumeAutomatic 后检测到任何用户活动，则会广播 ResumeSuspend 事件，使应用程序知道他们可以恢复与用户的完全交互。</summary>
    ResumeAutomatic = 18,
    /// <summary>系统在电池故障引起的严重挂起之后已恢复操作。由于在没有事先通知的情况下发生了严重暂停，因此当应用程序收到此事件时，以前可用的资源和数据可能不存在。应用程序应尝试尽其所能地恢复其状态。</summary>
    ResumeCritical = 6,
    /// <summary>系统在挂起之后已恢复操作。</summary>
    ResumeSuspend = 7,
    /// <summary>计算机将要进入挂起状态。 该事件通常在所有应用程序和可安装驱动程序已对上一个 true 状态返回 QuerySuspend 后广播。</summary>
    Suspend = 4
}

/// <summary>指定终端服务会话更改通知的原因。</summary>
public enum SessionChangeReason
{
    /// <summary>控制台会话已连接</summary>
    ConsoleConnect = 1,
    /// <summary>控制台会话已断开连接</summary>
    ConsoleDisconnect,
    /// <summary>远程会话已连接</summary>
    RemoteConnect,
    /// <summary>远程会话已断开连接</summary>
    RemoteDisconnect,
    /// <summary>用户已登录到会话</summary>
    SessionLogon,
    /// <summary>用户已从会话注销</summary>
    SessionLogoff,
    /// <summary>会话已被锁定</summary>
    SessionLock,
    /// <summary>会话已被解除锁定</summary>
    SessionUnlock,
    /// <summary>会话的远程控制状态已更改</summary>
    SessionRemoteControl
}

internal class Advapi32
{
    [Flags]
    public enum ServiceType
    {
        Adapter = 0x4,
        FileSystemDriver = 0x2,
        InteractiveProcess = 0x100,
        KernelDriver = 0x1,
        RecognizerDriver = 0x8,
        Win32OwnProcess = 0x10,
        Win32ShareProcess = 0x20
    }

    [Flags]
    public enum StartType
    {
        AutoStart = 0x2,
        BootStart = 0x0,
        DemandStart = 0x3,
        Disabled = 0x4,
        SystemStart = 0x1,
    }

    internal enum ControlOptions
    {
        Stop = 1,
        Pause = 2,
        Continue = 3,
        Interrogate = 4,
        Shutdown = 5,

        PowerEvent = 13,
        SessionChange = 14,
        TimeChange = 16,
    }

    internal class ServiceOptions
    {
        internal const Int32 SERVICE_QUERY_CONFIG = 1;

        internal const Int32 SERVICE_CHANGE_CONFIG = 2;

        internal const Int32 SERVICE_QUERY_STATUS = 4;

        internal const Int32 SERVICE_ENUMERATE_DEPENDENTS = 8;

        internal const Int32 SERVICE_START = 16;

        internal const Int32 SERVICE_STOP = 32;

        internal const Int32 SERVICE_PAUSE_CONTINUE = 64;

        internal const Int32 SERVICE_INTERROGATE = 128;

        internal const Int32 SERVICE_USER_DEFINED_CONTROL = 256;

        internal const Int32 SERVICE_ALL_ACCESS = 983551;

        internal const Int32 STANDARD_RIGHTS_DELETE = 65536;

        internal const Int32 STANDARD_RIGHTS_REQUIRED = 983040;
    }

    internal class ServiceControllerOptions
    {
        internal const Int32 SC_ENUM_PROCESS_INFO = 0;

        internal const Int32 SC_MANAGER_CONNECT = 1;

        internal const Int32 SC_MANAGER_CREATE_SERVICE = 2;

        internal const Int32 SC_MANAGER_ENUMERATE_SERVICE = 4;

        internal const Int32 SC_MANAGER_LOCK = 8;

        internal const Int32 SC_MANAGER_MODIFY_BOOT_CONFIG = 32;

        internal const Int32 SC_MANAGER_QUERY_LOCK_STATUS = 16;

        internal const Int32 SC_MANAGER_ALL = 983103;
    }

    internal struct SERVICE_STATUS
    {
        public ServiceType serviceType;

        public ServiceControllerStatus currentState;

        public ControlsAccepted controlsAccepted;

        public Int32 win32ExitCode;

        public Int32 serviceSpecificExitCode;

        public Int32 checkPoint;

        public Int32 waitHint;
    }

    public enum ServiceControllerStatus : Int32
    {
        ContinuePending = 5,
        Paused = 7,
        PausePending = 6,
        Running = 4,
        StartPending = 2,
        Stopped = 1,
        StopPending = 3
    }

    [Flags]
    public enum ControlsAccepted : Int32
    {
        NetBindChange = 0x00000010,
        ParamChange = 0x00000008,
        CanPauseAndContinue = 0x00000002,
        /// <summary>系统将关闭</summary>
        PreShutdown = 0x00000100,
        CanShutdown = 0x00000004,
        CanStop = 0x00000001,

        //supported only by HandlerEx
        HardwareProfileChange = 0x00000020,
        /// <summary>电源状态更改</summary>
        CanHandlePowerEvent = 0x00000040,
        /// <summary>会话状态发生更改</summary>
        CanHandleSessionChangeEvent = 0x00000080,
        /// <summary>系统时间已更改</summary>
        TimeChange = 0x00000200,
        /// <summary>注册为已发生事件的服务触发事件</summary>
        TriggerEvent = 0x00000400,
        /// <summary>用户已开始重新启动</summary>
        UserModeReboot = 0x00000800
    }

    public delegate void ServiceMainCallback(Int32 argCount, IntPtr argPointer);

    [StructLayout(LayoutKind.Sequential)]
    internal class SERVICE_TABLE_ENTRY
    {
        public IntPtr name;

        public ServiceMainCallback callback;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SERVICE_DESCRIPTION
    {
        public IntPtr Description;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SERVICE_CONFIG
    {
        public ServiceType serviceType;

        public StartType startType;

        public Int32 errorControl;

        public IntPtr BinaryPathName;

        public IntPtr LoadOrderGroup;

        public Int32 TagId;

        public IntPtr Dependencies;

        public IntPtr ServiceStartName;

        public IntPtr DisplayName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class WTSSESSION_NOTIFICATION
    {
        public Int32 size;

        public Int32 sessionId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class SERVICE_TIMECHANGE_INFO
    {
        public Int64 NewTime;
        public Int64 OldTime;
    }

    public delegate Int32 ServiceControlCallbackEx(ControlOptions control, Int32 eventType, IntPtr eventData, IntPtr eventContext);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern Boolean CloseServiceHandle(IntPtr handle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern unsafe Boolean ControlService(SafeServiceHandle serviceHandle, ControlOptions control, SERVICE_STATUS* pStatus);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW", SetLastError = true)]
    internal static extern IntPtr OpenSCManager(String machineName, String databaseName, Int32 access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW", SetLastError = true)]
    internal static extern IntPtr OpenService(SafeServiceHandle databaseHandle, String serviceName, Int32 access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateService(SafeServiceHandle databaseHandle, String lpSvcName, String lpDisplayName,
                                                Int32 dwDesiredAccess, Int32 dwServiceType, Int32 dwStartType,
                                                Int32 dwErrorControl, String lpPathName, String lpLoadOrderGroup,
                                                Int32 lpdwTagId, String lpDependencies, String lpServiceStartName,
                                                String lpPassword);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern Int32 DeleteService(SafeServiceHandle serviceHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern unsafe Boolean QueryServiceStatus(SafeServiceHandle serviceHandle, SERVICE_STATUS* pStatus);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "StartServiceW", SetLastError = true)]
    internal static extern Boolean StartService(SafeServiceHandle serviceHandle, Int32 argNum, IntPtr argPtrs);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern unsafe Boolean SetServiceStatus(IntPtr serviceStatusHandle, SERVICE_STATUS* status);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr RegisterServiceCtrlHandlerEx(String serviceName, ServiceControlCallbackEx callback, IntPtr userData);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern Boolean StartServiceCtrlDispatcher(IntPtr entry);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryServiceConfigW", SetLastError = true)]
    public static extern unsafe Boolean QueryServiceConfig(SafeServiceHandle serviceHandle, IntPtr config, Int32 bufSize, Int32* bytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern Boolean ChangeServiceConfig2(SafeServiceHandle serviceHandle, Int32 dwInfoLevel, IntPtr pInfo);
}