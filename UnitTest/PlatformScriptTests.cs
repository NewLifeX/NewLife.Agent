using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>Procd/RcInit 脚本生成单元测试</summary>
/// <remarks>验证 BuildScript() 输出的 shell 脚本内容，不依赖实际 Linux 环境</remarks>
public class PlatformScriptTests
{
    #region Procd 脚本

    [Fact]
    [DisplayName("Procd_无rc.common_生成标准shebang")]
    public void Procd_NoRcCommon_StandardShebang()
    {
        var result = Procd.BuildScript("/app/agent", "StarAgent.dll -s", "星尘代理", false);
        Assert.StartsWith("#!/bin/sh", result);
        Assert.DoesNotContain("/etc/rc.common", result);
    }

    [Fact]
    [DisplayName("Procd_有rc.common_生成rc.common shebang")]
    public void Procd_HasRcCommon_CommonShebang()
    {
        var result = Procd.BuildScript("/app/agent", "StarAgent.dll -s", "星尘代理", true);
        Assert.StartsWith("#!/bin/sh /etc/rc.common", result);
    }

    [Fact]
    [DisplayName("Procd_基本参数_包含关键结构")]
    public void Procd_BasicArgs_ContainsKeyStructure()
    {
        var result = Procd.BuildScript("/app/dotnet", "agent.dll -s", "测试服务", false);

        Assert.Contains("USE_PROCD=1", result);
        Assert.Contains("nohup /app/dotnet agent.dll -s >/dev/null 2>&1 &", result);
        Assert.Contains("start_service()", result);
        Assert.Contains("stop_service()", result);
        Assert.Contains("reload_service()", result);
        Assert.Contains("help()", result);
    }

    [Fact]
    [DisplayName("Procd_带-s参数_stop/reload自动移除-s")]
    public void Procd_WithSArg_StopReloadRemovesS()
    {
        var result = Procd.BuildScript("/app/app.exe", "app.dll -s", "测试", false);

        // start 保留 -s
        Assert.Contains("nohup /app/app.exe app.dll -s >/dev/null 2>&1 &", result);
        // stop/reload 移除 -s 并加上 -stop/-restart
        Assert.Contains("/app/app.exe app.dll -stop", result);
        Assert.Contains("/app/app.exe app.dll -restart", result);
    }

    [Fact]
    [DisplayName("Procd_无参数_脚本格式正确")]
    public void Procd_NoArgs_WellFormed()
    {
        var result = Procd.BuildScript("/app/myservice", "", "无参数服务", false);

        Assert.Contains("nohup /app/myservice  >/dev/null 2>&1 &", result);
        Assert.Contains("START=50", result);
        Assert.Contains("STOP=50", result);
    }

    #endregion

    #region RcInit 脚本

    [Fact]
    [DisplayName("RcInit_脚本头正确")]
    public void RcInit_Header_Correct()
    {
        var result = RcInit.BuildScript("/app/agent", "agent.dll -s", "星尘代理");

        Assert.StartsWith("#!/bin/bash", result);
        Assert.Contains("# chkconfig: 2345 10 90", result);
        Assert.Contains("# description: 星尘代理", result);
    }

    [Fact]
    [DisplayName("RcInit_包含case结构")]
    public void RcInit_ContainsCaseStructure()
    {
        var result = RcInit.BuildScript("/app/dotnet", "app.dll -s", "测试服务");

        Assert.Contains("case \"$1\" in", result);
        Assert.Contains("start)", result);
        Assert.Contains("stop)", result);
        Assert.Contains("restart)", result);
        Assert.Contains("esac", result);
        Assert.Contains("exit $?", result);
    }

    [Fact]
    [DisplayName("RcInit_带-s参数_stop自动移除-s")]
    public void RcInit_WithSArg_StopRemovesS()
    {
        var result = RcInit.BuildScript("/app/app.exe", "app.dll -s", "测试");

        // start 保留 -s
        Assert.Contains("nohup /app/app.exe app.dll -s >/dev/null 2>&1 &", result);
        // stop 移除 -s 并加上 -stop
        Assert.Contains("/app/app.exe app.dll -stop", result);
        // restart 使用 $0 stop/start
        Assert.Contains("$0 stop", result);
        Assert.Contains("$0 start", result);
    }

    [Fact]
    [DisplayName("RcInit_无参数_用例提示正确")]
    public void RcInit_NoArgs_UsageCorrect()
    {
        var result = RcInit.BuildScript("/app/svc", "", "无参数服务");

        Assert.Contains("Usage: $0 {start|stop|restart}", result);
        Assert.Contains("exit 1", result);
    }

    [Fact]
    [DisplayName("RcInit_完整脚本_格式正确")]
    public void RcInit_FullScript_FormatCorrect()
    {
        var result = RcInit.BuildScript("/usr/bin/myservice", "--daemon", "MyService");

        // 验证行数合理
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 15);

        // 验证最后一行是 exit $?
        var lastLine = lines[^1].Trim();
        Assert.Equal("exit $?", lastLine);
    }

    #endregion
}
