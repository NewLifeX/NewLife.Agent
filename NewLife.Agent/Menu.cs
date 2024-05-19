namespace NewLife.Agent;

/// <summary>
/// 菜单信息
/// </summary>
public class Menu : IComparable<Menu>
{
    /// <summary>按键</summary>
    public Char Key { get; set; }

    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>命令</summary>
    public String Cmd { get; set; }

    /// <summary>
    /// 实例化
    /// </summary>
    /// <param name="key"></param>
    /// <param name="name"></param>
    /// <param name="cmd"></param>
    public Menu(Char key, String name, String cmd)
    {
        Key = key;
        Name = name;
        Cmd = cmd;
    }

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