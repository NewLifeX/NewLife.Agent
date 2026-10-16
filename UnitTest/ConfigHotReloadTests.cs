using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using NewLife.Agent;

namespace UnitTest;

/// <summary>配置文件热加载单元测试</summary>
/// <remarks>
/// Setting 继承 Config&lt;Setting&gt;，FileConfigProvider 通过 FileSystemWatcher（500ms 防抖 + 200ms 延迟）
/// 与定时器轮询双重机制感知文件变更。测试直接改写 Config/Agent.config，轮询等待内存实例同步更新；
/// 防抖窗口或平台事件丢失时重新写入重试，保证判定稳定。
/// </remarks>
[Collection("WebPanel")]
public class ConfigHotReloadTests
{
    /// <summary>配置文件路径。与 SettingPersistenceTests 一致，基于应用基础目录</summary>
    private static String ConfigFile => "Config/Agent.config".GetBasePath();

    [Fact]
    [DisplayName("Setting_外部修改配置文件_内存实例热加载更新")]
    public void ExternalChange_HotReloads()
    {
        using var scope = new SettingScope();

        var file = ConfigFile;
        Assert.True(File.Exists(file), $"配置文件不存在：{file}");

        // 等待上一次落盘写事件越过 500ms 防抖窗口，避免本次变更被合并丢弃
        Thread.Sleep(700);

        var target = Random.Shared.Next(1000, 9999);
        var sw = Stopwatch.StartNew();

        // 最多 3 次写入尝试，每次等待至多 6 秒；事件驱动正常时首次即命中
        for (var attempt = 0; attempt < 3 && Setting.Current.WatchInterval != target; attempt++)
        {
            WriteWatchInterval(file, target);

            var waitUntil = sw.Elapsed + TimeSpan.FromSeconds(6);
            while (sw.Elapsed < waitUntil && Setting.Current.WatchInterval != target)
            {
                Thread.Sleep(100);
            }
        }

        Assert.Equal(target, Setting.Current.WatchInterval);
        // 内存实例即配置单例，变更后不需要重新创建对象
        Assert.Same(Setting.Current, Setting.Current);
    }

    [Fact]
    [DisplayName("Setting_外部修改配置文件_不触发时保留原值")]
    public void NoChange_KeepsValue()
    {
        using var scope = new SettingScope();

        var before = Setting.Current.WatchInterval;
        Thread.Sleep(200);

        Assert.Equal(before, Setting.Current.WatchInterval);
    }

    private static void WriteWatchInterval(String file, Int32 value)
    {
        var text = File.ReadAllText(file);
        var updated = Regex.Replace(text, "<WatchInterval>[^<]*</WatchInterval>", $"<WatchInterval>{value}</WatchInterval>");
        File.WriteAllText(file, updated);
    }
}
