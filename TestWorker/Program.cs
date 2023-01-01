using NewLife.Extensions.Hosting.AgentService;
using TestWorker;

Environment.CurrentDirectory = ".".GetFullPath();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddStardust();

        services.AddHostedService<Worker>();
    })
    //.UseWindowsService(options =>
    //{
    //    options.ServiceName = "TestWorker";
    //})
    .UseAgentService(options =>
    {
        options.ServiceName = "TestWorker";
        options.DisplayName = "Worker服务测试";
        options.Description = "Worker服务的测试应用";
    })
    .Build();

await host.RunAsync();
