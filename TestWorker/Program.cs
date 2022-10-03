using TestWorker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .UseWindowsService(options =>
    {
        options.ServiceName = "TestWorker";
    })
    .Build();

await host.RunAsync();
