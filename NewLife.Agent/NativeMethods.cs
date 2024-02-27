using System.Runtime.InteropServices;

namespace NewLife.Agent;

internal static class NativeMethods
{
    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern Boolean EmptyWorkingSet(IntPtr hProcess);
}
