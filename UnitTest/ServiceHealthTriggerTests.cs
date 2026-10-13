using System.ComponentModel;
using System.Diagnostics;
using NewLife.Agent;

namespace UnitTest;

/// <summary>ServiceBase 健康检查触发重启分支单元测试</summary>
/// <remarks>
/// 补齐现有用例缺失的"触发重启"路径：内存/线程/句柄超标、自动重启到点，以及 DoCheck 的短路顺序。
/// 用例修改全局 Setting.Current，加入 WebPanel 集合串行执行，避免全局配置并发冲突。
/// </remarks>
[Collection("WebPanel")]
public class ServiceHealthTriggerTests
{
    /// <summary>记录线程检查调用的测试服务，直接继承 ServiceBase 以便真正覆写检查方法</summary>
    /// <remarks>
    /// 不能继承 ServiceBaseTester：其 CheckThread 以 new 隐藏基类虚方法，后继类无法再 override（CS0506），
    /// 而 DoCheck 内部对检查方法为虚调用，必须真正覆写才能拦截。
    /// </remarks>
    private class OrderTester : ServiceBase
    {
        /// <summary>线程检查是否被调用</summary>
        public Boolean ThreadChecked;

        /// <summary>覆写线程检查，仅记录调用并返回 false</summary>
        /// <returns>固定返回 false，不触发重启</returns>
        protected override Boolean CheckThread()
        {
            ThreadChecked = true;
            return false;
        }

        /// <summary>触发一周期健康巡检</summary>
        public void DoCheckNow() => DoCheck(null);
    }

    /// <summary>断言宿主调用记录与期望序列完全一致（数量与顺序）</summary>
    /// <param name="host">测试宿主</param>
    /// <param name="expected">期望的调用序列</param>
    private static void AssertCalls(TestHost host, params String[] expected)
    {
        Assert.Equal(expected.Length, host.Calls.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], host.Calls[i]);
        }
    }

    /// <summary>内存超过阈值时触发重启并调用宿主 Stop/Start</summary>
    [Fact]
    [DisplayName("CheckMemory_超过阈值_触发重启并依次调用Stop与Start")]
    public void CheckMemory_OverThreshold_TriggersRestart()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        // 关闭内存整理，避免 FreeMemory 回收工作集导致检查读到的内存低于阈值
        set.FreeMemoryInterval = 0;

        var host = new TestHost { StopSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { Host = host, ServiceName = "UnitTestCheckMemory" };

        // 构造完成后取当前工作集 MB 作为阈值，缩小"取阈值-检查"间隔，确保检查时 memory >= max
        set.MaxMemory = (Int32)(Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024);

        var result = svc.CheckMemory();

        Assert.True(result);
        AssertCalls(host, "Stop", "Start");
    }

    /// <summary>线程数超过阈值时触发重启并调用宿主 Stop/Start</summary>
    [Fact]
    [DisplayName("CheckThread_超过阈值_触发重启并依次调用Stop与Start")]
    public void CheckThread_OverThreshold_TriggersRestart()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;

        var host = new TestHost { StopSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { Host = host, ServiceName = "UnitTestCheckThread" };

        // 构造完成后取当前线程数作为阈值，缩小"取阈值-检查"间隔，确保检查时 count >= max
        set.MaxThread = Process.GetCurrentProcess().Threads.Count;

        var result = svc.CheckThread();

        Assert.True(result);
        AssertCalls(host, "Stop", "Start");
    }

    /// <summary>句柄数超过阈值时触发重启并调用宿主 Stop/Start</summary>
    [Fact]
    [DisplayName("CheckHandle_超过阈值_触发重启并依次调用Stop与Start")]
    public void CheckHandle_OverThreshold_TriggersRestart()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;

        var host = new TestHost { StopSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { Host = host, ServiceName = "UnitTestCheckHandle" };

        // 构造完成后取当前句柄数作为阈值，缩小"取阈值-检查"间隔，确保检查时 count >= max
        set.MaxHandle = Process.GetCurrentProcess().HandleCount;

        var result = svc.CheckHandle();

        Assert.True(result);
        AssertCalls(host, "Stop", "Start");
    }

    /// <summary>内存未超标且距上次整理不足间隔时不再重复整理并更新时间</summary>
    [Fact]
    [DisplayName("CheckMemory_整理间隔内_不重复整理内存")]
    public void CheckMemory_FreeMemoryInterval_NotRepeatedWithinInterval()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.MaxMemory = 0;
        set.FreeMemoryInterval = 3600;

        var svc = new ServiceBaseTester();
        Assert.Equal(DateTime.MinValue, svc.GetNextCollect());

        // 首次调用进入整理分支并安排下一次整理时间
        var first = svc.CheckMemory();
        Assert.False(first);

        var next = svc.GetNextCollect();
        Assert.True(next > DateTime.Now);

        // 间隔未到，不重复整理，下次整理时间保持不变
        var second = svc.CheckMemory();
        Assert.False(second);
        Assert.Equal(next, svc.GetNextCollect());
    }

    /// <summary>运行时长达到自动重启分钟数时触发重启并停止主循环</summary>
    [Fact]
    [DisplayName("CheckAutoRestart_到达重启时间_触发重启并停止循环")]
    public void CheckAutoRestart_Reached_TriggersRestartAndStopsLoop()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.AutoRestart = 1;
        set.RestartTimeRange = "";

        var host = new TestHost { StopSuccess = true, StartSuccess = true };
        var svc = new ServiceBaseTester { Host = host, ServiceName = "UnitTestAutoRestart" };
        svc.Running = true;
        svc.BackdateStart(TimeSpan.FromMinutes(2));

        var result = svc.CheckAutoRestart();

        Assert.True(result);
        AssertCalls(host, "Stop", "Start");
        // DefaultHost 且非服务模式时同步停止主循环
        Assert.False(svc.Running);
    }

    /// <summary>内存超标触发重启后 DoCheck 短路，不再执行线程检查</summary>
    [Fact]
    [DisplayName("DoCheck_内存超标_短路线程检查")]
    public void DoCheck_MemoryOver_ShortCircuitsThreadCheck()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        // 关闭内存整理，避免 FreeMemory 回收工作集导致检查读到的内存低于阈值
        set.FreeMemoryInterval = 0;

        var host = new TestHost { StopSuccess = true, StartSuccess = true };
        var svc = new OrderTester { Host = host, ServiceName = "UnitTestDoCheckShortCircuit" };

        // 构造完成后取当前工作集 MB 作为阈值，缩小"取阈值-检查"间隔，确保检查时 memory >= max
        set.MaxMemory = (Int32)(Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024);

        svc.DoCheckNow();

        // 内存检查触发重启并返回，线程检查不应被调用
        Assert.False(svc.ThreadChecked);
        AssertCalls(host, "Stop", "Start");
    }

    /// <summary>内存未超标时 DoCheck 继续执行线程检查</summary>
    [Fact]
    [DisplayName("DoCheck_内存未超标_继续执行线程检查")]
    public void DoCheck_MemoryNotOver_ReachesThreadCheck()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        // 关闭内存、句柄与自动重启检查，隔离出"内存通过后进入线程检查"的路径
        set.MaxMemory = 0;
        set.FreeMemoryInterval = 0;
        set.MaxHandle = 0;
        set.AutoRestart = 0;
        set.WatchDog = "";

        var host = new TestHost { StopSuccess = true, StartSuccess = true };
        var svc = new OrderTester { Host = host, ServiceName = "UnitTestDoCheckOrder" };

        svc.DoCheckNow();

        // 内存检查未触发重启时 DoCheck 继续执行线程检查
        Assert.True(svc.ThreadChecked);
    }
}
