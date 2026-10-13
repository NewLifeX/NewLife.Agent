using System.Reflection;
using NewLife.Agent;

namespace UnitTest;

/// <summary>ServiceBase 测试子类，暴露 protected 方法与私有状态供测试</summary>
/// <remarks>共享测试替身：健康检查触发分支、命令处理等测试均复用该类</remarks>
public class ServiceBaseTester : ServiceBase
{
    /// <summary>暴露基类内存检查</summary>
    /// <returns>是否超标重启</returns>
    public new Boolean CheckMemory() => base.CheckMemory();

    /// <summary>暴露基类线程检查</summary>
    /// <returns>是否超标重启</returns>
    public new Boolean CheckThread() => base.CheckThread();

    /// <summary>暴露基类句柄检查</summary>
    /// <returns>是否超标重启</returns>
    public new Boolean CheckHandle() => base.CheckHandle();

    /// <summary>暴露基类自动重启检查</summary>
    /// <returns>是否触发重启</returns>
    public new Boolean CheckAutoRestart() => base.CheckAutoRestart();

    /// <summary>触发一周期健康巡检</summary>
    public void DoCheck() => base.DoCheck(null);

    /// <summary>回拨服务启动时间（私有字段 Start），用于构造自动重启到期场景</summary>
    /// <param name="ago">相对当前时间回拨的时长</param>
    public void BackdateStart(TimeSpan ago)
    {
        var field = typeof(ServiceBase).GetField("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(this, DateTime.Now - ago);
    }

    /// <summary>读取内存整理的下次触发时间（私有字段 _nextCollect）</summary>
    /// <returns>下次触发时间</returns>
    public DateTime GetNextCollect()
    {
        var field = typeof(ServiceBase).GetField("_nextCollect", BindingFlags.Instance | BindingFlags.NonPublic);
        return (DateTime)field.GetValue(this);
    }
}
