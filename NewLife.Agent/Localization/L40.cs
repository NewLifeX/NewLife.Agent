#if NET40
using System;

namespace NewLife;

/// <summary>net40 目标的本地化占位实现</summary>
/// <remarks>该目标引用旧版 NewLife.Core，不包含 Localization；保留英文源文即可。</remarks>
internal static class L
{
    /// <summary>返回原文</summary>
    /// <param name="template">英文原文模板</param>
    /// <returns>原文</returns>
    public static String T(String template) => template;

    /// <summary>格式化原文</summary>
    /// <param name="template">英文原文模板</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化结果</returns>
    public static String T(String template, params Object[] args) => args == null || args.Length == 0 ? template : String.Format(template, args);
}
#endif
