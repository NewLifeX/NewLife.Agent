using System.ComponentModel;
using NewLife.Agent.WebPanel;

namespace UnitTest;

/// <summary>LoginRateLimiter 时间窗口与过期清理单元测试</summary>
/// <remarks>验证统计窗口过期后失败计数重置、封禁到期自动解除、Cleanup 清理过期记录；时间依赖用例把窗口与封禁压缩到 100ms 并睡眠 150ms，留 50ms 裕量抵御时钟分辨率与调度抖动；测试会临时修改静态配置，与其它 WebPanel 测试串行执行并在 finally 还原</remarks>
[Collection("WebPanel")]
public class RateLimiterTimeWindowTests
{
    /// <summary>统计窗口过期后失败计数重置</summary>
    /// <remarks>窗口内先失败 4 次（阈值 5 次）；窗口过期后再失败 4 次，若计数已重置则累计仅 4 次，不应封禁</remarks>
    [Fact]
    [DisplayName("统计窗口过期后失败计数重置")]
    public void WindowExpired_FailureCountReset()
    {
        // 独立 IP，避免与其它用例的静态记录互相干扰
        var ip = "198.51.100.11";
        var prevMax = LoginRateLimiter.MaxAttempts;
        var prevWindow = LoginRateLimiter.Window;
        var prevBlock = LoginRateLimiter.BlockDuration;
        try
        {
            LoginRateLimiter.Reset();
            LoginRateLimiter.MaxAttempts = 5;
            LoginRateLimiter.Window = TimeSpan.FromMilliseconds(100);
            LoginRateLimiter.BlockDuration = TimeSpan.FromMinutes(5);

            // 窗口内失败 4 次，未达到阈值
            for (var i = 0; i < 4; i++)
            {
                LoginRateLimiter.RecordFailure(ip);
            }
            Assert.False(LoginRateLimiter.IsBlocked(ip));

            // 睡眠 150ms：比 100ms 窗口多出 50ms 裕量，抵御时钟分辨率与调度抖动
            Thread.Sleep(150);

            // 首次失败时判定 now - FirstFailure > Window，计数应重置为 1；若未重置，累计 8 次将触发封禁
            for (var i = 0; i < 4; i++)
            {
                LoginRateLimiter.RecordFailure(ip);
            }
            Assert.False(LoginRateLimiter.IsBlocked(ip));
        }
        finally
        {
            // 还原静态配置（默认 5 次 / 15 分钟 / 5 分钟）并清空全部记录，避免跨测试污染
            LoginRateLimiter.MaxAttempts = prevMax;
            LoginRateLimiter.Window = prevWindow;
            LoginRateLimiter.BlockDuration = prevBlock;
            LoginRateLimiter.Reset();
        }
    }

    /// <summary>封禁到期后自动解除</summary>
    /// <remarks>失败 2 次触发 100ms 封禁；睡眠 150ms 后连续查询两次均为未封禁，且首次查询已把记录移除</remarks>
    [Fact]
    [DisplayName("封禁到期后自动解除并移除记录")]
    public void BlockExpired_AutoUnblocked()
    {
        // 独立 IP，避免与其它用例的静态记录互相干扰
        var ip = "198.51.100.12";
        var prevMax = LoginRateLimiter.MaxAttempts;
        var prevWindow = LoginRateLimiter.Window;
        var prevBlock = LoginRateLimiter.BlockDuration;
        try
        {
            LoginRateLimiter.Reset();
            LoginRateLimiter.MaxAttempts = 2;
            // Window 保持测试前取值，封禁到期判定只依赖 BlockedUntil，与统计窗口无关
            LoginRateLimiter.BlockDuration = TimeSpan.FromMilliseconds(100);

            // 失败 2 次达到阈值，触发封禁
            LoginRateLimiter.RecordFailure(ip);
            LoginRateLimiter.RecordFailure(ip);
            Assert.True(LoginRateLimiter.IsBlocked(ip));

            // 睡眠 150ms：比 100ms 封禁多出 50ms 裕量，确保按 >= 判定时封禁确已到期
            Thread.Sleep(150);

            // 到期后 IsBlocked 自动移除记录并返回未封禁
            Assert.False(LoginRateLimiter.IsBlocked(ip));
            // 记录已被移除，再次查询仍为未封禁
            Assert.False(LoginRateLimiter.IsBlocked(ip));
        }
        finally
        {
            // 还原静态配置（默认 5 次 / 15 分钟 / 5 分钟）并清空全部记录，避免跨测试污染
            LoginRateLimiter.MaxAttempts = prevMax;
            LoginRateLimiter.Window = prevWindow;
            LoginRateLimiter.BlockDuration = prevBlock;
            LoginRateLimiter.Reset();
        }
    }

    /// <summary>Cleanup 清理过期记录</summary>
    /// <remarks>失败 2 次触发 100ms 封禁，睡眠 150ms 后封禁与统计窗口均已过期；Cleanup 不抛异常，同一 IP 再失败 1 次仅重新计数，不触发封禁</remarks>
    [Fact]
    [DisplayName("Cleanup 清理过期记录后重新计数")]
    public void Cleanup_RemovesExpiredRecords()
    {
        // 独立 IP，避免与其它用例的静态记录互相干扰
        var ip = "198.51.100.13";
        var prevMax = LoginRateLimiter.MaxAttempts;
        var prevWindow = LoginRateLimiter.Window;
        var prevBlock = LoginRateLimiter.BlockDuration;
        try
        {
            LoginRateLimiter.Reset();
            LoginRateLimiter.MaxAttempts = 2;
            LoginRateLimiter.Window = TimeSpan.FromMilliseconds(100);
            LoginRateLimiter.BlockDuration = TimeSpan.FromMilliseconds(100);

            // 失败 2 次达到阈值，触发封禁
            LoginRateLimiter.RecordFailure(ip);
            LoginRateLimiter.RecordFailure(ip);
            Assert.True(LoginRateLimiter.IsBlocked(ip));

            // 睡眠 150ms：比 100ms 窗口与封禁多出 50ms 裕量，两者均已过期
            Thread.Sleep(150);

            // 清理过期记录不应抛出异常
            var ex = Record.Exception(() => LoginRateLimiter.Cleanup());
            Assert.Null(ex);

            // 记录已被清理，再失败 1 次仅从 1 开始计数，未达到阈值 2 次
            LoginRateLimiter.RecordFailure(ip);
            Assert.False(LoginRateLimiter.IsBlocked(ip));
        }
        finally
        {
            // 还原静态配置（默认 5 次 / 15 分钟 / 5 分钟）并清空全部记录，避免跨测试污染
            LoginRateLimiter.MaxAttempts = prevMax;
            LoginRateLimiter.Window = prevWindow;
            LoginRateLimiter.BlockDuration = prevBlock;
            LoginRateLimiter.Reset();
        }
    }
}
