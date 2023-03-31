using System;
using NewLife.Agent;

namespace Test;

public class Program
{
    private static void Main(String[] args)
    {
        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1");

        var svc = new MyServices();
        svc.Main(args);
    }
}