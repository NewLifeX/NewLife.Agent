using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>ServiceHelper 单元测试</summary>
/// <remarks>验证 GetWorkingDirectory 路径解析逻辑</remarks>
public class ServiceHelperTests
{
    [Fact]
    [DisplayName("GetWorkingDirectory_运行时dotnet_从参数提取工作目录")]
    public void GetWorkingDirectory_DotnetRuntime_ExtractsFromArgs()
    {
        var dir = "/usr/share/dotnet/dotnet".GetWorkingDirectory("/root/agent/StarAgent.dll -s");
        Assert.Equal("/root/agent", dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_运行时dotnet_参数在文件名中")]
    public void GetWorkingDirectory_DotnetRuntime_ArgsInFileName()
    {
        var dir = "/usr/share/dotnet/dotnet /root/agent/StarAgent.dll".GetWorkingDirectory("-s");
        Assert.Equal("/root/agent", dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_运行时dotnet_无路径参数返回null")]
    public void GetWorkingDirectory_DotnetRuntime_NoPathReturnsNull()
    {
        var dir = "/usr/share/dotnet/dotnet".GetWorkingDirectory("-s");
        Assert.Null(dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_普通可执行文件_返回目录")]
    public void GetWorkingDirectory_RegularExe_ReturnsDirectory()
    {
        var dir = "/usr/local/bin/myapp".GetWorkingDirectory(null);
        Assert.Equal("/usr/local/bin", dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_带空格路径_正确解析")]
    public void GetWorkingDirectory_PathWithSpaces_ParsesCorrectly()
    {
        // 引号包裹的路径
        var dir = "\"/Program Files/MyApp/myapp.exe\"".GetWorkingDirectory(null);
        Assert.Equal("/Program Files/MyApp", dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_dotnet带空格dll路径_正确解析")]
    public void GetWorkingDirectory_DotnetWithSpaces_ParsesCorrectly()
    {
        // dotnet 运行时 + 带空格的 dll 路径
        var dir = "/usr/share/dotnet/dotnet".GetWorkingDirectory("\"/opt/My App/Service.dll\" -s");
        Assert.Equal("/opt/My App", dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_无路径信息_返回当前目录")]
    public void GetWorkingDirectory_NoPath_ReturnsCurrentDir()
    {
        var dir = "myapp".GetWorkingDirectory(null);
        Assert.NotNull(dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_java运行时_从参数提取工作目录")]
    public void GetWorkingDirectory_JavaRuntime_ExtractsFromArgs()
    {
        var dir = "/usr/bin/java".GetWorkingDirectory("-jar /opt/app/myapp.jar --port 8080");
        Assert.Equal("/opt/app", dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_空字符串_返回null")]
    public void GetWorkingDirectory_EmptyString_ReturnsNull()
    {
        var dir = "".GetWorkingDirectory(null);
        Assert.Null(dir);
    }

    [Fact]
    [DisplayName("GetWorkingDirectory_当前目录_返回null")]
    public void GetWorkingDirectory_NoSeparator_ReturnsCurrentDir()
    {
        var dir = "myapp".GetWorkingDirectory(null);
        Assert.NotNull(dir);
    }

    [Fact]
    [DisplayName("IsRuntime_dotnet和java_返回true")]
    public void IsRuntime_DotnetAndJava_ReturnsTrue()
    {
        Assert.True("dotnet".IsRuntime());
        Assert.True("dotnet.exe".IsRuntime());
        Assert.True("java".IsRuntime());
        Assert.True("java.exe".IsRuntime());
        Assert.True("testhost.exe".IsRuntime());
    }

    [Fact]
    [DisplayName("IsRuntime_其他文件_返回false")]
    public void IsRuntime_OtherExe_ReturnsFalse()
    {
        Assert.False("myapp".IsRuntime());
        Assert.False("myapp.exe".IsRuntime());
        Assert.False("".IsRuntime());
        Assert.False(((String)null!).IsRuntime());
    }

    #region SplitCommandLine
    [Fact]
    [DisplayName("SplitCommandLine_引号包裹路径_正确拆分")]
    public void SplitCommandLine_QuotedPath_Splits()
    {
        var parts = ServiceHelper.SplitCommandLine("\"C:\\Program Files\\app.exe\" --arg");
        Assert.Equal(2, parts.Length);
        Assert.Equal("C:\\Program Files\\app.exe", parts[0]);
        Assert.Equal("--arg", parts[1]);
    }

    [Fact]
    [DisplayName("SplitCommandLine_普通路径_正确拆分")]
    public void SplitCommandLine_PlainPath_Splits()
    {
        var parts = ServiceHelper.SplitCommandLine("app.exe --arg");
        Assert.Equal(2, parts.Length);
        Assert.Equal("app.exe", parts[0]);
        Assert.Equal("--arg", parts[1]);
    }

    [Fact]
    [DisplayName("SplitCommandLine_仅路径_单元素")]
    public void SplitCommandLine_OnlyPath_Single()
    {
        var parts = ServiceHelper.SplitCommandLine("app.exe");
        Assert.Single(parts);
        Assert.Equal("app.exe", parts[0]);
    }

    [Fact]
    [DisplayName("SplitCommandLine_带空格无引号路径_按首个空格拆分")]
    public void SplitCommandLine_PlainPathWithSpaces_Splits()
    {
        var parts = ServiceHelper.SplitCommandLine("C:\\Program Files\\app.exe --arg");
        Assert.Equal(2, parts.Length);
        Assert.Equal("C:\\Program", parts[0]);
        Assert.Equal("Files\\app.exe --arg", parts[1]);
    }

    [Fact]
    [DisplayName("SplitCommandLine_空或null_返回空数组")]
    public void SplitCommandLine_Empty_ReturnsEmpty()
    {
        Assert.Empty(ServiceHelper.SplitCommandLine(""));
        Assert.Empty(ServiceHelper.SplitCommandLine(null!));
    }

    [Fact]
    [DisplayName("SplitCommandLine_引号路径无参数_返回路径与空参数")]
    public void SplitCommandLine_QuotedPathOnly_Splits()
    {
        // 实现语义：始终返回 [程序路径, 参数]，无参数时第二个元素为空串
        var parts = ServiceHelper.SplitCommandLine("\"C:\\Program Files\\app.exe\"");
        Assert.Equal(2, parts.Length);
        Assert.Equal("C:\\Program Files\\app.exe", parts[0]);
        Assert.Equal("", parts[1]);
    }
    #endregion
}
