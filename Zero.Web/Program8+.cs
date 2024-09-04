//using System;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using NewLife;
//using NewLife.Cube;
//using NewLife.Cube.WebMiddleware;
//using NewLife.Log;
//using NewLife.Remoting;
//using Stardust.Monitors;
//using XCode.DataAccessLayer;




//XTrace.UseConsole();

////var builder = WebApplication.CreateBuilder(args);
////var services = builder.Services;

////var set = Zero.Web.Setting.Current;
////if (!set.TracerServer.IsNullOrEmpty())
////{
////    // APM跟踪器
////    var tracer = new StarTracer(set.TracerServer) { Log = XTrace.Log };
////    DefaultTracer.Instance = tracer;
////    ApiHelper.Tracer = tracer;
////    DAL.GlobalTracer = tracer;
////    TracerMiddleware.Tracer = tracer;

////    services.AddSingleton<ITracer>(tracer);
////}

////services.AddControllersWithViews();

////// 引入魔方
////services.AddCube();

////var app = builder.Build();

////app.UseCube(builder.Environment);

////app.UseEndpoints(endpoints =>
////{
////    endpoints.MapControllerRoute(
////        name: "default",
////        pattern: "{controller=CubeHome}/{action=Index}/{id?}");
////});

////app.Run();


//static WebApplication GetApp()
//{
//    //这里存放你原有的逻辑
//    var args = Environment.GetCommandLineArgs();

//    var builder = WebApplication.CreateBuilder(args);
//    var services = builder.Services;

//    var set = Zero.Web.Setting.Current;
//    if (!set.TracerServer.IsNullOrEmpty())
//    {
//        // APM跟踪器
//        var tracer = new StarTracer(set.TracerServer) { Log = XTrace.Log };
//        DefaultTracer.Instance = tracer;
//        ApiHelper.Tracer = tracer;
//        DAL.GlobalTracer = tracer;
//        TracerMiddleware.Tracer = tracer;

//        services.AddSingleton<ITracer>(tracer);
//    }

//    services.AddControllersWithViews();

//    // 引入魔方
//    services.AddCube();

//    var app = builder.Build();

//    app.UseCube(builder.Environment);

//    app.UseEndpoints(endpoints =>
//    {
//        endpoints.MapControllerRoute(
//            name: "default",
//            pattern: "{controller=CubeHome}/{action=Index}/{id?}");
//    });

//    return app;
//}

//#if DEBUG
////调试环境默认启动
//if (args?.Length == 0)
//    args = ["-run"];
//#endif
//new Zero.Web.MyServices { StartAct = GetApp }.Main(args);