using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Http;
using Setting = NewLife.Agent.Setting;
using Xunit;

namespace UnitTest;

/// <summary>Web面板默认全量鉴权中间件测试</summary>
/// <remarks>验证「未登录一律拒绝」与关键白名单，以及下游扩展控制器默认同样受保护。</remarks>
[Collection("WebPanel")]
public class WebPanelAuthMiddlewareTests
{
    #region 辅助
    /// <summary>测试用下游扩展控制器，模拟 StarAgent 这类追加业务接口的场景</summary>
    private class CustomApi : IHttpController
    {
        public IHttpContext Context { get; set; }

        public Object Ping() => new { code = 0, pong = true };
    }

    private class CustomPanel(ServiceBase service) : AgentWebPanel(service)
    {
        protected override void RegisterRoutes()
        {
            Server.MapController<CustomApi>("/custom");
            base.RegisterRoutes();
        }
    }

    private static AgentWebPanel StartPanel()
    {
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 0;
        set.WebUserName = "admin";
        set.WebPassword = "123456";

        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "AuthMw";

        var panel = new CustomPanel(mockSvc.Object);
        panel.Start();

        return panel;
    }

    private static HttpClient CreateClient(AgentWebPanel panel) => new()
    {
        BaseAddress = new Uri($"http://127.0.0.1:{panel.Port}"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    #endregion

    [Fact]
    [DisplayName("鉴权中间件_匿名访问业务接口与下游扩展接口均返回401")]
    public async Task Anonymous_BusinessApi_Returns401()
    {
        using var scope = new SettingScope();
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            var status = await client.GetAsync("/api/Status");
            Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);

            var config = await client.GetAsync("/api/configMetadata");
            Assert.Equal(HttpStatusCode.Unauthorized, config.StatusCode);

            // 下游追加的控制器同样默认受保护，杜绝「忘记写鉴权」的裸奔接口
            var custom = await client.GetAsync("/custom/Ping");
            Assert.Equal(HttpStatusCode.Unauthorized, custom.StatusCode);
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }

    [Fact]
    [DisplayName("鉴权中间件_登录页与公开接口免鉴权")]
    public async Task Public_Paths_Allowed()
    {
        using var scope = new SettingScope();
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            var index = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);

            var html = await client.GetAsync("/index.html");
            Assert.Equal(HttpStatusCode.OK, html.StatusCode);

            var i18n = await client.GetAsync("/i18n/en.json");
            Assert.Equal(HttpStatusCode.OK, i18n.StatusCode);

            var languages = await client.GetAsync("/api/languages");
            Assert.Equal(HttpStatusCode.OK, languages.StatusCode);

            var login = await client.PostAsync("/api/Login", new StringContent("{\"user\":\"admin\",\"password\":\"123456\"}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var body = await login.Content.ReadAsStringAsync();
            Assert.Contains("token", body);
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }

    [Fact]
    [DisplayName("鉴权中间件_携带有效令牌可访问业务接口")]
    public async Task WithToken_Allowed()
    {
        using var scope = new SettingScope();
        var panel = StartPanel();
        try
        {
            var token = panel.IssueToken("admin", "123456");
            Assert.NotNull(token);

            using var client = CreateClient(panel);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var status = await client.GetAsync("/api/Status");
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);

            var custom = await client.GetAsync("/custom/Ping");
            Assert.Equal(HttpStatusCode.OK, custom.StatusCode);

            // WebSocket/EventSource 等无法设置请求头的场景允许通过查询参数传递令牌
            using var queryClient = CreateClient(panel);
            var byQuery = await queryClient.GetAsync($"/api/Status?access_token={token}");
            Assert.Equal(HttpStatusCode.OK, byQuery.StatusCode);
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }
}
