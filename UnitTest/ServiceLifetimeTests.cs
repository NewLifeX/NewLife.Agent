using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NewLife.Agent;
using NewLife.Extensions.Hosting.AgentService;
using NewLife.Log;

namespace UnitTest;

/// <summary>ServiceLifetimeOptions 选项单元测试</summary>
public class ServiceLifetimeOptionsTests
{
    [Fact]
    [DisplayName("ServiceLifetimeOptions_默认值为空")]
    public void Defaults_AreNull()
    {
        var opt = new ServiceLifetimeOptions();
        Assert.Null(opt.ServiceName);
        Assert.Null(opt.DisplayName);
        Assert.Null(opt.Description);
    }

    [Fact]
    [DisplayName("ServiceLifetimeOptions_属性读写")]
    public void Properties_ReadWrite()
    {
        var opt = new ServiceLifetimeOptions
        {
            ServiceName = "MySvc",
            DisplayName = "我的服务",
            Description = "测试服务"
        };

        Assert.Equal("MySvc", opt.ServiceName);
        Assert.Equal("我的服务", opt.DisplayName);
        Assert.Equal("测试服务", opt.Description);
    }
}

/// <summary>ServiceLifetime Host 集成单元测试</summary>
/// <remarks>验证 ServiceBase 与 IHostLifetime 的桥接行为</remarks>
public class ServiceLifetimeTests
{
    private static (Mock<IHostEnvironment> env, Mock<IHostApplicationLifetime> lifetime) CreateMocks()
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns("Test");
        var lifetime = new Mock<IHostApplicationLifetime>();
        return (env, lifetime);
    }

    [Fact]
    [DisplayName("ServiceLifetime_构造_从Options读取服务元数据")]
    public void Constructor_ReadsOptions()
    {
        var (env, lifetime) = CreateMocks();
        var opt = Options.Create(new ServiceLifetimeOptions { ServiceName = "MySvc", DisplayName = "我的服务", Description = "描述" });

        var svc = new ServiceLifetime(env.Object, lifetime.Object, Logger.Null, opt);

        Assert.Equal("MySvc", svc.ServiceName);
        Assert.Equal("我的服务", svc.DisplayName);
        Assert.Equal("描述", svc.Description);
    }

    [Fact]
    [DisplayName("ServiceLifetime_构造_环境为null抛异常")]
    public void Constructor_NullEnvironment_Throws()
    {
        var (_, lifetime) = CreateMocks();
        Assert.Throws<ArgumentNullException>(() => new ServiceLifetime(null!, lifetime.Object, Logger.Null));
    }

    [Fact]
    [DisplayName("ServiceLifetime_构造_生命周期为null抛异常")]
    public void Constructor_NullLifetime_Throws()
    {
        var (env, _) = CreateMocks();
        Assert.Throws<ArgumentNullException>(() => new ServiceLifetime(env.Object, null!, Logger.Null));
    }

    [Fact]
    [DisplayName("ServiceLifetime_构造_选项为null抛异常")]
    public void Constructor_NullOptions_Throws()
    {
        var (env, lifetime) = CreateMocks();
        Assert.Throws<ArgumentNullException>(() => new ServiceLifetime(env.Object, lifetime.Object, Logger.Null, null!));
    }

    [Fact]
    [DisplayName("ServiceLifetime_StopWork_触发应用停止")]
    public void StopWork_StopsApplication()
    {
        var (env, lifetime) = CreateMocks();
        var svc = new ServiceLifetime(env.Object, lifetime.Object, Logger.Null);

        svc.StopWork("test");

        lifetime.Verify(e => e.StopApplication(), Times.Once);
    }

    [Fact]
    [DisplayName("ServiceLifetime_StartWork_不抛异常")]
    public void StartWork_NoException()
    {
        var (env, lifetime) = CreateMocks();
        var svc = new ServiceLifetime(env.Object, lifetime.Object, Logger.Null);

        // 关闭 Web 面板，避免 StartWork 启动 HttpServer
        var set = Setting.Current;
        var prev = set.EnableWebPanel;
        set.EnableWebPanel = false;
        try
        {
            var ex = Record.Exception(() => { svc.StartWork("test"); });
            Assert.Null(ex);
        }
        finally
        {
            set.EnableWebPanel = prev;
        }
    }

    [Fact]
    [DisplayName("ServiceLifetime_StopAsync_返回已完成任务")]
    public void StopAsync_ReturnsCompleted()
    {
        var (env, lifetime) = CreateMocks();
        var svc = new ServiceLifetime(env.Object, lifetime.Object, Logger.Null);

        var task = svc.StopAsync(CancellationToken.None);
        Assert.True(task.IsCompleted);
    }
}

/// <summary>UseAgentService 扩展方法单元测试</summary>
public class ServiceLifetimeHostBuilderExtensionsTests
{
    [Fact]
    [DisplayName("UseAgentService_注册ServiceLifetime")]
    public void UseAgentService_RegistersServiceLifetime()
    {
        var builder = new HostBuilder();
        builder.UseAgentService(o => { o.ServiceName = "MySvc"; });

        using var host = builder.Build();
        var lifetime = (IHostLifetime)host.Services.GetService(typeof(IHostLifetime))!;

        Assert.NotNull(lifetime);
        Assert.IsType<NewLife.Extensions.Hosting.AgentService.ServiceLifetime>(lifetime);
    }
}
