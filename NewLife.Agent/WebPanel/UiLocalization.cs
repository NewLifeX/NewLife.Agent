#if !NET40
using System.Globalization;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using NewLife.Serialization;

namespace NewLife.Agent.WebPanel;

/// <summary>Web面板界面本地化</summary>
/// <remarks>
/// index.html 以中文为源文，同目录 i18n/{语言}.json 保存中文到目标语言的映射。
/// 服务端输出 HTML 前按当前语言做最长匹配替换，缺少语言词表时回退英文；
/// 简体中文即源文，直接返回，不再做替换。
/// </remarks>
public static class UiLocalization
{
    #region 属性
    /// <summary>本地化后的首页缓存。Key 为资源前缀|语言</summary>
    private static readonly ConcurrentDictionary<String, String> _pages = new(StringComparer.Ordinal);

    /// <summary>界面词典缓存。Key 为资源前缀|语言</summary>
    private static readonly ConcurrentDictionary<String, Dictionary<String, String>> _dictionaries = new(StringComparer.Ordinal);
    #endregion

    #region 方法
    /// <summary>本地化页面</summary>
    /// <param name="html">中文源文页面</param>
    /// <param name="assembly">页面资源所在程序集</param>
    /// <param name="prefix">资源前缀，如 NewLife.Agent.WebPanel.wwwroot</param>
    /// <param name="culture">目标语言</param>
    /// <returns>本地化页面；简体中文或没有词表时返回源文</returns>
    public static String Localize(String html, Assembly assembly, String prefix, CultureInfo culture)
    {
        if (html.IsNullOrEmpty() || assembly == null || prefix.IsNullOrEmpty()) return html;

        var language = GetLanguage(culture);
        if (language == "zh-Hans") return html;

        var key = $"{prefix}|{language}";
        if (_pages.TryGetValue(key, out var page)) return page;

        var map = GetDictionary(assembly, prefix, language);
        if (map.Count == 0) return html;

        // 最长匹配优先，避免“启动”先替换掉“启动服务”这类包含关系
        foreach (var item in map.OrderByDescending(e => e.Key.Length))
        {
            if (item.Key.Length == 0 || item.Value.IsNullOrEmpty()) continue;

            page ??= html;
            page = page.Replace(item.Key, item.Value);
        }

        page ??= html;
        _pages[key] = page;

        return page;
    }

    /// <summary>获取界面词典</summary>
    /// <param name="assembly">页面资源所在程序集</param>
    /// <param name="prefix">资源前缀</param>
    /// <param name="language">语言代码</param>
    /// <returns>中文到目标语言的映射；找不到词表时返回空字典</returns>
    public static Dictionary<String, String> GetDictionary(Assembly assembly, String prefix, String language)
    {
        var key = $"{prefix}|{language}";
        if (_dictionaries.TryGetValue(key, out var map)) return map;

        map = LoadDictionary(assembly, prefix, language);
        if (map.Count == 0 && language != "en") map = LoadDictionary(assembly, prefix, "en");

        _dictionaries[key] = map;

        return map;
    }

    /// <summary>从嵌入资源加载词典</summary>
    /// <param name="assembly">页面资源所在程序集</param>
    /// <param name="prefix">资源前缀</param>
    /// <param name="language">语言代码</param>
    /// <returns>词典</returns>
    public static Dictionary<String, String> LoadDictionary(Assembly assembly, String prefix, String language)
    {
        var map = new Dictionary<String, String>(StringComparer.Ordinal);

        using var stream = assembly.GetManifestResourceStream($"{prefix}.i18n.{language}.json");
        if (stream == null) return map;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = reader.ReadToEnd();
        if (json.IsNullOrEmpty()) return map;

        var dic = JsonParser.Decode(json);
        if (dic == null) return map;

        foreach (var item in dic)
        {
            if (item.Key.IsNullOrEmpty() || item.Value == null) continue;

            map[item.Key] = item.Value + "";
        }

        return map;
    }

    /// <summary>获取语言代码</summary>
    /// <param name="culture">目标语言</param>
    /// <returns>与词表文件一致的语言代码</returns>
    public static String GetLanguage(CultureInfo culture)
    {
        var name = culture?.Name;
        if (name.IsNullOrEmpty()) return "en";

        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var isHant = name.Contains("Hant") ||
                name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);
            return isHant ? "zh-Hant" : "zh-Hans";
        }

        var index = name.IndexOf('-');
        return (index > 0 ? name[..index] : name).ToLowerInvariant();
    }
    #endregion
}
#endif
