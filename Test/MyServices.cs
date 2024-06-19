using System;
using NewLife.Agent;
using NewLife.Log;
using NewLife.Threading;
using NewLife.Agent.Windows;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace Test;

/// <summary>代理服务例子。自定义服务程序可参照该类实现。</summary>
public class MyServices : ServiceBase
{
    #region 属性
    #endregion

    #region 构造函数
    /// <summary>实例化一个代理服务</summary>
    public MyServices()
    {
        // 一般在构造函数里面指定服务名
        ServiceName = "XAgent";

        DisplayName = "新生命服务代理";
        Description = "用于承载各种服务的服务代理！";

        //UseAutorun = true;
    }
    #endregion

    #region 核心
    private TimerX _timer;
    private TimerX _timer2;
    /// <summary>开始工作</summary>
    /// <param name="reason"></param>
    public override void StartWork(String reason)
    {
        WriteLog("业务开始……");

        // 5秒开始，每60秒执行一次
        _timer = new TimerX(DoWork, null, 1_000, 60_000) { Async = true };
        // 每天凌晨2点13分执行一次
        _timer2 = new TimerX(DoWork, null, DateTime.Today.AddMinutes(2 * 60 + 13), 24 * 3600 * 1000) { Async = true };

        base.StartWork(reason);
    }

    private void DoWork(Object state)
    {
        var data = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 日志会输出到 Bin/Test 目录中
        XTrace.WriteLine($"代码执行时间：{data}");

        //Process.Start("ping.exe", "-n 10 newlifex.com");

        var desktop = new Desktop { Log = Log };
        //desktop.StartProcess(@"D:\Net\ILSpy\ILSpy\ILSpy.exe");
        //desktop.StartProcess(@"D:\X\NewLife.XCoder\Bin\CrazyCoder\CrazyCoder.exe");
        //desktop.StartProcess(@"ping.exe", "-n 10 newlifex.com");
        //desktop.StartProcess("../net45/test.exe", "-n 10 newlifex.com");

        desktop.StartProcess(@"C:\Windows\System32\notepad.exe");

        //var file = Assembly.GetEntryAssembly().Location;
        //file = Path.ChangeExtension(file, ".exe");
        //desktop.StartProcess(file);
    }

    /// <summary>停止服务</summary>
    /// <param name="reason"></param>
    public override void StopWork(String reason)
    {
        WriteLog("业务结束！{0}", reason);

        _timer.Dispose();
        _timer2.Dispose();

        base.StopWork(reason);
    }
    #endregion
}