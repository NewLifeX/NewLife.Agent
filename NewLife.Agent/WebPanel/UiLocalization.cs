#if !NET40
using System.Globalization;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.Serialization;

namespace NewLife.Agent.WebPanel;

/// <summary>Web面板界面本地化</summary>
/// <remarks>
/// index.html 以英文为源文，同目录 i18n/{语言}.json 保存英文到目标语言的映射。
/// 服务端输出 HTML 前按当前语言做最长匹配替换，缺少语言词表时保持英文源文；
/// 英文即源文，直接返回，不再做替换。
/// </remarks>
public static class UiLocalization
{
    #region 属性
    /// <summary>本地化后的首页缓存。Key 为资源前缀|语言</summary>
    private static readonly ConcurrentDictionary<String, String> _pages = new(StringComparer.Ordinal);

    /// <summary>界面词典缓存。Key 为资源前缀|语言</summary>
    private static readonly ConcurrentDictionary<String, Dictionary<String, String>> _dictionaries = new(StringComparer.Ordinal);

    /// <summary>词典匹配表达式缓存。Key 为资源前缀|语言</summary>
    private static readonly ConcurrentDictionary<String, Regex> _regexes = new(StringComparer.Ordinal);
    #endregion

    #region 方法
    /// <summary>本地化页面</summary>
    /// <param name="html">英文源文页面</param>
    /// <param name="assembly">页面资源所在程序集</param>
    /// <param name="prefix">资源前缀，如 NewLife.Agent.WebPanel.wwwroot</param>
    /// <param name="culture">目标语言</param>
    /// <returns>本地化页面；英文或没有词表时返回源文</returns>
    public static String Localize(String html, Assembly assembly, String prefix, CultureInfo culture)
    {
        if (html.IsNullOrEmpty() || assembly == null || prefix.IsNullOrEmpty()) return html;

        var language = GetLanguage(culture);
        if (language == "en") return html;

        var key = $"{prefix}|{language}";
        if (_pages.TryGetValue(key, out var page)) return page;

        var map = GetDictionary(assembly, prefix, language);
        if (map.Count == 0) return html;

        // 单次扫描、最长匹配优先，避免译文再次参与替换
        var regex = _regexes.GetOrAdd(key, _ => BuildRegex(map));
        if (regex == null) return html;

        page = regex.Replace(html, m => map[m.Value]);

        // 同步根元素语言与书写方向，便于浏览器与辅助技术选择字体和排版
        page = Regex.Replace(page, "<html[^>]*>", $"<html lang=\"{language}\"{(IsRightToLeft(language) ? " dir=\"rtl\"" : "")}>", RegexOptions.IgnoreCase);
        _pages[key] = page;

        return page;
    }

    /// <summary>构建词典匹配表达式</summary>
    /// <param name="map">英文到目标语言的映射</param>
    /// <returns>按最长匹配优先排列的表达式</returns>
    private static Regex BuildRegex(Dictionary<String, String> map)
    {
        var keys = map.Where(e => !e.Key.IsNullOrEmpty() && !e.Value.IsNullOrEmpty())
            .Select(e => e.Key)
            .OrderByDescending(e => e.Length)
            .ToArray();
        if (keys.Length == 0) return null;

        return new Regex(String.Join("|", keys.Select(ToPattern)));
    }

    /// <summary>把词条转换为匹配模式。英文词条加词边界，避免命中脚本标识符中的同名片段</summary>
    /// <param name="key">英文源文</param>
    /// <returns>匹配模式</returns>
    private static String ToPattern(String key)
    {
        var pattern = Regex.Escape(key);
        if (IsAsciiWordChar(key[0])) pattern = "\\b" + pattern;
        if (IsAsciiWordChar(key[key.Length - 1])) pattern += "\\b";

        return pattern;
    }

    private static Boolean IsAsciiWordChar(Char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_';

    /// <summary>判断是否从右到左书写的语言</summary>
    /// <param name="language">语言代码</param>
    /// <returns>阿拉伯语、希伯来语、波斯语返回 true</returns>
    public static Boolean IsRightToLeft(String language) => language is "ar" or "he" or "fa";

    /// <summary>获取界面词典</summary>
    /// <param name="assembly">页面资源所在程序集</param>
    /// <param name="prefix">资源前缀</param>
    /// <param name="language">语言代码</param>
    /// <returns>英文到目标语言的映射；找不到词表时返回空字典</returns>
    public static Dictionary<String, String> GetDictionary(Assembly assembly, String prefix, String language)
    {
        var key = $"{prefix}|{language}";
        if (_dictionaries.TryGetValue(key, out var map)) return map;

        map = LoadDictionary(assembly, prefix, language);
        // 自定义面板没有自带词表时，退回主库词表；仍找不到则保持英文源文
        if (map.Count == 0 && language != "en" && assembly != typeof(UiLocalization).Assembly)
            map = LoadDictionary(typeof(UiLocalization).Assembly, "NewLife.Agent.WebPanel.wwwroot", language);

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
