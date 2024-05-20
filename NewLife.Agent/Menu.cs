namespace NewLife.Agent;

/// <summary>
/// 菜单信息
/// </summary>
/// <remarks>
/// 实例化
/// </remarks>
/// <param name="key"></param>
/// <param name="name"></param>
/// <param name="cmd"></param>
/// <param name="callback"></param>
public class Menu(Char key, String name, String cmd, Action callback) : IComparable<Menu>
{
    /// <summary>按键</summary>
    public Char Key { get; set; } = key;

    /// <summary>名称</summary>
    public String Name { get; set; } = name;

    /// <summary>命令</summary>
    public String Cmd { get; set; } = cmd;

    /// <summary>处理函数</summary>
    public Action Callback { get; set; } = callback;

    /// <summary>比较</summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public Int32 CompareTo(Menu other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;

        var keyComparison = Key.CompareTo(other.Key);
        if (keyComparison != 0) return keyComparison;

        return String.Compare(Cmd, other.Cmd, StringComparison.Ordinal);
    }
}