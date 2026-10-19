using NewLife.Log;

namespace NewLife.Agent;

/// <summary>Agent日志扩展</summary>
/// <remarks>
/// 带实例前缀的日志，前缀不参与本地化。先按日志语言翻译并格式化消息，再拼接前缀，
/// 避免日志实现直接拿包含动态前缀的整串查表而永远回退英文。
/// 与 NewLife.Core 的 <c>LogExtensions.WritePrefixed</c> 语义一致；net40 目标引用旧版 Core，这里提供等价实现。
/// </remarks>
public static class AgentLogExtensions
{
    /// <summary>写信息日志。前缀在翻译之后拼接</summary>
    /// <param name="log">日志对象，可为空</param>
    /// <param name="prefix">日志前缀，可为空</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    public static void WriteLogPrefixed(this ILog log, String prefix, String format, params Object[] args) => log.WriteLogPrefixed(LogLevel.Info, prefix, format, args);

    /// <summary>写日志。前缀在翻译之后拼接</summary>
    /// <param name="log">日志对象，可为空</param>
    /// <param name="level">日志等级</param>
    /// <param name="prefix">日志前缀，可为空</param>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    /// <example>
    /// <code>
    /// Log?.WriteLogPrefixed("[Session]", "Received {0} bytes", count);
    /// </code>
    /// </example>
    public static void WriteLogPrefixed(this ILog log, LogLevel level, String prefix, String format, params Object[] args)
    {
        if (log == null || !log.Enable || level < log.Level) return;

#if NET40
        // net40 引用旧版 Core，无本地化与 Logger.FormatMessage，直接格式化后拼接前缀
        var message = args == null || args.Length == 0 ? format : String.Format(format, args);
#else
        var message = Logger.FormatMessage(log, format, args);
#endif

        log.Write(level, prefix + message);
    }
}
