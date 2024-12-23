namespace NewLife.Agent;

/// <summary>服务助手</summary>
public static class ServiceHelper
{
    /// <summary>是否运行时框架主程序</summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static Boolean IsRuntime(this String fileName) => fileName.EndsWithIgnoreCase("dotnet", "dotnet.exe", "testhost.exe", "java", "java.exe");

    /// <summary>从文件名中分析工作目录</summary>
    /// <param name="fileName"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static String GetWorkingDirectory(this String fileName, String arguments)
    {
        var dll = "";
        var ss = fileName.Split(" ");
        if (ss.Length >= 2 && ss[0].IsRuntime())
        {
            dll = ss[1];
        }
        else if (!arguments.IsNullOrEmpty() && fileName.IsRuntime())
        {
            ss = arguments.Split(" ");
            dll = ss[0];
        }
        if (!dll.IsNullOrEmpty())
        {
            var p = dll.LastIndexOfAny(['/', '\\']);
            if (p > 0)
                return dll.Substring(0, p);
            else
                return ".".GetFullPath();
        }

        return null;
    }
}
