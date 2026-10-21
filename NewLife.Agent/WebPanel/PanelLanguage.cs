#if !NET40
using System.Globalization;
using NewLife;

namespace NewLife.Agent.WebPanel;

/// <summary>面板语言状态</summary>
/// <remarks>
/// 单一事实源为 NewLife.Setting.Language：为空表示跟随浏览器自动识别，非空表示固定语言。
/// Selected 为配置值（归一化后），Effective 为当前请求实际生效的语言，二者语义不同。
/// </remarks>
public static class PanelLanguage
{
    /// <summary>配置选中的语言。空表示自动识别</summary>
    public static String Selected => Localization.NormalizeCulture(Localization.ForcedLanguage) ?? "";

    /// <summary>当前请求实际生效的语言</summary>
    public static String Effective
    {
        get
        {
            var culture = Localization.Culture ?? CultureInfo.CurrentUICulture;
            return Localization.NormalizeCulture(culture.Name) ?? "";
        }
    }

    /// <summary>是否已固定语言</summary>
    public static Boolean Forced => !Selected.IsNullOrEmpty();
}
#endif
