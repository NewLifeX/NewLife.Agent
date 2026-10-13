using System.ComponentModel;
using NewLife.Agent.WebPanel;

namespace UnitTest;

/// <summary>DiskMonitor 磁盘统计解析单元测试</summary>
/// <remarks>
/// 直接测试 internal 纯逻辑（InternalsVisibleTo 开放）：
/// /proc/diskstats 解析的设备过滤（主磁盘保留、分区与虚拟设备剔除）、读写累计与 IO 时间取最大值。
/// </remarks>
public class DiskMonitorTests
{
    #region ParseDiskStats
    [Fact]
    [DisplayName("ParseDiskStats_主磁盘保留_分区与虚拟设备剔除")]
    public void ParseDiskStats_FilterDevices_SumsMainDisks()
    {
        // 真实 /proc/diskstats 格式：major minor name，第4列读完成、第8列写完成、第13列 io_ticks
        var content = """
            8       0 sda 100 0 1000 50 200 0 2000 70 0 30 120
            8      16 sda1 10 0 100 5 20 0 200 7 0 3 12
            259       0 nvme0n1 300 0 3000 15 400 0 4000 25 0 40 160
            259       1 nvme0n1p1 1 0 10 0 2 0 20 1 0 0 1
            7       0 loop0 5 0 50 0 0 0 0 0 0 0 0
            253       0 dm-0 7 0 70 0 0 0 0 0 0 0 0
            1       0 ram0 9 0 90 0 0 0 0 0 0 0 0
            8      32 sdb 11 0 110 1 12 0 120 2 0 3 4
            """;

        var (reads, writes, ticks) = DiskMonitor.ParseDiskStats(content);

        // 仅统计 sda、nvme0n1、sdb 三块主磁盘
        Assert.Equal(100 + 300 + 11, reads);
        Assert.Equal(200 + 400 + 12, writes);
        Assert.Equal(40, ticks);
    }

    [Fact]
    [DisplayName("ParseDiskStats_空内容_返回全零")]
    public void ParseDiskStats_Empty_ReturnsZeros()
    {
        var (reads, writes, ticks) = DiskMonitor.ParseDiskStats(null);

        Assert.Equal(0, reads);
        Assert.Equal(0, writes);
        Assert.Equal(0, ticks);

        (reads, writes, ticks) = DiskMonitor.ParseDiskStats("");
        Assert.Equal(0, reads);
        Assert.Equal(0, writes);
        Assert.Equal(0, ticks);
    }

    [Fact]
    [DisplayName("ParseDiskStats_列数不足_整行忽略")]
    public void ParseDiskStats_ShortLine_Ignored()
    {
        // 少于 14 列的行不是合法磁盘统计，整行跳过；混排时不影响其它行
        var content = """
            8       0 sda 100 0 1000
            8      32 sdb 11 0 110 1 12 0 120 2 0 3 4
            """;

        var (reads, writes, ticks) = DiskMonitor.ParseDiskStats(content);

        Assert.Equal(11, reads);
        Assert.Equal(12, writes);
        Assert.Equal(3, ticks);
    }

    [Fact]
    [DisplayName("ParseDiskStats_值非法_按零处理不抛异常")]
    public void ParseDiskStats_InvalidValues_TreatedAsZero()
    {
        var content = "  8       0 sda abc 0 x 0 0 0 0 0 0 x 0";

        var (reads, writes, ticks) = DiskMonitor.ParseDiskStats(content);

        Assert.Equal(0, reads);
        Assert.Equal(0, writes);
        Assert.Equal(0, ticks);
    }

    [Fact]
    [DisplayName("ParseDiskStats_ioTicks_取所有主磁盘最大值")]
    public void ParseDiskStats_MaxIoTicks_TakesMaximum()
    {
        var content = """
            8       0 sda 1 0 10 0 2 0 20 0 0 500 0
            8      32 sdb 1 0 10 0 2 0 20 0 0 300 0
            """;

        var (_, _, ticks) = DiskMonitor.ParseDiskStats(content);

        Assert.Equal(500, ticks);
    }
    #endregion

    #region GetIOPS
    [Fact]
    [DisplayName("GetIOPS_当前平台_不抛异常且非负")]
    public void GetIOPS_CurrentPlatform_NonNegative()
    {
        // 冒烟：Windows 走 PDH/WMIC，Linux 走 /proc/diskstats 首次采样；任何平台都不得抛异常或返回负值
        var iops = DiskMonitor.GetIOPS();

        Assert.True(iops >= 0);
    }

    [WindowsFact]
    [DisplayName("PdhHelper_Windows获取磁盘统计_不崩溃且值域合法")]
    public void PdhHelper_GetDiskStats_ValidRange()
    {
        // 冒烟：验证 P/Invoke 调用链不崩溃；计数器不可用时返回 (-1, -1)
        var (iops, activeTime) = PdhHelper.GetDiskStats();

        Assert.True(iops == -1 || iops >= 0);
        Assert.True(activeTime == -1 || (activeTime >= 0 && activeTime <= 1.0));
    }
    #endregion
}
