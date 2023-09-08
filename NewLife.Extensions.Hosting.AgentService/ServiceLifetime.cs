using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NewLife.Agent;
using NewLife.Log;

/*
 * IHost主机主流程：
 * IHost.RunAsync
 *   IHost.StartAsync
 *     await _hostLifetime.WaitForStartAsync
 *     foreach await IHostedService.StartAsync
 *     ApplicationLifetime.ApplicationStarted
 *   IHost.WaitForShutdownAsync
 *   IHost.StopAsync
 *     ApplicationLifetime.ApplicationStopping
 *     foreach.Reverse await IHostedService.StopAsync
 *     ApplicationLifetime.ApplicationStopped
 *     _hostLifetime.StopAsync
 */

namespace NewLife.Extensions.Hosting.AgentService;

/// <summary>Agent服务主机。接管IHost应用的启动和停止</summary>
public class ServiceLifetime : ServiceBase, IHostLifetime
{
    private readonly TaskCompletionSource<Object> _delayStart = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ManualResetEventSlim _delayStop = new();

    //private readonly HostOptions _hostOptions;

    private IHostApplicationLifetime ApplicationLifetime { get; }

    private IHostEnvironment Environment { get; }

    //private ILog Logger { get; }

    /// <summary>实例化服务生命周期</summary>
    /// <param name="environment"></param>
    /// <param name="applicationLifetime"></param>
    /// <param name="log"></param>
    public ServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILog log)
        : this(environment, applicationLifetime, log, Options.Create(new ServiceLifetimeOptions()))
    {
    }

    /// <summary>实例化服务生命周期</summary>
    /// <param name="environment"></param>
    /// <param name="applicationLifetime"></param>
    /// <param name="log"></param>
    /// <param name="serviceOptionsAccessor"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILog log, IOptions<ServiceLifetimeOptions> serviceOptionsAccessor)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        Log = log;
        //if (optionsAccessor == null) throw new ArgumentNullException(nameof(optionsAccessor));
        if (serviceOptionsAccessor == null) throw new ArgumentNullException(nameof(serviceOptionsAccessor));

        //_hostOptions = optionsAccessor.Value;

        var opt = serviceOptionsAccessor.Value;
        ServiceName = opt.ServiceName;
        DisplayName = opt.DisplayName;
        Description = opt.Description;
    }

    /// <summary>等待启动。IHost.StartAsync调用</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.Register(delegate
        {
            _delayStart.TrySetCanceled();
        });
        ApplicationLifetime.ApplicationStarted.Register(delegate
        {
            Log.Info("Application started. Hosting environment: {0}", Environment.EnvironmentName);
        });
        ApplicationLifetime.ApplicationStopping.Register(delegate
        {
            Log.Info("Application is shutting down...");
        });
        ApplicationLifetime.ApplicationStopped.Register(delegate
        {
            _delayStop.Set();
        });

        // 独立线程启动主逻辑
        var thread = new Thread(new ThreadStart(Run))
        {
            IsBackground = true
        };
        thread.Start();

        // 当前方法返回Task，直到该Task完成，IHost的启动初始化才算完成
        return _delayStart.Task;
    }

    private void Run()
    {
        try
        {
            //System.ServiceProcess.ServiceBase.Run(this);
            Main(null);

            //todo 这里很遗憾，如果菜单使用0退出，这里不得不抛出异常，以打断IHost.StartAsync向下执行IHostedService
            //Log.Info("退出菜单，后续异常无需理会");
            _delayStart.TrySetException(new InvalidOperationException("正常退出菜单"));
            //_delayStart.TrySetResult(null);
        }
        catch (Exception exception)
        {
            // 通知IHost启动失败
            _delayStart.TrySetException(exception);
        }
    }

    /// <summary>停止服务</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Host is NewLife.Agent.DefaultHost host && host.InService)
            Task.Run(() => Host.Stop(ServiceName), CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <summary>开始工作</summary>
    /// <param name="reason"></param>
    protected override void StartWork(String reason)
    {
        // 设置结果，说明IHost启动已完成
        _delayStart.TrySetResult(null);

        base.StartWork(reason);
    }

    /// <summary>停止工作</summary>
    /// <param name="reason"></param>
    protected override void StopWork(String reason)
    {
        base.StopWork(reason);

        ApplicationLifetime.StopApplication();
        //_delayStop.Wait(_hostOptions.ShutdownTimeout);
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing) _delayStop.Set();

        base.Dispose(disposing);
    }
}