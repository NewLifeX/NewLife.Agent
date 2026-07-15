namespace NewLife.Agent;

/// <summary>服务助手</summary>
public static class ServiceHelper
{
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
    /// <param name="commandLine">原始命令行字符串</param>
    /// <returns>[程序路径, 其余参数]，无法解析时长度为0的数组</returns>
    private static String[] SplitCommandLine(String commandLine)
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
