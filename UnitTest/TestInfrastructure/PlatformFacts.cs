namespace UnitTest;

/// <summary>仅 Windows 平台运行的测试。非 Windows 平台显式跳过（而非静默通过）</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <summary>实例化</summary>
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "仅 Windows 平台运行";
    }
}

/// <summary>仅 Linux 平台运行的测试。非 Linux 平台显式跳过（而非静默通过）</summary>
public sealed class LinuxFactAttribute : FactAttribute
{
    /// <summary>实例化</summary>
    public LinuxFactAttribute()
    {
        if (!OperatingSystem.IsLinux()) Skip = "仅 Linux 平台运行";
    }
}

/// <summary>仅 macOS 平台运行的测试。非 macOS 平台显式跳过（而非静默通过）</summary>
public sealed class OSXFactAttribute : FactAttribute
{
    /// <summary>实例化</summary>
    public OSXFactAttribute()
    {
        if (!OperatingSystem.IsMacOS()) Skip = "仅 macOS 平台运行";
    }
}
