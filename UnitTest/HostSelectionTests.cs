using System.ComponentModel;
using NewLife;
using NewLife.Agent;
using Setting = NewLife.Agent.Setting;

namespace UnitTest;

/// <summary>平台自动检测与宿主选择单元测试</summary>
/// <remarks>验证 ServiceBase.Init() 依据当前平台与 UseAutorun 自动选择宿主；会写入 Setting，串行执行避免全局状态冲突</remarks>
[Collection("WebPanel")]
public class HostSelectionTests
{
    /// <summary>暴露 protected 初始化方法的测试子类</summary>
    private class HostInitTester : ServiceBase
    {
        public void InitHost() => Init();
    }

    private static void RunInit(Boolean useAutorun, Action<ServiceBase, Setting> assert)
    {
        var set = Setting.Current;
        var prevName = set.ServiceName;
        var prevDisplay = set.DisplayName;
        var prevDesc = set.Description;
        var prevAutorun = set.UseAutorun;
        try
        {
            var svc = new HostInitTester
            {
                ServiceName = $"UnitTestHost{Guid.NewGuid():N}",
                DisplayName = "单元测试服务",
                Description = "平台宿主自动检测",
                UseAutorun = useAutorun,
            };
            svc.InitHost();

            assert(svc, set);
        }
        finally
        {
            set.ServiceName = prevName;
            set.DisplayName = prevDisplay;
            set.Description = prevDesc;
            set.UseAutorun = prevAutorun;
            set.Save();
        }
    }

    [Fact]
    [DisplayName("Init_默认配置_自动选择宿主且不为空")]
    public void Init_AutoSelectsHost()
    {
        RunInit(false, (svc, _) =>
        {
            Assert.NotNull(svc.Host);
            Assert.False(svc.Host.Name.IsNullOrEmpty());

            if (Runtime.Windows) Assert.IsType<WindowsService>(svc.Host);
            else Assert.IsNotType<WindowsService>(svc.Host);
        });
    }

    [Fact]
    [DisplayName("Init_UseAutorun_Windows选择登录自启动宿主")]
    public void Init_UseAutorun_SelectsAutorunHost()
    {
        RunInit(true, (svc, _) =>
        {
            Assert.NotNull(svc.Host);

            if (Runtime.Windows) Assert.IsType<WindowsAutorun>(svc.Host);
        });
    }

    [Fact]
    [DisplayName("Init_已指定宿主_不被覆盖")]
    public void Init_ExistingHost_NotOverwritten()
    {
        var set = Setting.Current;
        var prevName = set.ServiceName;
        var prevDisplay = set.DisplayName;
        var prevDesc = set.Description;
        try
        {
            var svc = new HostInitTester { ServiceName = "UnitTestHostKeep", DisplayName = "单元测试服务", Description = "指定宿主" };
            var host = new DefaultHost { Service = svc };
            svc.Host = host;

            svc.InitHost();

            Assert.Same(host, svc.Host);
        }
        finally
        {
            set.ServiceName = prevName;
            set.DisplayName = prevDisplay;
            set.Description = prevDesc;
            set.Save();
        }
    }
}
