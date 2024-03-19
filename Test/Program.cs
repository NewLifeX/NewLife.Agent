using System;
using System.Threading;
using NewLife.Agent.Windows;
using NewLife.Log;

namespace Test;

public class Program
{
    private static void Main(String[] args)
    {
        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1");

        XTrace.UseConsole();

        var power = new PowerStatus();
        for (var i = 0; i < 10; i++)
        {
            XTrace.WriteLine("PowerEvent: {0}, LineStatus={1}, LifePercent={2:p0}, ChargeStatus={3}", "xxx", power.PowerLineStatus, power.BatteryLifePercent, power.BatteryChargeStatus);
            Thread.Sleep(1000);
        }

        var svc = new MyServices();
        svc.Main(args);
    }
}