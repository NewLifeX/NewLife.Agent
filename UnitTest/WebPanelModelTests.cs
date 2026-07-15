#if !NET40
using System.ComponentModel;
using NewLife.Agent.WebPanel;

namespace UnitTest;

/// <summary>Web 面板模型与辅助类单元测试</summary>
/// <remarks>测试不依赖 HttpServer 的 WebPanel 组件</remarks>
public class WebPanelModelTests
{
    [Fact]
    [DisplayName("AuthLevel_None_解析正确")]
    public void ParseAuthLevel_None_ReturnsNone()
    {
        var level = AgentWebPanel.ParseAuthLevel("none");
        Assert.Equal(AuthLevel.None, level);
    }

    [Fact]
    [DisplayName("AuthLevel_Full_解析正确")]
    public void ParseAuthLevel_Full_ReturnsFull()
    {
        var level = AgentWebPanel.ParseAuthLevel("full");
        Assert.Equal(AuthLevel.Full, level);
    }

    [Fact]
    [DisplayName("AuthLevel_LocalOnly_解析正确")]
    public void ParseAuthLevel_LocalOnly_ReturnsLocalOnly()
    {
        var level = AgentWebPanel.ParseAuthLevel("LocalOnly");
        Assert.Equal(AuthLevel.LocalOnly, level);
    }

    [Fact]
    [DisplayName("AuthLevel_未知值_默认LocalOnly")]
    public void ParseAuthLevel_Unknown_DefaultsToLocalOnly()
    {
        var level = AgentWebPanel.ParseAuthLevel("invalid");
        Assert.Equal(AuthLevel.LocalOnly, level);
    }

    [Fact]
    [DisplayName("AuthLevel_空值_默认LocalOnly")]
    public void ParseAuthLevel_Empty_DefaultsToLocalOnly()
    {
        var level = AgentWebPanel.ParseAuthLevel("");
        Assert.Equal(AuthLevel.LocalOnly, level);
    }

    [Fact]
    [DisplayName("AuthLevel_大小写不敏感")]
    public void ParseAuthLevel_CaseInsensitive()
    {
        Assert.Equal(AuthLevel.None, AgentWebPanel.ParseAuthLevel("NONE"));
        Assert.Equal(AuthLevel.None, AgentWebPanel.ParseAuthLevel("None"));
        Assert.Equal(AuthLevel.Full, AgentWebPanel.ParseAuthLevel("FULL"));
    }

    [Fact]
    [DisplayName("PanelExtension_构造_默认值正确")]
    public void PanelExtension_Defaults()
    {
        var ext = new PanelExtension();

        Assert.Equal("", ext.Id);
        Assert.Equal("", ext.Name);
        Assert.Equal("", ext.Icon);
        Assert.Equal("", ext.ApiEndpoint);
        Assert.Equal(0, ext.Order);
        Assert.Equal("data", ext.Mode);
    }

    [Fact]
    [DisplayName("PanelExtension_属性可读写")]
    public void PanelExtension_ReadWrite()
    {
        var ext = new PanelExtension
        {
            Id = "my-panel",
            Name = "我的面板",
            Icon = "🔧",
            ApiEndpoint = "/api/my-panel",
            Order = 1,
            Mode = "html"
        };

        Assert.Equal("my-panel", ext.Id);
        Assert.Equal("我的面板", ext.Name);
        Assert.Equal("🔧", ext.Icon);
        Assert.Equal("/api/my-panel", ext.ApiEndpoint);
        Assert.Equal(1, ext.Order);
        Assert.Equal("html", ext.Mode);
    }
}
#endif
