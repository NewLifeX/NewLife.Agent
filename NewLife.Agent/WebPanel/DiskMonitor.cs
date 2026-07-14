#if !NET40
using System.Diagnostics;
using System.Text.RegularExpressions;
using NewLife;

namespace NewLife.Agent.WebPanel;

/// <summary>磁盘监控辅助类</summary>
/// <remarks>
/// 跨平台采集磁盘 IOPS（每秒读写次数总和）。
/// Windows：优先 PDH API，降级 WMIC；
/// Linux：读取 /proc/diskstats 两次采样差分计算。
/// </remarks>
internal static class DiskMonitor
{
    #region Linux 采样缓存
    private static Int64 _lastSampleTime;
    private static Int64 _lastReads;
    private static Int64 _lastWrites;
    private static readonly Object _lock = new();
    #endregion

    /// <summary>获取磁盘 IOPS</summary>
    /// <returns>每秒读写次数总和</returns>
    public static Int32 GetIOPS()
    {
        if (Runtime.Windows) return GetIOPSWindows();
        if (Runtime.Linux) return GetIOPSLinux();
        return 0;
    }

    #region Windows
    /// <summary>Windows 平台获取磁盘 IOPS</summary>
    private static Int32 GetIOPSWindows()
    {
        // 优先使用 PDH API
        try
        {
            var (iops, _) = PdhHelper.GetDiskStats();
            if (iops >= 0) return iops;
        }
        catch { }

        // 降级为 WMIC 方式（直接创建进程，不依赖 String.Execute 扩展方法）
        try
        {
            var rs = ExecuteWmic("path Win32_PerfFormattedData_PerfDisk_PhysicalDisk where \"Name='_Total'\" get DiskReadsPersec, DiskWritesPersec /value");
            if (!rs.IsNullOrEmpty())
            {
                var reads = 0;
                var writes = 0;

                var lines = rs.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWithIgnoreCase("DiskReadsPersec="))
                        reads = line["DiskReadsPersec=".Length..].Trim().ToInt();
                    else if (line.StartsWithIgnoreCase("DiskWritesPersec="))
                        writes = line["DiskWritesPersec=".Length..].Trim().ToInt();
                }

                return reads + writes;
            }
        }
        catch { }

        return 0;
    }
    /// <summary>执行 WMIC 命令并返回输出</summary>
    /// <param name="arguments">WMIC 参数</param>
    /// <returns>命令输出，失败返回 null</returns>
    private static String ExecuteWmic(String arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("wmic", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return null;

            if (!process.WaitForExit(5_000))
            {
                try { process.Kill(); } catch { }
                return null;
            }

            return process.StandardOutput.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }
    #endregion

    #region Linux
    /// <summary>Linux 平台获取磁盘 IOPS</summary>
    private static Int32 GetIOPSLinux()
    {
        try
        {
            var file = "/proc/diskstats";
            if (!File.Exists(file)) return 0;

            var content = File.ReadAllText(file);
            var (totalReads, totalWrites, _) = ParseDiskStats(content);
            var now = Runtime.TickCount64;

            lock (_lock)
            {
                // 首次采样，保存数据并返回 0
                if (_lastSampleTime == 0)
                {
                    _lastSampleTime = now;
                    _lastReads = totalReads;
                    _lastWrites = totalWrites;
                    return 0;
                }

                var elapsed = now - _lastSampleTime;
                if (elapsed <= 0) return 0;

                // 计算差值
                var deltaReads = totalReads - _lastReads;
                var deltaWrites = totalWrites - _lastWrites;

                // 处理计数器回绕或重置
                if (deltaReads < 0) deltaReads = totalReads;
                if (deltaWrites < 0) deltaWrites = totalWrites;

                // 更新采样
                _lastSampleTime = now;
                _lastReads = totalReads;
                _lastWrites = totalWrites;

                // IOPS = 操作次数差 / 时间差（秒）
                var elapsedSeconds = elapsed / 1000.0;
                return (Int32)((deltaReads + deltaWrites) / elapsedSeconds);
            }
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>解析 /proc/diskstats，提取读写完成次数</summary>
    /// <param name="content">/proc/diskstats 原始内容</param>
    /// <returns>读次数、写次数和最大 IO 活动时间（毫秒）</returns>
    internal static (Int64 TotalReads, Int64 TotalWrites, Int64 MaxIoTicks) ParseDiskStats(String content)
    {
        if (content.IsNullOrEmpty()) return (0, 0, 0);

        var totalReads = 0L;
        var totalWrites = 0L;
        var maxIoTicks = 0L;

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 14) continue;

            // parts[0]=major parts[1]=minor parts[2]=device name
            var deviceName = parts[2];

            // 只统计主磁盘（如 sda、nvme0n1），跳过分区和虚拟设备
            if (deviceName.StartsWith("loop") || deviceName.StartsWith("ram") || deviceName.StartsWith("dm-"))
                continue;
            if (Regex.IsMatch(deviceName, @"^[a-z]+\d+$") && !deviceName.StartsWith("nvme"))
                continue;
            if (Regex.IsMatch(deviceName, @"^nvme\d+n\d+p\d+$"))
                continue;

            // 第 4 列：读完成次数，第 8 列：写完成次数
            totalReads += parts[3].ToLong();
            totalWrites += parts[7].ToLong();

            // 第 13 列：io_ticks（磁盘活动时间，毫秒）
            var ioTicks = parts[12].ToLong();
            if (ioTicks > maxIoTicks) maxIoTicks = ioTicks;
        }

        return (totalReads, totalWrites, maxIoTicks);
    }
    #endregion
}
#endif
