using System.Runtime.InteropServices;

namespace NewLife.Agent;

internal static class NativeMethods
{
    [DllImport("psapi.dll", SetLastError = true)]
    public static extern Boolean EmptyWorkingSet(IntPtr hProcess);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

    private const UInt32 WM_CLOSE = 0x0010;
    /// <summary>关闭目标窗口（发送CLOSE消息）</summary>
    /// <param name="hWnd"></param>
    public static void CloseWindow(IntPtr hWnd) => SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
}
