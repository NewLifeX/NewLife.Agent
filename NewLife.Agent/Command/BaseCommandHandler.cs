namespace NewLife.Agent.Command;

/// <summary>
/// 命令处理基类
/// </summary>
/// <remarks>
/// 安装并启动服务
/// </remarks>
/// <param name="service"></param>
public abstract class BaseCommandHandler(ServiceBase service) : ICommandHandler
{
    /// <summary>
    /// 服务
    /// </summary>
    protected ServiceBase Service { get; } = service;

    /// <summary>
    /// 命令（命令为唯一标识，子类如果使用相同命令，父类将会被覆盖）
    /// </summary>
    public String Cmd { get; set; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public String Description { get; set; }

    /// <summary>
    /// 快捷键
    /// </summary>
    public Char? ShortcutKey { get; set; }

    /// <summary>
    /// 是否显示菜单
    /// </summary>
    /// <returns></returns>
    public virtual Boolean IsShowMenu() => ShortcutKey != null;

    /// <summary>处理命令</summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public abstract void Process(String[] args);
}