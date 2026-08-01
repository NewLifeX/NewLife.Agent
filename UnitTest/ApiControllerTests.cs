#if !NET40
#nullable disable
using System.ComponentModel;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Data;
using NewLife.Http;
using Setting = NewLife.Agent.Setting;

namespace UnitTest;

/// <summary>ApiController 单元测试</summary>
/// <remarks>
/// 通过真实 HttpRequest 报文构造带 Bearer Token 的请求上下文，
/// 验证状态/控制/配置/健康/日志等 API 行为。串行集合避免全局状态冲突。
/// </remarks>
[Collection("WebPanel")]
public class ApiControllerTests
{
    /// <summary>测试子类，暴露 protected 成员</summary>
    private class Tester : ApiController
    {
        public String Format(UInt64 bps) => FormatSpeed(bps);
        public Boolean? CheckStatus(String name) => CheckServiceStatus(name);
    }

    #region 辅助
    /// <summary>创建带鉴权 token 的控制器实例</summary>
    private static (AgentWebPanel panel, ApiController controller) CreateController()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "ApiTest";
        mockSvc.Object.DisplayName = "API 测试服务";
        mockSvc.Object.Description = "单元测试";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;
        set.WebPort = 0;
        set.EnableWebPanel = false;

        try
        {
            var panel = new AgentWebPanel(mockSvc.Object);
            panel.UserName = "admin";
            panel.Password = "123456";
            var token = panel.IssueToken("admin", "123456");

            // 构造带 Bearer token 的请求上下文
            var request = new HttpRequest();
            request.Parse(new ArrayPacket($"GET /api/status HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer {token}\r\n\r\n".GetBytes()));

            var context = new Mock<IHttpContext>();
            context.Setup(x => x.Request).Returns(request);
            context.Setup(x => x.Response).Returns(new HttpResponse());

            var controller = new ApiController { Context = context.Object };
            return (panel, controller);
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }

    /// <summary>从匿名结果中读取 code</summary>
    private static Int32 GetCode(Object result) => Convert.ToInt32(result.GetType().GetProperty("code")?.GetValue(result));

    /// <summary>从匿名结果中读取 data</summary>
    private static Object GetData(Object result) => result.GetType().GetProperty("data")?.GetValue(result);

    /// <summary>构造指定路径的请求上下文（无鉴权头）</summary>
    private static IHttpContext CreateContext(String path)
    {
        var request = new HttpRequest();
        request.Parse(new ArrayPacket($"GET {path} HTTP/1.1\r\nHost: localhost\r\n\r\n".GetBytes()));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(new HttpResponse());
        return context.Object;
    }
    #endregion

    #region FormatSpeed
    [Theory]
    [DisplayName("FormatSpeed_格式化网络速率")]
    [InlineData(0UL, "0 bps")]
    [InlineData(999UL, "999 bps")]
    [InlineData(1000UL, "1.0 Kbps")]
    [InlineData(1500UL, "1.5 Kbps")]
    [InlineData(1000000UL, "1.0 Mbps")]
    [InlineData(2500000UL, "2.5 Mbps")]
    [InlineData(1000000000UL, "1.00 Gbps")]
    [InlineData(2500000000UL, "2.50 Gbps")]
    public void FormatSpeed_Formats(UInt64 bps, String expected)
    {
        var tester = new Tester();
        Assert.Equal(expected, tester.Format(bps));
    }
    #endregion

    #region 鉴权
    [Fact]
    [DisplayName("Status_无token_返回401")]
    public void Status_NoToken_ReturnsUnauthorized()
    {
        var controller = new ApiController { Context = CreateContext("/api/status") };
        var result = controller.Status();

        Assert.Equal(401, GetCode(result));
    }

    [Fact]
    [DisplayName("Login_正确凭据_返回token")]
    public void Login_ValidCredentials_ReturnsToken()
    {
        var (_, _) = CreateController();
        var controller = new ApiController { Context = CreateContext("/api/login") };

        var result = controller.Login("admin", "123456");

        Assert.Equal(0, GetCode(result));
        Assert.NotNull(GetData(result));
    }

    [Fact]
    [DisplayName("Login_错误密码_返回401")]
    public void Login_WrongPassword_ReturnsUnauthorized()
    {
        var (_, _) = CreateController();
        var controller = new ApiController { Context = CreateContext("/api/login") };

        var result = controller.Login("admin", "wrongpass");

        Assert.Equal(401, GetCode(result));
    }
    #endregion

    #region 状态
    [Fact]
    [DisplayName("Status_带token_返回服务状态")]
    public void Status_WithToken_ReturnsStatus()
    {
        var (_, controller) = CreateController();

        var result = controller.Status();

        Assert.Equal(0, GetCode(result));
        var data = GetData(result);
        Assert.NotNull(data);
        Assert.Equal("ApiTest", data.GetType().GetProperty("serviceName")?.GetValue(data));
    }
    #endregion

    #region 控制
    [Fact]
    [DisplayName("Control_未知操作_返回400")]
    public void Control_UnknownAction_Returns400()
    {
        var (_, controller) = CreateController();

        var result = controller.Control("unknown");

        Assert.Equal(400, GetCode(result));
    }

    [Fact]
    [DisplayName("Control_停止操作_返回成功")]
    public void Control_Stop_ReturnsSuccess()
    {
        var (_, controller) = CreateController();

        var result = controller.Control("stop");

        Assert.Equal(0, GetCode(result));
    }

    [Fact]
    [DisplayName("Control_空操作_返回400")]
    public void Control_EmptyAction_Returns400()
    {
        var (_, controller) = CreateController();

        var result = controller.Control("");

        Assert.Equal(400, GetCode(result));
    }
    #endregion

    #region 配置
    [Fact]
    [DisplayName("ConfigMetadata_返回配置项_排除密码字段")]
    public void ConfigMetadata_ReturnsItems_ExcludesPassword()
    {
        var (_, controller) = CreateController();

        var result = controller.ConfigMetadata();

        Assert.Equal(0, GetCode(result));
        var data = GetData(result);
        var items = (System.Collections.IEnumerable)data.GetType().GetProperty("items")?.GetValue(data);
        var list = items.Cast<Object>().ToList();

        Assert.NotEmpty(list);
        // 应包含 ServiceName，排除 WebPassword
        Assert.Contains(list, e => (String)e.GetType().GetProperty("name")?.GetValue(e) == "ServiceName");
        Assert.DoesNotContain(list, e => (String)e.GetType().GetProperty("name")?.GetValue(e) == "WebPassword");
    }
    #endregion

    #region 健康
    [Fact]
    [DisplayName("Health_带token_返回健康指标")]
    public void Health_WithToken_ReturnsHealth()
    {
        var (_, controller) = CreateController();

        var result = controller.Health();

        Assert.Equal(0, GetCode(result));
        var data = GetData(result);
        Assert.NotNull(data);
        Assert.NotNull(data.GetType().GetProperty("memoryMB")?.GetValue(data));
        Assert.NotNull(data.GetType().GetProperty("threadCount")?.GetValue(data));
    }
    #endregion

    #region 日志
    [Fact]
    [DisplayName("Logs_无日志文件_返回空列表不抛异常")]
    public void Logs_NoLogFile_ReturnsEmpty()
    {
        var (_, controller) = CreateController();

        var result = controller.Logs(10, null, null);

        Assert.Equal(0, GetCode(result));
        var data = GetData(result);
        Assert.NotNull(data.GetType().GetProperty("lines")?.GetValue(data));
    }

    [Fact]
    [DisplayName("Logs_负行数_回退默认200")]
    public void Logs_NegativeCount_DefaultsTo200()
    {
        var (_, controller) = CreateController();

        var result = controller.Logs(-1, null, null);

        Assert.Equal(0, GetCode(result));
    }
    #endregion

    #region 看门狗
    [Fact]
    [DisplayName("WatchDog_带token_返回监控服务列表")]
    public void WatchDog_WithToken_ReturnsServices()
    {
        var (_, controller) = CreateController();

        var result = controller.WatchDog();

        Assert.Equal(0, GetCode(result));
    }
    #endregion
}
#endif
