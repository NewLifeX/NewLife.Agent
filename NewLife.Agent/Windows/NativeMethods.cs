using System.Runtime.InteropServices;

namespace NewLife.Agent.Windows;

internal static class NativeMethods
{
    /// <summary>清空指定进程的工作集，释放物理内存（PSAPI）</summary>
    /// <param name="hProcess">进程句柄</param>
    /// <returns>是否成功</returns>
    [DllImport("psapi.dll", SetLastError = true)]
    public static extern Boolean EmptyWorkingSet(IntPtr hProcess);

    /// <summary>向指定窗口发送消息（User32）</summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="Msg">消息 ID</param>
    /// <param name="wParam">附加参数 w</param>
    /// <param name="lParam">附加参数 l</param>
    /// <returns>消息处理结果</returns>
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

    private const UInt32 WM_CLOSE = 0x0010;
    /// <summary>关闭目标窗口（发送CLOSE消息）</summary>
    /// <param name="hWnd"></param>
    public static void CloseWindow(IntPtr hWnd) => SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
}
