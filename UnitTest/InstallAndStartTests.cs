using System.ComponentModel;
using NewLife;
using NewLife.Agent.Command;
using NewLife.Agent.CommandHandler;
using NewLife.Agent.Models;

namespace UnitTest;

/// <summary>安装并启动服务命令处理器单元测试</summary>
/// <remarks>
/// 覆盖 InstallAndStart 的「安装 → 等待服务出现 → 启动」链路、安装失败不阻断启动，以及构造属性。
/// Process 内含两次 Thread.Sleep(500)，单个用例耗时约 1 秒，串行集合 WebPanel 内执行。
/// </remarks>
[Collection("WebPanel")]
public class InstallAndStartTests
{
    /// <summary>首个 IsInstalled 返回 false 的测试宿主，用于覆盖安装后的等待轮询</summary>
    /// <remarks>InstallAndStart 安装后最多轮询 50 次（每次 100 毫秒），直至 IsInstalled 返回 true</remarks>
    private sealed class WaitFirstHost : TestHost
    {
        private Int32 _count;

        /// <inheritdoc/>
        public override Boolean IsInstalled(String serviceName)
        {
            _count++;
            if (_count > 1) return true;

            return base.IsInstalled(serviceName);
        }
    }

    /// <summary>安装抛异常的测试宿主，用于验证安装失败不阻断服务启动</summary>
    private sealed class InstallThrowHost : TestHost
    {
        /// <inheritdoc/>
        public override Boolean Install(ServiceModel service)
        {
            base.Install(service);
            throw new InvalidOperationException("模拟安装失败");
        }
    }

    /// <summary>验证 Process 先调用 Install 再调用 Start，且服务模型传递服务名</summary>
    [Fact]
    [DisplayName("InstallAndStart_Process_先安装后启动")]
    public void Process_InstallsThenStarts()
    {
        // Process 内 Install 与 InstallAndStart 各休眠 500 毫秒，本用例耗时约 1 秒
        var host = new TestHost { Installed = true, InstallSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { ServiceName = "TestSvc", Host = host };

        var handler = new InstallAndStart(svc);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);

        var installIndex = host.Calls.IndexOf(nameof(TestHost.Install));
        var startIndex = host.Calls.IndexOf(nameof(TestHost.Start));
        Assert.True(installIndex >= 0, "未调用 Host.Install");
        Assert.True(startIndex >= 0, "未调用 Host.Start");
        Assert.True(installIndex < startIndex, "Install 应先于 Start");
        Assert.NotNull(host.LastInstallModel);
        Assert.Equal("TestSvc", host.LastInstallModel.ServiceName);
    }

    /// <summary>验证首轮 IsInstalled 为 false 时等待循环会重试，并在服务出现后调用 Start</summary>
    [Fact]
    [DisplayName("InstallAndStart_Process_未安装时等待循环后仍启动")]
    public void Process_NotInstalledAtFirst_StillStarts()
    {
        // 宿主首个 IsInstalled 返回 false，等待循环休眠 100 毫秒后再次探测成功
        var host = new WaitFirstHost { Installed = false, InstallSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { ServiceName = "TestSvc", Host = host };

        var handler = new InstallAndStart(svc);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.Contains(nameof(TestHost.IsInstalled), host.Calls);
        Assert.Contains(nameof(TestHost.Start), host.Calls);
        Assert.True(host.Calls.IndexOf(nameof(TestHost.IsInstalled)) < host.Calls.IndexOf(nameof(TestHost.Start)), "等待服务出现应先于 Start");
    }

    /// <summary>验证安装抛异常被吞掉后仍继续启动服务，且 Process 不外抛</summary>
    [Fact]
    [DisplayName("InstallAndStart_Process_安装抛异常_不阻断启动")]
    public void Process_InstallThrows_StillStarts()
    {
        var host = new InstallThrowHost { Installed = true, InstallSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { ServiceName = "TestSvc", Host = host };

        var handler = new InstallAndStart(svc);
        var ex = Record.Exception(() => handler.Process([]));

        Assert.Null(ex);
        Assert.Contains(nameof(TestHost.Install), host.Calls);
        Assert.Contains(nameof(TestHost.Start), host.Calls);
        Assert.True(host.Calls.IndexOf(nameof(TestHost.Install)) < host.Calls.IndexOf(nameof(TestHost.Start)), "Install 应先于 Start");
    }

    /// <summary>验证构造时设置命令、描述，且未占用快捷键</summary>
    [Fact]
    [DisplayName("InstallAndStart_构造_命令与描述正确")]
    public void Constructor_SetsCommandAndDescription()
    {
        var svc = new ServiceBaseTester { ServiceName = "TestSvc" };

        var handler = new InstallAndStart(svc);

        Assert.Equal(CommandConst.InstallAndStart, handler.Cmd);
        Assert.Equal("-install", handler.Cmd);
        Assert.Equal("Install and start service", handler.Description);
        Assert.Null(handler.ShortcutKey);
    }
}
