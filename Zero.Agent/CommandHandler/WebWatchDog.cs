using System;
using System.Threading;
using NewLife.Agent;
using NewLife.Agent.CommandHandler;

namespace Zero.Agent.CommandHandler
{
    /// <summary>
    /// WEB服务看门狗命令处理器
    /// </summary>
    public class WebWatchDog : WatchDog
    {
        public override String Description { get; set; } = "WEB看门狗保护服务";

        public WebWatchDog(ServiceBase service) : base(service)
        {
            //本功能主要是用于演示如何覆盖默认的看门狗保护服务，实现自己的看门狗逻辑，其他需要覆盖基础命令处理器也可以参照这个类实现
        }

        public override void Process(String[] args)
        {
            Console.WriteLine("这是[WEB看门狗保护服务]处理程序，开始检查WEB服务是否可以正常访问");
            CheckWatchDog();
        }

        public override void CheckWatchDog()
        {
            //测试访问网址，如果访问不是200状态，则重启服务
            Console.WriteLine("这里只是模拟，实际应该是访问网站，检查响应状态码");

            Thread.Sleep(2000);

            Console.WriteLine("模拟检查完成，如果状态码不是200，则重启服务");
        }

    }
}
