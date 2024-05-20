using System;
using System.Linq;
using System.Threading;
using NewLife.Agent;
using NewLife.Agent.Command;

namespace Zero.Agent
{
    public class TestCommandHandler : BaseCommandHandler
    {
        public override String Cmd { get; set; } = "-test";
        public override String Description { get; set; } = "测试自定义菜单";
        public override Char? ShortcutKey { get; set; } = 't';

        public TestCommandHandler(ServiceBase service) : base(service)
        {
            //本功能主要是用于演示如何定义自己的命令处理器，所有命令处理器必需指定Cmd和Description，
            //ShortcutKey用于定义快捷键，如果不需要快捷键，可以不指定，将不会显示在菜单中
        }

        public override Boolean IsShowMenu()
        {
            //是否显示在菜单中，默认情况下，不需要重写，基类中自动判断是否有快捷键来决定是否显示
            //另外，还可以根据服务的运行状态来决定是否显示，比如：
            //只有服务已安装时才显示： return Service.Host.IsInstalled(Service.ServiceName);
            //只有服务已在运行中时才显示： return Service.Host.IsRunning(Service.ServiceName);

            return base.IsShowMenu();
        }

        public override void Process(String[] args)
        {
            Console.WriteLine("这是[测试自定义菜单]处理程序，开始输出 九九乘法表");
            Thread.Sleep(1000);
            for (var i = 1; i <= 9; i++)
            {
                for (var j = 1; j <= i; j++)
                {
                    Console.Write($"{j}x{i}={i * j}\t");
                }
                Console.WriteLine();
                Thread.Sleep(200);
            }

            if (args.Contains("-showme"))
            {
                Console.WriteLine("这是[测试自定义菜单]处理程序，显示了自定义参数 -showme");
            }
        }
    }
}
