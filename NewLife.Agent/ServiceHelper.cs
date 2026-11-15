using System.Diagnostics;
using System.Text;

namespace NewLife.Agent;

/// <summary>服务助手</summary>
public static class ServiceHelper
{
    /// <summary>判断 pid 文件是否已陈旧</summary>
    /// <remarks>
    /// 进程异常退出后 pid 文件会残留，操作系统可能把同一 PID 分配给无关进程；
    /// 若目标进程的启动时间明显晚于 pid 文件写入时间，说明 PID 已被复用，不能再当作本服务进程。
    /// </remarks>
    /// <param name="process">按 pid 找到的进程</param>
    /// <param name="pidWriteTime">pid 文件最后写入时间</param>
    /// <returns>是否已陈旧（PID 被复用）</returns>
    internal static Boolean IsStalePid(Process process, DateTime pidWriteTime)
    {
        if (process == null) return false;

        try
        {
            // 容差 2 秒，避开文件时间精度与进程启动时间的差异
            return process.StartTime > pidWriteTime.AddSeconds(2);
        }
        catch
        {
            // 权限不足取不到启动时间时不做判断，保持原有行为
            return false;
        }
    }
    /// <summary>是否运行时框架主程序</summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否为运行时主程序（dotnet/java）</returns>
    public static Boolean IsRuntime(this String fileName) => fileName.EndsWithIgnoreCase("dotnet", "dotnet.exe", "testhost.exe", "java", "java.exe");

    /// <summary>从文件名中分析工作目录</summary>
    /// <remarks>
    /// 分析命令行字符串，从运行时主程序（dotnet/java）或目标程序路径中提取工作目录。
    /// 支持带空格路径（引号包裹的场景由调用方保证已去除外层引号）。
    /// </remarks>
    /// <param name="fileName">可执行文件名或完整路径</param>
    /// <param name="arguments">命令行参数</param>
    /// <returns>工作目录绝对路径，无法确定时返回 null</returns>
    public static String GetWorkingDirectory(this String fileName, String arguments)
    {
        if (fileName.IsNullOrEmpty()) return null;

        var dll = GetTargetFile(fileName, arguments);
        if (dll.IsNullOrEmpty()) return null;

        // 从完整路径中提取目录部分
        var p = dll.LastIndexOfAny(['/', '\\']);
        if (p > 0)
        {
            // 处理路径尾部的引号
            var dir = dll.Substring(0, p);
            if (dir.Length > 1 && dir[0] == '"') dir = dir.Substring(1);
            return dir;
        }

        return ".".GetFullPath();
    }

    /// <summary>从命令行中解析目标程序文件路径</summary>
    /// <remarks>
    /// 处理两类场景：
    /// 1. fileName 是运行时主程序（dotnet/java），从 arguments 或 fileName 的其余部分中提取第一个非参数路径
    /// 2. fileName 就是目标程序自身
    /// </remarks>
    /// <param name="fileName">可执行文件名</param>
    /// <param name="arguments">命令行参数</param>
    /// <returns>目标程序文件路径，无法确定时返回 null</returns>
    private static String GetTargetFile(String fileName, String arguments)
    {
        // 从 fileName 分离程序路径和参数
        var parts = SplitCommandLine(fileName);
        var exe = parts[0];
        var embeddedArgs = parts.Length > 1 ? parts[1] : null;

        if (exe.IsNullOrEmpty()) return null;

        // 如果是运行时主程序，从 arguments 中提取 dll 路径
        if (exe.IsRuntime())
        {
            // 优先使用传入的 arguments；若为空或无有效路径，回退到 embeddedArgs（fileName 中可执行文件后的部分）
            var args = !arguments.IsNullOrEmpty() ? arguments : null;
            if (args.IsNullOrEmpty())
                args = embeddedArgs;

            if (args.IsNullOrEmpty()) return null;

            // 解析第一个非参数令牌
            var tokens = ParseTokens(args);
            foreach (var token in tokens)
            {
                // 跳过 "-" 开头的参数
                if (token.StartsWith("-")) continue;
                return token;
            }

            // 如果 arguments 没有非参数令牌，尝试从 embeddedArgs 查找
            if (!arguments.IsNullOrEmpty() && !embeddedArgs.IsNullOrEmpty())
            {
                var tokens2 = ParseTokens(embeddedArgs);
                foreach (var token2 in tokens2)
                {
                    if (!token2.StartsWith("-")) return token2;
                }
            }

            return null;
        }

        return exe;
    }

    /// <summary>将命令行字符串拆分为程序路径和其余参数</summary>
    /// <remarks>
    /// 支持两类格式：
    /// 1. 引号包裹路径："C:\Program Files\app.exe" --arg
    /// 2. 普通路径：app.exe --arg
    /// </remarks>
    /// <param name="commandLine">原始命令行字符串</param>
    /// <returns>[程序路径, 其余参数]，无法解析时长度为0的数组</returns>
    public static String[] SplitCommandLine(String commandLine)
    {
        if (commandLine.IsNullOrEmpty()) return [];

        commandLine = commandLine.Trim();
        if (commandLine.Length == 0) return [];

        // 处理引号包裹的路径
        if (commandLine[0] == '"')
        {
            var end = commandLine.IndexOf('"', 1);
            if (end > 0)
            {
                var exe = commandLine.Substring(1, end - 1);
                var args = commandLine.Length > end + 1 ? commandLine.Substring(end + 1).Trim() : "";
                return [exe, args];
            }
        }

        // 按空格分割取第一个
        var spaceIdx = commandLine.IndexOf(' ');
        if (spaceIdx > 0)
        {
            var exe2 = commandLine.Substring(0, spaceIdx);
            var args2 = commandLine.Substring(spaceIdx + 1).Trim();
            return [exe2, args2];
        }

        return [commandLine];
    }

    /// <summary>解析命令行参数字符串为令牌列表（尊重双引号）</summary>
    /// <param name="arguments">参数字符串</param>
    /// <returns>令牌列表，无法解析时返回空数组</returns>
    public static String[] ParseArguments(String arguments)
    {
        var list = ParseTokens(arguments);

        return [.. list];
    }

    /// <summary>判断字符串是否为“可执行文件 + 内嵌参数”的复合命令行，而不是单个路径</summary>
    /// <remarks>
    /// 单个路径允许包含空格（如 /opt/my app/agent），不能一律按空格拆分；
    /// 只有以下两种情况视为复合命令行：① 以引号开头（"C:\Program Files\app.exe" -s）；
    /// ② 首个 token 不含路径分隔符（dotnet app.dll）或该 token 是已存在的文件。
    /// </remarks>
    /// <param name="value">待判断字符串</param>
    /// <returns>是否为复合命令行</returns>
    public static Boolean IsCompositeCommandLine(String value)
    {
        if (value.IsNullOrEmpty()) return false;

        value = value.Trim();
        if (value.Length == 0) return false;
        if (value[0] == '"') return true;

        var idx = value.IndexOf(' ');
        if (idx <= 0) return false;

        var exe = value.Substring(0, idx);
        var rest = value.Substring(idx + 1).TrimStart();

        // 首个 token 不含路径分隔符（dotnet）、文件已存在，或明显是可执行文件（C:\app.exe -s），
        // 都按复合命令行处理；单个含空格路径的首个 token 通常是 "C:\Program" 或 "/opt/my"，不会被误判。
        // 剩余部分以 - / \ 开头（如 dotnet /root/app.dll）同样说明是命令行而不是单个路径
        return exe.IndexOf('/') < 0 && exe.IndexOf('\\') < 0
            || File.Exists(exe)
            || exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || exe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            || exe.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
            || exe.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)
            || rest.StartsWith("-", StringComparison.Ordinal)
            || rest.StartsWith("/", StringComparison.Ordinal)
            || rest.StartsWith("\\", StringComparison.Ordinal);
    }

    /// <summary>解析文件名与内嵌参数：单个路径整体保留，复合命令行才拆分</summary>
    /// <param name="fileName">可执行文件路径或复合命令行</param>
    /// <returns>[可执行文件, 内嵌参数]</returns>
    static String[] SplitFileName(String fileName) => IsCompositeCommandLine(fileName) ? SplitCommandLine(fileName) : [fileName];

    /// <summary>为进程参数加引号（含空白或引号时用双引号包裹）</summary>
    /// <param name="value">原始参数；已带外层双引号时保持不变，避免二次包裹</param>
    /// <returns>可直接拼进 ProcessStartInfo.Arguments 的参数</returns>
    public static String QuoteArgument(String value)
    {
        if (value.IsNullOrEmpty()) return value;
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') return value;

        return value.IndexOfAny([' ', '\t', '\r', '\n', '"']) >= 0 ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }

    /// <summary>给单个路径加引号；已是复合命令行（含引号）时原样返回，避免把整串当成一个路径</summary>
    /// <param name="value">单个路径或复合命令行</param>
    /// <returns>可直接写入注册表/配置的路径</returns>
    public static String QuotePath(String value)
    {
        if (value.IsNullOrEmpty()) return value;
        if (value.IndexOf('"') >= 0) return value;
        if (IsCompositeCommandLine(value)) return value;

        return value.IndexOfAny([' ', '\t', '\r', '\n']) >= 0 ? $"\"{value}\"" : value;
    }

    /// <summary>为 shell 脚本加单引号，避免空格与元字符改变命令含义</summary>
    /// <param name="value">原始值；已带外层双引号时先去掉，再统一转成单引号</param>
    /// <returns>可直接嵌入 sh 脚本的字面量；空值原样返回</returns>
    public static String QuoteShell(String value)
    {
        if (value.IsNullOrEmpty()) return value;

        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') value = value.Substring(1, value.Length - 2);

        return "'" + value.Replace("'", "'\\''") + "'";
    }

    /// <summary>构建 sh 脚本中的命令：只给可执行文件加引号，复合命令行中的内嵌参数与附加参数原样保留</summary>
    /// <param name="fileName">可执行文件路径，或 dotnet + dll 形式的复合命令行</param>
    /// <param name="arguments">附加参数</param>
    /// <returns>可直接写入 init 脚本的命令文本</returns>
    public static String BuildShellCommand(String fileName, String arguments)
    {
        var parts = SplitFileName(fileName);
        var sb = new StringBuilder();
        if (parts.Length > 0) sb.Append(QuoteShell(parts[0]));
        if (parts.Length > 1 && !parts[1].IsNullOrEmpty()) sb.Append(' ').Append(parts[1].Trim());
        if (!arguments.IsNullOrEmpty()) sb.Append(' ').Append(arguments.Trim());

        return sb.ToString();
    }

    /// <summary>解析命令行参数字符串为令牌列表</summary>
    /// <param name="arguments">参数字符串</param>
    /// <returns>令牌列表</returns>
    private static List<String> ParseTokens(String arguments)
    {
        var tokens = new List<String>();
        if (arguments.IsNullOrEmpty()) return tokens;

        var i = 0;
        while (i < arguments.Length)
        {
            // 跳过空白
            var c = arguments[i];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                i++;
                continue;
            }

            // 引号包裹的令牌
            if (c == '"')
            {
                var start = i + 1;
                var end = arguments.IndexOf('"', start);
                if (end < 0) end = arguments.Length;
                tokens.Add(arguments.Substring(start, end - start));
                i = end + 1;
            }
            else
            {
                // 非引号令牌，直到下一个空白
                var start = i;
                while (i < arguments.Length)
                {
                    var c2 = arguments[i];
                    if (c2 == ' ' || c2 == '\t' || c2 == '\r' || c2 == '\n') break;
                    i++;
                }
                tokens.Add(arguments.Substring(start, i - start));
            }
        }

        return tokens;
    }
}
