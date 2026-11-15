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

    /// <summary>命令（唯一标识）。继承具体处理器并复用同一命令时覆盖父类；两个直接继承 BaseCommandHandler 的处理器使用同一命令视为冲突并抛异常</summary>
    public String Cmd { get; set; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public String Description { get; set; }

    /// <summary>
    /// 快捷键
    /// </summary>
    public Char? ShortcutKey { get; set; }

    /// <summary>是否显示菜单</summary>
    /// <returns>是否显示菜单</returns>
    public virtual Boolean IsShowMenu() => ShortcutKey != null;

    /// <summary>处理命令</summary>
    /// <param name="args">命令行参数</param>
    public abstract void Process(String[] args);
}