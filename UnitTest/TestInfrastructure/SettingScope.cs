using System.Reflection;
using NewLife.Agent;

namespace UnitTest;

/// <summary>全局配置测试作用域。快照 Setting.Current 全部可写属性，Dispose 时还原并落盘</summary>
/// <remarks>
/// Setting.Current 为进程级单例，测试修改后必须还原，否则污染后续测试。
/// 语言属于 NewLife.Setting 全局配置，同样纳入快照，避免切换语言后影响其他用例。
/// 用法：using var scope = new SettingScope(); 测试内即可自由修改 Setting.Current。
/// </remarks>
public sealed class SettingScope : IDisposable
{
    private readonly Dictionary<String, Object> _values = [];
    private readonly String _language = NewLife.Setting.Current.Language;

    /// <summary>实例化。快照当前全部可写配置属性</summary>
    public SettingScope()
    {
        var set = Setting.Current;
        foreach (var prop in typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;

            _values[prop.Name] = prop.GetValue(set, null);
        }
    }

    /// <summary>销毁。还原快照值并保存，确保内存与配置文件同时恢复</summary>
    public void Dispose()
    {
        var set = Setting.Current;
        foreach (var prop in typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || !_values.TryGetValue(prop.Name, out var value)) continue;

            prop.SetValue(set, value, null);
        }

        set.Save();

        // 语言属于 NewLife.Setting 全局配置，随配置一起还原
        var core = NewLife.Setting.Current;
        if (core.Language != _language)
        {
            core.Language = _language;
            core.Save();
        }
    }
}
