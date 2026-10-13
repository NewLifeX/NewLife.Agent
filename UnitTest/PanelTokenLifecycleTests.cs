using System.ComponentModel;
using Moq;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using Setting = NewLife.Agent.Setting;
using Xunit;

namespace UnitTest;

/// <summary>面板令牌吊销与 ServiceBase 面板启停联动单元测试</summary>
/// <remarks>
/// 覆盖 AgentWebPanel.RevokeToken 的吊销语义，以及 ServiceBase.StartWork/StopWork
/// 在 EnableWebPanel 开启/关闭时对 Web 面板的创建、启动与清理；会修改全局 Setting，串行执行。
/// </remarks>
[Collection("WebPanel")]
public class PanelTokenLifecycleTests
{
    #region Token 吊销
    /// <summary>吊销已签发 Token 后，验证应失败</summary>
    [Fact]
    [DisplayName("RevokeToken_吊销后验证失败")]
    public void RevokeToken_IssuedToken_ValidateReturnsFalse()
    {
        var panel = CreatePanel("admin", "123456");

        var token = panel.IssueToken("admin", "123456");
        Assert.NotNull(token);
        Assert.True(panel.ValidateToken(token));

        panel.RevokeToken(token);

        Assert.False(panel.ValidateToken(token));
    }

    /// <summary>吊销空 Token 应安全返回，不抛异常</summary>
    [Fact]
    [DisplayName("RevokeToken_空token_不抛异常")]
    public void RevokeToken_EmptyToken_NoThrow()
    {
        var panel = CreatePanel("admin", "123456");

        Assert.Null(Record.Exception(() => panel.RevokeToken(null!)));
        Assert.Null(Record.Exception(() => panel.RevokeToken("")));
    }
    #endregion

    #region 面板启停联动
    /// <summary>暴露 protected 字段 _webPanel 的测试服务</summary>
    private sealed class PanelServiceTester : ServiceBaseTester
    {
        /// <summary>当前 Web 面板实例</summary>
        public AgentWebPanel Panel => _webPanel;
    }

    /// <summary>启用面板时 StartWork 自动启动面板，StopWork 停止并清空引用</summary>
    [Fact]
    [DisplayName("StartWork_启用面板_自动启动并在StopWork后关闭")]
    public void StartWork_PanelEnabled_StartsAndStopsWithService()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = true;
        set.WebPort = 0;
        set.WebBind = "127.0.0.1";

        var svc = new PanelServiceTester { ServiceName = "UTPanelLifecycle" };
        svc.StartWork("test");

        var panel = svc.Panel;
        try
        {
            Assert.NotNull(panel);
            Assert.True(panel.Active);
            Assert.True(panel.Port > 0);
        }
        finally
        {
            svc.StopWork("test");
        }

        Assert.Null(svc.Panel);
        Assert.False(panel.Active);
    }

    /// <summary>未启用面板时 StartWork 不创建面板，StopWork 安全返回</summary>
    [Fact]
    [DisplayName("StartWork_未启用面板_不创建面板")]
    public void StartWork_PanelDisabled_NoPanel()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebPort = 0;

        var svc = new PanelServiceTester { ServiceName = "UTPanelDisabled" };
        svc.StartWork("test");

        Assert.Null(svc.Panel);
        Assert.Null(Record.Exception(() => svc.StopWork("test")));
    }
    #endregion

    #region 辅助
    /// <summary>构造面板并设置凭据，构造期间临时关闭面板自动启动</summary>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>面板实例</returns>
    private static AgentWebPanel CreatePanel(String userName, String password)
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "TestSvc";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;

        set.WebPort = 0;
        set.EnableWebPanel = false;

        try
        {
            var panel = new AgentWebPanel(mockSvc.Object);
            panel.UserName = userName;
            panel.Password = password;
            return panel;
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }
    #endregion
}
