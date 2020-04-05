using System;
using System.Runtime.InteropServices;

namespace NewLife.Agent
{
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
        public class SERVICE_TABLE_ENTRY
        {
            public IntPtr name;

            public ServiceMainCallback callback;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SERVICE_DESCRIPTION
        {
            public IntPtr Description;
        }

        public enum PowerBroadcastStatus
        {
            BatteryLow = 9,
            OemEvent = 11,
            PowerStatusChange = 10,
            QuerySuspend = 0,
            QuerySuspendFailed = 2,
            ResumeAutomatic = 18,
            ResumeCritical = 6,
            ResumeSuspend = 7,
            Suspend = 4
        }

        public enum SessionChangeReason
        {
            ConsoleConnect = 1,
            ConsoleDisconnect,
            RemoteConnect,
            RemoteDisconnect,
            SessionLogon,
            SessionLogoff,
            SessionLock,
            SessionUnlock,
            SessionRemoteControl
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

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean ChangeServiceConfig2(SafeServiceHandle serviceHandle, Int32 dwInfoLevel, IntPtr pInfo);
    }
}