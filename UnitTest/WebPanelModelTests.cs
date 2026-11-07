using System.ComponentModel;
using System.Net;
using NewLife.Agent.WebPanel;
using Xunit;

namespace UnitTest;

/// <summary>Web 面板模型与辅助类单元测试</summary>
/// <remarks>测试不依赖 HttpServer 的 WebPanel 组件</remarks>
public class WebPanelModelTests
{
    #region 绑定地址
    [Fact]
    [DisplayName("绑定地址_空值与空串_回退本机回环")]
    public void ParseBindAddress_Empty_Loopback()
    {
        Assert.Equal(IPAddress.Loopback, AgentWebPanel.ParseBindAddress(""));
        Assert.Equal(IPAddress.Loopback, AgentWebPanel.ParseBindAddress(null));
    }

    [Fact]
    [DisplayName("绑定地址_星号与0.0.0.0_监听所有网卡")]
    public void ParseBindAddress_Any_ReturnsNull()
    {
        Assert.Null(AgentWebPanel.ParseBindAddress("*"));
        Assert.Null(AgentWebPanel.ParseBindAddress("0.0.0.0"));
    }

    [Fact]
    [DisplayName("绑定地址_具体IP_原样返回")]
    public void ParseBindAddress_Concrete_ReturnsAddress()
    {
        Assert.Equal(IPAddress.Parse("192.168.1.10"), AgentWebPanel.ParseBindAddress("192.168.1.10"));
        Assert.Equal(IPAddress.Loopback, AgentWebPanel.ParseBindAddress("127.0.0.1"));
    }

    [Fact]
    [DisplayName("绑定地址_无法识别_回退本机回环")]
    public void ParseBindAddress_Invalid_Loopback() => Assert.Equal(IPAddress.Loopback, AgentWebPanel.ParseBindAddress("not-an-ip"));

    [Fact]
    [DisplayName("绑定地址_回环判断")]
    public void IsLoopbackBind_Detects()
    {
        Assert.True(AgentWebPanel.IsLoopbackBind("127.0.0.1"));
        Assert.True(AgentWebPanel.IsLoopbackBind(""));
        Assert.False(AgentWebPanel.IsLoopbackBind("*"));
        Assert.False(AgentWebPanel.IsLoopbackBind("192.168.1.10"));
    }
    #endregion
}
