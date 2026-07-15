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
}
