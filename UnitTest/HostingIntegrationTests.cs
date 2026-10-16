using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NewLife.Agent;
using NewLife.Extensions.Hosting.AgentService;
using NewLife.Log;
using NewLifeServiceLifetime = NewLife.Extensions.Hosting.AgentService.ServiceLifetime;

namespace UnitTest;

/// <summary>Host 集成单元测试（M6-4/M6-5）</summary>
/// <remarks>
/// 通过 HostBuilder + UseAgentService 进程内构建真实 IHost，端到端验证
/// “IHost 启停 ↔ ServiceLifetime ↔ ServiceBase” 桥接链路（ASP.NET Core 与 Worker 共用同一 IHostLifetime 机制）。
/// 测试宿主进程会携带 --port 等参数，命令行分支被测试子类改写为无参数启动路径，交互菜单以可注入方式替换。
/// </remarks>
[Collection("WebPanel")]
public class HostingIntegrationTests
{
    /// <summary>可观测的后台服务，用于验证宿主内托管服务的启动与停止</summary>
    private sealed class ProbeService : IHostedService
    {
        public Boolean Started { get; private set; }

        public Boolean Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>集成测试生命周期。把交互菜单替换为“模拟服务宿主启动”，StartLoop 触发 StartWork 后等待停止信号</summary>
    private sealed class IntegrationLifetime : NewLifeServiceLifetime
    {
        private readonly ManualResetEventSlim _stop = new(false);

        public IntegrationLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILog log, IOptions<ServiceLifetimeOptions> options)
            : base(environment, applicationLifetime, log, options)
        {
        }

        /// <summary>菜单路径是否已进入</summary>
        public Boolean MenuEntered { get; private set; }

        /// <inheritdoc/>
        protected override void ProcessCommand(String cmd, String[] args)
        {
            // 测试宿主进程携带 --port 等参数，会命中 Main 的命令行分支；
            // 这里还原控制台应用无参数启动时的路径，交由 ProcessMenu 驱动服务启动。
            ProcessMenu(args);
        }

        /// <inheritdoc/>
        protected override void ProcessMenu(String[] args)
        {
            MenuEntered = true;

            // StartLoop 内部调用 StartWork，ServiceLifetime.StartWork 会置位 _delayStart，
            // 从而让 IHost.StartAsync 完成；随后阻塞等待测试释放，模拟服务常驻。
            StartLoop();
            _stop.Wait(TimeSpan.FromSeconds(30));
        }

        /// <summary>释放菜单阻塞，让宿主线程正常收尾</summary>
        public void Release() => _stop.Set();
    }

    [Fact]
    [DisplayName("Host集成_UseAgentService_驱动IHost完整启动与停止")]
    public async Task Host_UseAgentService_StartAndStop()
    {
        using var scope = new SettingScope();
        // 关闭 Web 面板，避免测试绑定端口
        Setting.Current.EnableWebPanel = false;

        IntegrationLifetime lifetime = null;
        var probe = new ProbeService();

        using var host = new HostBuilder()
            .UseAgentService(o =>
            {
                o.ServiceName = "UT-Host-" + Guid.NewGuid().ToString("N");
                o.DisplayName = "单元测试宿主";
                o.Description = "Host 集成";
            })
            .ConfigureServices(services =>
            {
                // 覆盖默认 ServiceLifetime 注册（后者生效），注入可观测的集成测试替身
                services.AddSingleton<IHostLifetime>(sp => lifetime = new IntegrationLifetime(
                    sp.GetRequiredService<IHostEnvironment>(),
                    sp.GetRequiredService<IHostApplicationLifetime>(),
                    Logger.Null,
                    Options.Create(new ServiceLifetimeOptions
                    {
                        ServiceName = "UT-Host",
                        DisplayName = "单元测试宿主",
                        Description = "Host 集成",
                    })));
                services.AddHostedService(_ => probe);
            })
            .Build();

        await host.StartAsync();

        Assert.NotNull(lifetime);
        Assert.True(lifetime.MenuEntered);
        Assert.True(probe.Started);

        await host.StopAsync();

        Assert.True(probe.Stopped);

        lifetime.Release();
    }

    [Fact]
    [DisplayName("Host集成_UseAgentService_注册IHostLifetime为真实生命周期实现")]
    public void UseAgentService_RegistersLifetime()
    {
        using var host = new HostBuilder()
            .UseAgentService()
            .Build();

        var lifetime = host.Services.GetRequiredService<IHostLifetime>();

        Assert.IsType<NewLifeServiceLifetime>(lifetime);
        host.Dispose();
    }
}
