using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NewLife.Agent;
using NewLife.Log;

namespace NewLife.Extensions.Hosting.AgentService;

/// <summary>服务声明周期扩展</summary>
public static class ServiceLifetimeHostBuilderExtensions
{
    /// <summary>启用NewLifeAgent服务，自动识别支持Windows/Linux</summary>
    /// <param name="hostBuilder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static IHostBuilder UseAgentService(this IHostBuilder hostBuilder, Action<ServiceLifetimeOptions> configure = null)
    {
        ServiceBase.InitService();

        //hostBuilder.UseContentRoot(AppContext.BaseDirectory);
        hostBuilder.ConfigureServices(delegate (HostBuilderContext hostContext, IServiceCollection services)
        {
            services.AddSingleton<IHostLifetime, ServiceLifetime>();
            services.TryAddSingleton(XTrace.Log);

            // 允许无参调用：configure 为空时跳过选项配置（Options.Configure 不接受空委托）
            if (configure != null) services.Configure(configure);
        });

        return hostBuilder;
    }
}