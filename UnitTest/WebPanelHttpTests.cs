#if !NET40
#nullable disable
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Moq;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using Setting = NewLife.Agent.Setting;
using Xunit;

namespace UnitTest;

/// <summary>AgentWebPanel 真实 HTTP 集成测试</summary>
/// <remarks>
/// 启动真实 HttpServer（随机端口、仅本机）后发起真实 HTTP 请求，验证静态资源、
/// /api 路由优先级、鉴权与登录流程。补齐功能清单 M7-1 的集成测试缺口，
/// 同时作为 Core 网络栈升级（v12）后的行为回归网。
/// </remarks>
[Collection("WebPanel")]
public class WebPanelHttpTests
{
    #region 辅助
    /// <summary>启动面板并返回基址；调用方负责在 finally 中停止</summary>
    private static AgentWebPanel StartPanel()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "HttpTest";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;
        var prevBind = set.WebBind;
        try
        {
            set.WebPort = 0;            // 端口 0 让系统分配空闲端口
            set.EnableWebPanel = false; // 仅构造面板，不触发 ServiceBase 自动启动
            set.WebBind = "127.0.0.1";  // 仅本机监听，避免防火墙与端口占用干扰

            var panel = new AgentWebPanel(mockSvc.Object)
            {
                // Full 让所有请求都走鉴权，便于验证 Token 流程（LocalOnly 会放行本机）
                Level = AuthLevel.Full,
                UserName = "admin",
                Password = "123456"
            };
            panel.Start();
            return panel;
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
            set.WebBind = prevBind;
        }
    }

    private static HttpClient CreateClient(AgentWebPanel panel) => new()
    {
        BaseAddress = new Uri($"http://127.0.0.1:{panel.Port}"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    #endregion

    [Fact]
    [DisplayName("集成_首页返回内嵌HTML")]
    public async Task Get_Root_ReturnsEmbeddedIndex()
    {
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            var res = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var html = await res.Content.ReadAsStringAsync();
            Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
        }
        finally { panel.Stop("TestDone"); }
    }

    [Fact]
    [DisplayName("集成_静态资源favicon返回200")]
    public async Task Get_Favicon_Returns200()
    {
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            var res = await client.GetAsync("/favicon.ico");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var bytes = await res.Content.ReadAsByteArrayAsync();
            Assert.NotEmpty(bytes);
        }
        finally { panel.Stop("TestDone"); }
    }

    [Fact]
    [DisplayName("集成_api路由优先于静态兜底_未带Token返回401")]
    public async Task Get_ApiWithoutToken_RoutesToControllerAndReturns401()
    {
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            var res = await client.GetAsync("/api/status");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            // 命中 ApiController（若被 /* 静态兜底抢走，这里会是 404 且无 code 字段）
            var body = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.Equal(401, doc.RootElement.GetProperty("code").GetInt32());
        }
        finally { panel.Stop("TestDone"); }
    }

    [Fact]
    [DisplayName("集成_登录后携Token访问status返回数据")]
    public async Task Post_Login_Then_Status_ReturnsData()
    {
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            // 登录（ControllerHandler 从查询串绑定参数）
            var login = await client.PostAsync("/api/login?user=admin&password=123456", null);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var loginBody = await login.Content.ReadAsStringAsync();
            using var loginDoc = JsonDocument.Parse(loginBody);
            Assert.Equal(0, loginDoc.RootElement.GetProperty("code").GetInt32());
            var token = loginDoc.RootElement.GetProperty("data").GetProperty("token").GetString();
            Assert.False(String.IsNullOrEmpty(token));

            // 携带 Token 访问状态接口
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/status");
            req.Headers.Add("Authorization", "Bearer " + token);
            var status = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);

            var statusBody = await status.Content.ReadAsStringAsync();
            using var statusDoc = JsonDocument.Parse(statusBody);
            Assert.Equal(0, statusDoc.RootElement.GetProperty("code").GetInt32());
            Assert.True(statusDoc.RootElement.TryGetProperty("data", out var data));
            Assert.Equal(JsonValueKind.Object, data.ValueKind);
        }
        finally { panel.Stop("TestDone"); }
    }

    [Fact]
    [DisplayName("集成_未匹配路径返回404")]
    public async Task Get_UnknownPath_Returns404()
    {
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            var res = await client.GetAsync("/not-exist-file.html");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
        finally { panel.Stop("TestDone"); }
    }

    [Fact]
    [DisplayName("集成_连续请求稳定_响应体生命周期正常")]
    public async Task Sequential_Requests_KeepServerAlive()
    {
        var panel = StartPanel();
        try
        {
            using var client = CreateClient(panel);

            // 响应体由响应归还池化缓冲（v12 所有权语义），连续请求验证不出现释放后复用问题
            for (var i = 0; i < 5; i++)
            {
                var res = await client.GetAsync("/api/status");
                Assert.Equal(HttpStatusCode.OK, res.StatusCode);

                var body = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                Assert.Equal(401, doc.RootElement.GetProperty("code").GetInt32());
            }
        }
        finally { panel.Stop("TestDone"); }
    }
}
#endif
