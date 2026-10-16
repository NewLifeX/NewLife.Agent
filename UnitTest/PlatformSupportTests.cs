using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NewLife.Agent;
using NewLife.Agent.Windows;

namespace UnitTest;

/// <summary>Windows 平台支持类可测面单元测试</summary>
/// <remarks>
/// MarshalHelper 与 Desktop 路径解析为与桌面会话无关的跨平台纯逻辑；
/// PowerStatus/WindowsService 依赖 Windows 系统 API，非 Windows 平台显式跳过。
/// 真实桌面会话（StartProcess 全链路）与 SCM 安装启停属集成范畴，见功能清单收口记录。
/// </remarks>
public class PlatformSupportTests
{
    #region MarshalHelper
    [StructLayout(LayoutKind.Sequential)]
    private struct SampleStruct
    {
        public Int32 Value;
        public Int64 Big;
    }

    [Fact]
    [DisplayName("MarshalHelper_SizeOf_与运行时封送大小一致")]
    public void MarshalHelper_SizeOf_MatchesRuntime()
    {
        Assert.Equal(Marshal.SizeOf<SampleStruct>(), MarshalHelper.SizeOf<SampleStruct>());
        Assert.True(MarshalHelper.SizeOf<SampleStruct>() > 0);
    }

    [Fact]
    [DisplayName("MarshalHelper_PtrToStructure_与非托管内存往返一致")]
    public void MarshalHelper_PtrToStructure_RoundTrip()
    {
        var size = MarshalHelper.SizeOf<SampleStruct>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            var expected = new SampleStruct { Value = 42, Big = 1234567890123L };
            Marshal.StructureToPtr(expected, ptr, false);

            var actual = MarshalHelper.PtrToStructure<SampleStruct>(ptr);

            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Big, actual.Big);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
    #endregion

    #region Desktop 路径解析
    [Fact]
    [SupportedOSPlatform("windows")]
    [DisplayName("Desktop_解析文件路径_绝对路径原样返回")]
    public void Desktop_ResolveFilePath_AbsolutePath()
    {
        var file = Path.Combine(Path.GetTempPath(), "abc.exe");
        Assert.Equal(file, Desktop.ResolveFilePath(file, null));
        Assert.Equal(file, Desktop.ResolveFilePath(file, "/some/dir"));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    [DisplayName("Desktop_解析文件路径_简单命令名在工作目录命中时返回全路径")]
    public void Desktop_ResolveFilePath_SimpleName_FoundInWorkDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ut-desktop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "tool.exe");
            File.WriteAllText(file, "");

            var actual = Desktop.ResolveFilePath("tool.exe", dir);

            Assert.Equal(Path.GetFullPath(file), actual);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    [DisplayName("Desktop_解析文件路径_简单命令名未命中时原样返回交由PATH查找")]
    public void Desktop_ResolveFilePath_SimpleName_NotFound()
    {
        Assert.Equal("definitely-not-exists.exe", Desktop.ResolveFilePath("definitely-not-exists.exe", null));
        Assert.Equal("definitely-not-exists.exe", Desktop.ResolveFilePath("definitely-not-exists.exe", Path.GetTempPath()));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    [DisplayName("Desktop_解析文件路径_相对路径与工作目录合并为绝对路径")]
    public void Desktop_ResolveFilePath_RelativePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ut-desktop-rel");
        var withDir = Desktop.ResolveFilePath(Path.Combine("sub", "app.exe"), dir);
        Assert.Equal(Path.GetFullPath(Path.Combine(dir, "sub", "app.exe")), withDir);

        var noDir = Desktop.ResolveFilePath(Path.Combine("sub", "app.exe"), null);
        Assert.True(Path.IsPathRooted(noDir));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    [DisplayName("Desktop_解析工作目录_优先显式目录否则取文件目录")]
    public void Desktop_ResolveWorkDirectory_PrefersExplicit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ut-desktop-work");

        Assert.Equal(dir, Desktop.ResolveWorkDirectory("/x/app.exe", dir));

        var file = Path.Combine(Path.GetTempPath(), "app.exe");
        Assert.Equal(Path.GetDirectoryName(file), Desktop.ResolveWorkDirectory(file, null));

        // 非绝对路径时回退当前目录
        Assert.Equal(Environment.CurrentDirectory, Desktop.ResolveWorkDirectory("app.exe", null));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    [DisplayName("Desktop_启动进程_文件名空白抛参数异常")]
    public void Desktop_StartProcess_BlankFileName_Throws()
    {
        var desktop = new Desktop();

        Assert.Throws<ArgumentNullException>(() => desktop.StartProcess(null));
        Assert.Throws<ArgumentNullException>(() => desktop.StartProcess("  "));
    }
    #endregion

    #region Windows 系统 API 冒烟
    [WindowsFact]
    [SupportedOSPlatform("windows")]
    [DisplayName("PowerStatus_读取电源状态_属性合法不抛异常")]
    public void PowerStatus_Read_PropertiesAreValid()
    {
        var ps = new PowerStatus();

        // 触发真实 GetSystemPowerStatus 调用；CI 虚拟机无电池时也应返回合法值
        var _ = ps.PowerLineStatus;
        var percent = ps.BatteryLifePercent;

        Assert.True(percent >= 0f && percent <= 1f);
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    [DisplayName("WindowsService_未运行的服务_返回false且查询配置为空")]
    public void WindowsService_NotRunning_ReturnsFalse()
    {
        var host = new WindowsService();

        Assert.False(host.IsRunning("NewLifeAgentUnitTestNotExist"));
        Assert.Null(host.QueryConfig("NewLifeAgentUnitTestNotExist"));
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    [DisplayName("WindowsService_管理员检测_正常返回不抛异常")]
    public void WindowsService_IsAdministrator_NoThrow()
    {
        // 仅验证 SCM 令牌查询链路可用；结果取决于运行环境是否提权
        var admin = WindowsService.IsAdministrator();

        Assert.True(admin || !admin);
    }

    [WindowsFact]
    [SupportedOSPlatform("windows")]
    [DisplayName("WindowsAutorun_未运行的服务_返回false")]
    public void WindowsAutorun_NotRunning_ReturnsFalse()
    {
        var host = new WindowsAutorun();

        Assert.False(host.IsRunning("NewLifeAgentUnitTestNotExist"));
        Assert.False(host.IsInstalled("NewLifeAgentUnitTestNotExist"));
    }
    #endregion
}
