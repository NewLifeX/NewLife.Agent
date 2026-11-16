using System.ComponentModel;
using System.Diagnostics;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Models;
using Setting = NewLife.Agent.Setting;

namespace UnitTest;

/// <summary>服务启动/停止生命周期单元测试</summary>
/// <remarks>覆盖 StartWork 单次执行与启动异常处理、启动后命令执行；会修改全局 Setting，串行执行</remarks>
[Collection("WebPanel")]
public class ServiceLifecycleTests
{
    /// <summary>统计 StartWork 调用次数、可模拟启动失败的测试服务</summary>
    private class StartWorkTester : ServiceBaseTester
    {
        public Int32 StartCount;
        public Boolean ThrowOnStartWork;
        public Boolean ThrowOnStopWork;

        public override void StartWork(String reason)
        {
            Interlocked.Increment(ref StartCount);
            if (ThrowOnStartWork) throw new InvalidOperationException("模拟业务启动失败");
        }

        public override void StopWork(String reason)
        {
            if (ThrowOnStopWork) throw new InvalidOperationException("模拟业务停止失败");
        }

        public void StartLoopNow() => StartLoop();
        public void StopLoopNow() => StopLoop();
        public void DoLoopNow() => DoLoop();
        public Boolean RestartNow(String message) => RequestRestart(message);
    }

    /// <summary>只统计重启次数的测试主机，避免触发真实的服务控制</summary>
    private class CountingHost : IHost
    {
        public Int32 RestartCount;
        public Boolean Result = true;

        public String Name => "CountingHost";

        public Boolean IsInstalled(String serviceName) => true;
        public Boolean IsRunning(String serviceName) => true;
        public Boolean Install(ServiceModel service) => true;
        public Boolean Remove(String serviceName) => true;
        public Boolean Start(String serviceName) => true;
        public Boolean Stop(String serviceName) => true;
        public Boolean Restart(String serviceName)
        {
            Interlocked.Increment(ref RestartCount);
            return Result;
        }
        public void Run(ServiceBase service) { }
        public ServiceConfig QueryConfig(String serviceName) => null;
    }

    private static Boolean WaitUntil(Func<Boolean> predicate, Int32 timeout = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeout)
        {
            if (predicate()) return true;
            Thread.Sleep(20);
        }

        return predicate();
    }

    private static String ReadMarker(String path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch (IOException)
        {
            return "";
        }
    }

    [Fact]
    [DisplayName("StartLoop_后台执行StartWork_仅执行一次")]
    public void StartLoop_RunsStartWorkOnce()
    {
        var svc = new StartWorkTester { ServiceName = "UnitTestStartOnce" };

        svc.StartLoopNow();

        Assert.True(WaitUntil(() => svc.StartCount > 0), "StartWork 未被执行");
        Thread.Sleep(200);
        Assert.Equal(1, svc.StartCount);
    }

    [Fact]
    [DisplayName("StartWork_抛异常_不外泄且不重试")]
    public void StartWork_Throws_NotPropagated()
    {
        var svc = new StartWorkTester { ServiceName = "UnitTestStartFail", ThrowOnStartWork = true };

        var ex = Record.Exception(() => svc.StartLoopNow());
        Assert.Null(ex);

        Assert.True(WaitUntil(() => svc.StartCount > 0), "StartWork 未被执行");
        Thread.Sleep(200);
        Assert.Equal(1, svc.StartCount);
    }

    [Fact]
    [DisplayName("StopLoop_未运行时_安全返回")]
    public void StopLoop_NotRunning_NoThrow()
    {
        var svc = new StartWorkTester { ServiceName = "UnitTestStopIdle" };

        var ex = Record.Exception(() => svc.StopLoopNow());
        Assert.Null(ex);
        Assert.False(svc.Running);
    }

    [WindowsFact]
    [DisplayName("AfterStart_引号包裹路径_成功执行外部命令")]
    public async Task AfterStart_QuotedPath_RunsCommand()
    {
        // 依赖 cmd.exe 构造外部命令，仅 Windows 可运行（非 Windows 显式跳过）
        var set = Setting.Current;
        var prevAfter = set.AfterStart;
        var prevInterval = set.WatchInterval;
        var prevWatchDog = set.WatchDog;
        var prevFree = set.FreeMemoryInterval;
        var marker = Path.Combine(Path.GetTempPath(), $"agent-afterstart-{Guid.NewGuid():N}.txt");
        try
        {
            var comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            set.AfterStart = $"\"{comspec}\" /c echo ok> \"{marker}\"";
            set.WatchInterval = 1;
            set.WatchDog = "";
            set.FreeMemoryInterval = 0;

            var svc = new StartWorkTester { ServiceName = "UnitTestAfterStart" };
            svc.Host = new DefaultHost { Service = svc };

            var task = Task.Run(() => svc.Host.Run(svc));
            // 只等文件出现会在 cmd 重定向创建空文件后立刻被 StopLoop Kill，产生空内容竞态；必须等内容写入
            var executed = WaitUntil(() => File.Exists(marker) && ReadMarker(marker) == "ok", 10_000);

            svc.StopLoopNow();
            try { await task.WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { }

            Assert.True(executed, "启动后命令未执行");
            Assert.Equal("ok", File.ReadAllText(marker).Trim());
        }
        finally
        {
            set.AfterStart = prevAfter;
            set.WatchInterval = prevInterval;
            set.WatchDog = prevWatchDog;
            set.FreeMemoryInterval = prevFree;

            try { if (File.Exists(marker)) File.Delete(marker); } catch { }
        }
    }

    [Fact]
    [DisplayName("StopLoop_早于巡检循环_不进入巡检且Running为false")]
    public void StopLoop_BeforeDoLoop_DoesNotEnterLoop()
    {
        // 服务在启动挂起阶段被停止：DoLoop 尚未执行，StopLoop 必须留下停止标记供随后启动的循环识别
        var svc = new StartWorkTester { ServiceName = "UnitTestStopBeforeLoop" };

        svc.StopLoopNow();

        var ex = Record.Exception(() => svc.DoLoopNow());
        Assert.Null(ex);
        Assert.False(svc.Running);
    }

    [Fact]
    [DisplayName("StopLoop_StopWork抛异常_状态仍复位且不外泄")]
    public void StopLoop_StopWorkThrows_StateReset()
    {
        var svc = new StartWorkTester { ServiceName = "UnitTestStopWorkThrows", ThrowOnStopWork = true };

        svc.StartLoopNow();
        Assert.True(WaitUntil(() => svc.StartCount > 0), "StartWork 未被执行");

        var ex = Record.Exception(() => svc.StopLoopNow());
        Assert.Null(ex);
        Assert.False(svc.Running);
    }

    [Fact]
    [DisplayName("RequestRestart_冷却期内_只重启一次")]
    public void RequestRestart_Cooldown_OnlyOnce()
    {
        var host = new CountingHost();
        var svc = new StartWorkTester { ServiceName = "UnitTestRestartCooldown", Host = host };

        Assert.True(svc.RestartNow("首次触发"));
        Assert.False(svc.RestartNow("冷却期内再次触发"));
        Assert.Equal(1, host.RestartCount);
    }

    [Fact]
    [DisplayName("RequestRestart_重启失败_不进入冷却下一轮继续尝试")]
    public void RequestRestart_Failure_DoesNotStartCooldown()
    {
        var host = new CountingHost { Result = false };
        var svc = new StartWorkTester { ServiceName = "UnitTestRestartFailure", Host = host };

        Assert.False(svc.RestartNow("首次失败"));
        Assert.False(svc.RestartNow("立即重试仍失败"));
        Assert.Equal(2, host.RestartCount);
    }

    [Fact]
    [DisplayName("CheckAutoRestart_定时重启后_冷却基准随之归零不屏蔽其它重启")]
    public void CheckAutoRestart_ResetsCooldown()
    {
        using var scope = new SettingScope();

        var host = new CountingHost();
        var svc = new StartWorkTester { ServiceName = "UnitTestAutoRestartCooldown", Host = host };

        var set = Setting.Current;
        set.AutoRestart = 1;
        set.RestartTimeRange = null;
        set.MaxMemory = 0;
        set.FreeMemoryInterval = 0;

        // 回拨运行时长到定时重启阈值
        svc.BackdateStart(TimeSpan.FromMinutes(2));
        Assert.True(svc.CheckAutoRestart(), "达到自动重启间隔却未触发");
        Assert.Equal(1, host.RestartCount);

        // 重启后运行计时归零，冷却基准必须同时归零；否则内存超限触发的重启会被长时间屏蔽
        set.MaxMemory = 1;
        Assert.True(svc.CheckMemory(), "定时重启后内存超限未触发重启，冷却基准未归零");
        Assert.Equal(2, host.RestartCount);
    }
}
