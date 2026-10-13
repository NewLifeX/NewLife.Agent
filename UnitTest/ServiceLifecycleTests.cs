using System.ComponentModel;
using System.Diagnostics;
using NewLife;
using NewLife.Agent;
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

        public override void StartWork(String reason)
        {
            Interlocked.Increment(ref StartCount);
            if (ThrowOnStartWork) throw new InvalidOperationException("模拟业务启动失败");
        }

        public void StartLoopNow() => StartLoop();
        public void StopLoopNow() => StopLoop();
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
            var executed = WaitUntil(() => File.Exists(marker), 10_000);

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
}
