using System;
using System.Linq;
using System.Threading;
using NewLife.Agent;
using NewLife.Agent.Command;

namespace Zero.Agent.CommandHandler;

public class TransferCall : BaseCommandHandler
{
    public TransferCall(ServiceBase service) : base(service)
    {
        //本功能主要是用于演示如何转调用其他命令处理器，并且可以传入自定义参数
        Cmd = "-TransferCall";
        Description = "测试转调用其他命令";
        ShortcutKey = 'w';
    }

    public override Boolean IsShowMenu() => true;

    public override void Process(String[] args)
    {
        Console.WriteLine("这是[测试转调用其他命令]处理程序");
        Thread.Sleep(1000);
        Console.WriteLine("以下开始转调用 [测试自定义菜单]，还可以传入自定义参数");
        Service.Command.Handle("-test", args.Concat(["-showme"]).ToArray());
        Thread.Sleep(1000);
        Console.WriteLine("以下开始转调用 [显示状态]");
        Service.Command.Handle(CommandConst.ShowStatus, args);
        Console.WriteLine("转调用 [测试自定义菜单] 已执行完毕。");
    }
}
