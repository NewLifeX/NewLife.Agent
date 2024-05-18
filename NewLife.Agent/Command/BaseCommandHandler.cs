
namespace NewLife.Agent.Command;

/// <summary>
/// 命令处理基类
/// </summary>
public abstract class BaseCommandHandler : ICommandHandler
{
    /// <summary>
    /// 服务
    /// </summary>
    protected ServiceBase Service { get; }

    /// <summary>
    /// 命令（命令为唯一标识，子类如果使用相同命令，父类将会被覆盖）
    /// </summary>
    public abstract String Cmd { get; set; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public abstract String Description { get; set; }

    /// <summary>
    /// 快捷键
    /// </summary>
    public abstract Char? ShortcutKey { get; set; }

    /// <summary>
    /// 安装并启动服务
    /// </summary>
    /// <param name="service"></param>
    public BaseCommandHandler(ServiceBase service)
    {
        Service = service;
    }

    /// <summary>
    /// 是否显示菜单
    /// </summary>
    /// <returns></returns>
    public virtual Boolean IsShowMenu()
    {
        return ShortcutKey != null;
    }

    /// <summary>处理命令</summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public abstract void Process(String[] args);
}