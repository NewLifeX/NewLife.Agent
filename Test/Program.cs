using System;
using System.Linq;
using System.Threading;
using NewLife;
using NewLife.Agent.Windows;
using NewLife.Log;

namespace Test;

public class Program
{
    private static void Main(String[] args)
    {
        XTrace.UseConsole();

        // NativeAOT 冒烟：自检 Web 面板（内嵌资源、i18n 词表、JSON 源生成与全局配置反射）
        if (args != null && args.Any(e => e.EqualIgnoreCase("-panel")))
        {
            Environment.ExitCode = PanelSmoke.Run();
            return;
        }

        //var power = new PowerStatus();
        //for (var i = 0; i < 10; i++)
        //{
        //    XTrace.WriteLine("PowerEvent: {0}, LineStatus={1}, LifePercent={2:p0}, ChargeStatus={3}", "xxx", power.PowerLineStatus, power.BatteryLifePercent, power.BatteryChargeStatus);
        //    Thread.Sleep(1000);
        //}

        var svc = new MyServices();
        svc.Main(args);
    }
}