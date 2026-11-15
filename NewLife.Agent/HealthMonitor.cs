namespace NewLife.Agent;

/// <summary>健康监控。封装服务健康检查的纯逻辑判断，供 <see cref="ServiceBase"/> 调用，可独立测试</summary>
/// <remarks>
/// 仅包含"判断"逻辑，不含重启/日志等副作用。副作用由调用方（ServiceBase）负责，
/// 从而保证判断逻辑可脱离服务实例独立单元测试。
/// </remarks>
public static class HealthMonitor
{
    /// <summary>判断进程内存是否超标。超过 MaxMemory（MB）应触发重启</summary>
    /// <param name="set">配置</param>
    /// <param name="workingSetMB">进程工作集内存（MB）</param>
    /// <returns>是否超标</returns>
    public static Boolean IsMemoryOver(Setting set, Int64 workingSetMB)
    {
        var max = set?.MaxMemory ?? 0;
        return max > 0 && workingSetMB >= max;
    }

    /// <summary>判断进程线程数是否超标。超过 MaxThread 应触发重启</summary>
    /// <param name="set">配置</param>
    /// <param name="threadCount">进程总线程数</param>
    /// <returns>是否超标</returns>
    public static Boolean IsThreadOver(Setting set, Int32 threadCount)
    {
        var max = set?.MaxThread ?? 0;
        return max > 0 && threadCount >= max;
    }

    /// <summary>判断进程句柄数是否超标。超过 MaxHandle 应触发重启（Windows）</summary>
    /// <param name="set">配置</param>
    /// <param name="handleCount">进程句柄数</param>
    /// <returns>是否超标</returns>
    public static Boolean IsHandleOver(Setting set, Int32 handleCount)
    {
        var max = set?.MaxHandle ?? 0;
        return max > 0 && handleCount >= max;
    }

    /// <summary>判断是否到达自动重启时间。达到 AutoRestart 分钟数且落入 RestartTimeRange 允许时间段时返回 true</summary>
    /// <remarks>
    /// 时间范围规则：范围字符串以 '-' 分割恰好 2 段时校验当前时间是否在范围内，起始时间大于结束时间表示跨零点（如 22:00-06:00）；
    /// 未配置范围或格式非 2 段时视为无限制，直接允许重启。
    /// 运行时长建议传入由单调时钟计算的 <see cref="TimeSpan"/>（见重载），避免系统时间被调整时判断错乱。
    /// </remarks>
    /// <param name="set">配置</param>
    /// <param name="startTime">服务启动时间</param>
    /// <param name="now">当前时间</param>
    /// <param name="inTimeRange">是否配置了重启时间范围且当前时间在范围之内</param>
    /// <returns>是否应触发自动重启</returns>
    public static Boolean IsAutoRestartDue(Setting set, DateTime startTime, DateTime now, out Boolean inTimeRange)
        => IsAutoRestartDue(set, now - startTime, now, out inTimeRange);

    /// <summary>判断是否到达自动重启时间。按已运行时长判断，避免使用系统时间做差</summary>
    /// <param name="set">配置</param>
    /// <param name="elapsed">已运行时长，建议来自单调时钟</param>
    /// <param name="now">当前时间，仅用于判断是否落入允许时间段</param>
    /// <param name="inTimeRange">是否配置了重启时间范围且当前时间在范围之内</param>
    /// <returns>是否应触发自动重启</returns>
    public static Boolean IsAutoRestartDue(Setting set, TimeSpan elapsed, DateTime now, out Boolean inTimeRange)
    {
        inTimeRange = false;

        var auto = set?.AutoRestart ?? 0;
        if (auto <= 0) return false;

        if (elapsed.TotalMinutes < auto) return false;

        return IsInTimeRange(set.RestartTimeRange, now.TimeOfDay, out inTimeRange);
    }

    /// <summary>判断时间点是否落在重启时间范围内</summary>
    /// <remarks>起始大于结束表示跨零点区间（如 22:00-06:00），此时“晚于起始”或“早于结束”均算命中</remarks>
    /// <param name="range">范围字符串，格式 HH:mm-HH:mm</param>
    /// <param name="timeOfDay">待判断的时间点</param>
    /// <param name="inTimeRange">是否显式配置了范围并命中</param>
    /// <returns>是否允许重启；未配置或无法解析时视为不限制</returns>
    public static Boolean IsInTimeRange(String range, TimeSpan timeOfDay, out Boolean inTimeRange)
    {
        inTimeRange = false;

        var parts = range?.Split('-');
        if (parts?.Length != 2) return true;
        if (!TimeSpan.TryParse(parts[0], out var start)) return true;
        if (!TimeSpan.TryParse(parts[1], out var end)) return true;

        inTimeRange = start <= end
            ? timeOfDay >= start && timeOfDay <= end
            : timeOfDay >= start || timeOfDay <= end;

        return inTimeRange;
    }

    /// <summary>解析自动重启时间范围。格式 "HH:mm-HH:mm"，如 "00:00-06:00"</summary>
    /// <param name="range">时间范围字符串</param>
    /// <param name="start">起始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>是否解析成功</returns>
    public static Boolean TryParseTimeRange(String range, out TimeSpan start, out TimeSpan end)
    {
        start = TimeSpan.Zero;
        end = TimeSpan.Zero;

        var parts = range?.Split('-');
        if (parts?.Length != 2) return false;
        if (!TimeSpan.TryParse(parts[0], out start)) return false;
        if (!TimeSpan.TryParse(parts[1], out end)) return false;

        return true;
    }
}
