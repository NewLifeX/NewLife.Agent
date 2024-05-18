using System;
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
        }

        public override Boolean IsShowMenu()
        {
            return true;
        }

        public override void Process(String[] args)
        {
            Console.WriteLine("这是[测试自定义菜单]处理程序，开始输出 九九乘法表");
            for (var i = 1; i <= 9; i++)
            {
                for (var j = 1; j <= i; j++)
                {
                    Console.Write($"{j}x{i}={i * j}\t");
                }
                Console.WriteLine();
            }
        }
    }
}
