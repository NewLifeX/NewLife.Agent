using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>HealthMonitor 健康检查纯逻辑单元测试</summary>
/// <remarks>验证阈值判断与自动重启时间判断，使用 new Setting() 隔离全局配置</remarks>
public class HealthMonitorTests
{
    #region IsMemoryOver
    [Fact]
    [DisplayName("IsMemoryOver_未配置阈值_不超标")]
    public void IsMemoryOver_NoLimit_NotOver()
    {
        var set = new Setting { MaxMemory = 0 };
        Assert.False(HealthMonitor.IsMemoryOver(set, 1024));
    }

    [Fact]
    [DisplayName("IsMemoryOver_低于阈值_不超标")]
    public void IsMemoryOver_BelowLimit_NotOver()
    {
        var set = new Setting { MaxMemory = 100 };
        Assert.False(HealthMonitor.IsMemoryOver(set, 99));
    }

    [Fact]
    [DisplayName("IsMemoryOver_等于阈值_超标")]
    public void IsMemoryOver_EqualLimit_Over()
    {
        var set = new Setting { MaxMemory = 100 };
        Assert.True(HealthMonitor.IsMemoryOver(set, 100));
    }

    [Fact]
    [DisplayName("IsMemoryOver_超过阈值_超标")]
    public void IsMemoryOver_AboveLimit_Over()
    {
        var set = new Setting { MaxMemory = 100 };
        Assert.True(HealthMonitor.IsMemoryOver(set, 101));
    }

    [Fact]
    [DisplayName("IsMemoryOver_空配置_不超标")]
    public void IsMemoryOver_NullSet_NotOver()
    {
        Assert.False(HealthMonitor.IsMemoryOver(null, 1024));
    }
    #endregion

    #region IsThreadOver
    [Fact]
    [DisplayName("IsThreadOver_未配置阈值_不超标")]
    public void IsThreadOver_NoLimit_NotOver()
    {
        var set = new Setting { MaxThread = 0 };
        Assert.False(HealthMonitor.IsThreadOver(set, 100));
    }

    [Fact]
    [DisplayName("IsThreadOver_低于阈值_不超标")]
    public void IsThreadOver_BelowLimit_NotOver()
    {
        var set = new Setting { MaxThread = 100 };
        Assert.False(HealthMonitor.IsThreadOver(set, 99));
    }

    [Fact]
    [DisplayName("IsThreadOver_等于阈值_超标")]
    public void IsThreadOver_EqualLimit_Over()
    {
        var set = new Setting { MaxThread = 100 };
        Assert.True(HealthMonitor.IsThreadOver(set, 100));
    }

    [Fact]
    [DisplayName("IsThreadOver_超过阈值_超标")]
    public void IsThreadOver_AboveLimit_Over()
    {
        var set = new Setting { MaxThread = 100 };
        Assert.True(HealthMonitor.IsThreadOver(set, 101));
    }
    #endregion

    #region IsHandleOver
    [Fact]
    [DisplayName("IsHandleOver_未配置阈值_不超标")]
    public void IsHandleOver_NoLimit_NotOver()
    {
        var set = new Setting { MaxHandle = 0 };
        Assert.False(HealthMonitor.IsHandleOver(set, 100));
    }

    [Fact]
    [DisplayName("IsHandleOver_低于阈值_不超标")]
    public void IsHandleOver_BelowLimit_NotOver()
    {
        var set = new Setting { MaxHandle = 100 };
        Assert.False(HealthMonitor.IsHandleOver(set, 99));
    }

    [Fact]
    [DisplayName("IsHandleOver_超过阈值_超标")]
    public void IsHandleOver_AboveLimit_Over()
    {
        var set = new Setting { MaxHandle = 100 };
        Assert.True(HealthMonitor.IsHandleOver(set, 101));
    }
    #endregion

    #region IsAutoRestartDue
    [Fact]
    [DisplayName("IsAutoRestartDue_未配置_不触发")]
    public void IsAutoRestartDue_Disabled_NotDue()
    {
        var set = new Setting { AutoRestart = 0 };
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(HealthMonitor.IsAutoRestartDue(set, now.AddMinutes(-120), now, out _));
    }

    [Fact]
    [DisplayName("IsAutoRestartDue_未达分钟数_不触发")]
    public void IsAutoRestartDue_NotReached_NotDue()
    {
        var set = new Setting { AutoRestart = 60 };
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(HealthMonitor.IsAutoRestartDue(set, now.AddMinutes(-10), now, out _));
    }

    [Fact]
    [DisplayName("IsAutoRestartDue_达到分钟数无时间范围_触发")]
    public void IsAutoRestartDue_ReachedNoRange_Due()
    {
        var set = new Setting { AutoRestart = 60, RestartTimeRange = null };
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.True(HealthMonitor.IsAutoRestartDue(set, now.AddMinutes(-120), now, out var inRange));
        Assert.False(inRange);
    }

    [Fact]
    [DisplayName("IsAutoRestartDue_达到分钟数在范围内_触发并标记范围内")]
    public void IsAutoRestartDue_ReachedInRange_Due()
    {
        var set = new Setting { AutoRestart = 60, RestartTimeRange = "08:00-18:00" };
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.True(HealthMonitor.IsAutoRestartDue(set, now.AddMinutes(-120), now, out var inRange));
        Assert.True(inRange);
    }

    [Fact]
    [DisplayName("IsAutoRestartDue_达到分钟数不在范围内_不触发")]
    public void IsAutoRestartDue_ReachedOutOfRange_NotDue()
    {
        var set = new Setting { AutoRestart = 60, RestartTimeRange = "00:00-06:00" };
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(HealthMonitor.IsAutoRestartDue(set, now.AddMinutes(-120), now, out _));
    }

    [Fact]
    [DisplayName("IsAutoRestartDue_时间范围非法格式_宽松触发")]
    public void IsAutoRestartDue_InvalidRange_Due()
    {
        var set = new Setting { AutoRestart = 60, RestartTimeRange = "abc" };
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        // 非 2 段格式按无限制处理，直接触发（与原实现一致）
        Assert.True(HealthMonitor.IsAutoRestartDue(set, now.AddMinutes(-120), now, out var inRange));
        Assert.False(inRange);
    }

    [Fact]
    [DisplayName("IsAutoRestartDue_空配置_不触发")]
    public void IsAutoRestartDue_NullSet_NotDue()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0);
        Assert.False(HealthMonitor.IsAutoRestartDue(null, now.AddMinutes(-120), now, out _));
    }
    #endregion

    #region TryParseTimeRange
    [Theory]
    [DisplayName("TryParseTimeRange_合法格式_解析成功")]
    [InlineData("00:00-06:00", 0, 6)]
    [InlineData("22:00-08:00", 22, 8)]
    public void TryParseTimeRange_Valid_Parses(String range, Int32 startHour, Int32 endHour)
    {
        Assert.True(HealthMonitor.TryParseTimeRange(range, out var start, out var end));
        Assert.Equal(startHour, start.Hours);
        Assert.Equal(endHour, end.Hours);
    }

    [Theory]
    [DisplayName("TryParseTimeRange_非法格式_解析失败")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("06:00")]
    [InlineData("abc")]
    [InlineData("a-b")]
    public void TryParseTimeRange_Invalid_Fails(String range)
    {
        Assert.False(HealthMonitor.TryParseTimeRange(range, out _, out _));
    }
    #endregion
}
