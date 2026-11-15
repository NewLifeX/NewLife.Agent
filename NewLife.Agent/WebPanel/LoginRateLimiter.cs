#if !NET40
using System.Collections.Concurrent;

namespace NewLife.Agent.WebPanel;

/// <summary>登录爆破防护。按 IP 跟踪失败次数，超限后临时封禁</summary>
public static class LoginRateLimiter
{
    #region 属性
    private class AttemptInfo
    {
        public Int32 Count { get; set; }
        public DateTime FirstFailure { get; set; }
        public DateTime? BlockedUntil { get; set; }
    }

    private static readonly ConcurrentDictionary<String, AttemptInfo> _attempts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>记录失败次数，用于概率性清理</summary>
    private static Int64 _calls;

    /// <summary>最大允许失败次数，默认5次</summary>
    public static Int32 MaxAttempts { get; set; } = 5;

    /// <summary>统计窗口，默认15分钟</summary>
    public static TimeSpan Window { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>封禁时长，默认5分钟</summary>
    public static TimeSpan BlockDuration { get; set; } = TimeSpan.FromMinutes(5);
    #endregion

    /// <summary>检查指定IP是否已被封禁</summary>
    /// <param name="ip">客户端IP地址</param>
    /// <returns>是否被封禁</returns>
    public static Boolean IsBlocked(String ip)
    {
        if (ip.IsNullOrEmpty()) return false;

        if (!_attempts.TryGetValue(ip, out var attempt)) return false;

        lock (attempt)
        {
            if (attempt.BlockedUntil == null) return false;

            if (DateTime.UtcNow >= attempt.BlockedUntil.Value)
            {
                _attempts.TryRemove(ip, out _);
                return false;
            }

            return true;
        }
    }

    /// <summary>记录一次失败尝试</summary>
    /// <param name="ip">客户端IP地址</param>
    public static void RecordFailure(String ip)
    {
        if (ip.IsNullOrEmpty()) return;

        var now = DateTime.UtcNow;
        _attempts.AddOrUpdate(ip, _ => new AttemptInfo
        {
            Count = 1,
            FirstFailure = now
        }, (_, old) =>
        {
            // 同一记录可能被多个并发失败请求同时更新，必须锁住实例，否则计数与封禁时间会互相覆盖导致爆破可越过阈值
            lock (old)
            {
                // 窗口过期，重置
                if (now - old.FirstFailure > Window)
                {
                    old.Count = 1;
                    old.FirstFailure = now;
                    old.BlockedUntil = null;
                }
                else
                {
                    old.Count++;
                    if (old.Count >= MaxAttempts)
                        old.BlockedUntil = now.Add(BlockDuration);
                }
                return old;
            }
        });

        // 失败记录可能来自大量不同来源，概率性清理避免字典无限增长（原先只在测试里调用 Cleanup）
        if (Interlocked.Increment(ref _calls) % 128 == 0) Cleanup();
    }

    /// <summary>记录成功登录，清除该IP记录</summary>
    /// <param name="ip">客户端IP地址</param>
    public static void RecordSuccess(String ip)
    {
        if (ip.IsNullOrEmpty()) return;

        _attempts.TryRemove(ip, out _);
    }

    /// <summary>重置所有记录（仅用于测试）</summary>
    public static void Reset()
    {
        _attempts.Clear();
    }

    /// <summary>清理过期记录</summary>
    public static void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _attempts)
        {
            Boolean expired;
            lock (kv.Value)
            {
                expired = kv.Value.BlockedUntil != null && now >= kv.Value.BlockedUntil.Value;
                if (!expired && now - kv.Value.FirstFailure > Window)
                    expired = true;
            }

            if (expired)
                _attempts.TryRemove(kv.Key, out _);
        }
    }
}
#endif
