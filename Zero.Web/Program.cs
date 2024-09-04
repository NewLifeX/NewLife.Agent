using System;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NewLife.Agent;

namespace Zero.Web;

public class Program
{
    private static void Main(String[] args)
    {
#if DEBUG
        //调试环境默认启动
        if (args?.Length == 0)
            args = ["-run"];
#endif

        new MyServices { StartAct = () => CreateHostBuilder(args) }.Main(args);
    }

    public static IHostBuilder CreateHostBuilder(String[] args) =>
    Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
}

/// <summary>代理服务例子。自定义服务程序可参照该类实现。</summary>
public class MyServices : ServiceBase
{
    #region 属性
    /// <summary>ihost启动委托</summary>
    public Func<IHostBuilder> StartAct { get; set; }
    #endregion

    #region 构造函数
    /// <summary>实例化一个代理服务</summary>
    public MyServices()
    {
        // 一般在构造函数里面指定服务名
        ServiceName = "WebAgent";

        DisplayName = "Web服务代理";
        Description = "用于承载各种服务的服务代理！";
    }

    protected override void Init()
    {
        base.Init();

        // 依赖网络
        if (Host is Systemd sys)
            sys.Setting.Network = true;
    }
    #endregion

    #region 核心
    private CancellationTokenSource _source;
    /// <summary>开始工作</summary>
    /// <param name="reason"></param>
    public override void StartWork(String reason)
    {
        WriteLog("业务开始……");

        // 提前设置好当前目录，避免后续各种问题
        //Environment.CurrentDirectory = ".".GetFullPath();

        _source = new CancellationTokenSource();
        if (StartAct != null)
        {
            var host = StartAct.Invoke();
            host.Build().RunAsync(_source.Token);
        }
        //CreateHostBuilder(Args).Build().RunAsync(_source.Token);

        base.StartWork(reason);
    }



    /// <summary>停止服务</summary>
    /// <param name="reason"></param>
    public override void StopWork(String reason)
    {
        WriteLog("业务结束！{0}", reason);

        _source.Cancel();

        base.StopWork(reason);
    }
    #endregion
}