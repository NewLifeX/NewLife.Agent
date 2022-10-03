#if NETCOREAPP
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NewLife.Log;

namespace NewLife.Agent.Services;

/// <summary>服务生命周期</summary>
public class ServiceLifetime : ServiceBase, IHostLifetime
{
    private readonly TaskCompletionSource<Object> _delayStart = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ManualResetEventSlim _delayStop = new();

    private readonly HostOptions _hostOptions;

    private IHostApplicationLifetime ApplicationLifetime { get; }

    private IHostEnvironment Environment { get; }

    private ILog Logger { get; }

    /// <summary>实例化服务生命周期</summary>
    /// <param name="environment"></param>
    /// <param name="applicationLifetime"></param>
    /// <param name="log"></param>
    /// <param name="optionsAccessor"></param>
    public ServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILog log, IOptions<HostOptions> optionsAccessor)
        : this(environment, applicationLifetime, log, optionsAccessor, Options.Create(new ServiceLifetimeOptions()))
    {
    }

    /// <summary>实例化服务生命周期</summary>
    /// <param name="environment"></param>
    /// <param name="applicationLifetime"></param>
    /// <param name="log"></param>
    /// <param name="optionsAccessor"></param>
    /// <param name="serviceOptionsAccessor"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public ServiceLifetime(IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILog log, IOptions<HostOptions> optionsAccessor, IOptions<ServiceLifetimeOptions> serviceOptionsAccessor)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        Logger = log;
        if (optionsAccessor == null) throw new ArgumentNullException(nameof(optionsAccessor));
        if (serviceOptionsAccessor == null) throw new ArgumentNullException(nameof(serviceOptionsAccessor));

        _hostOptions = optionsAccessor.Value;

        var opt = serviceOptionsAccessor.Value;
        ServiceName = opt.ServiceName;
        DisplayName = opt.DisplayName;
        Description = opt.Description;
    }

    /// <summary>等待启动</summary>
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
            Logger.Info("Application started. Hosting environment: {envName}; Content root path: {contentRoot}", Environment.EnvironmentName, Environment.ContentRootPath);
        });
        ApplicationLifetime.ApplicationStopping.Register(delegate
        {
            Logger.Info("Application is shutting down...");
        });
        ApplicationLifetime.ApplicationStopped.Register(delegate
        {
            _delayStop.Set();
        });

        var thread = new Thread(new ThreadStart(Run))
        {
            IsBackground = true
        };
        thread.Start();

        return _delayStart.Task;
    }

    private void Run()
    {
        try
        {
            System.ServiceProcess.ServiceBase.Run(this);
            _delayStart.TrySetException(new InvalidOperationException("Stopped without starting"));
        }
        catch (Exception exception)
        {
            _delayStart.TrySetException(exception);
        }
    }

    /// <summary>停止服务</summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Task.Run(() => Host.Stop(ServiceName), CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <summary>开始工作</summary>
    /// <param name="reason"></param>
    protected override void StartWork(String reason)
    {
        _delayStart.TrySetResult(null);

        base.StartWork(reason);
    }

    /// <summary>停止工作</summary>
    /// <param name="reason"></param>
    protected override void StopWork(String reason)
    {
        base.StopWork(reason);

        ApplicationLifetime.StopApplication();
        _delayStop.Wait(_hostOptions.ShutdownTimeout);
    }

    /// <summary>销毁</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            _delayStop.Set();
        }
        base.Dispose(disposing);
    }
}
#endif