namespace NewLife.Agent.Command;

/// <summary>
/// 命令处理接口
/// </summary>
internal interface ICommandHandler
{
    /// <summary>
    /// 命令
    /// </summary>
    String Cmd { get; set; }

    /// <summary>
    /// 命令描述
    /// </summary>
    String Description { get; set; }

    /// <summary>
    /// 快捷键
    /// </summary>
    Char? ShortcutKey { get; set; }

    /// <summary>
    /// 是否显示菜单
    /// </summary>
    /// <returns></returns>
    Boolean IsShowMenu();

    /// <summary>处理命令</summary>
    /// <param name="args"></param>
    /// <returns></returns>
    void Process(String[] args);
}