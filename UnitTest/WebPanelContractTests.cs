#if !NET40
#nullable disable
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Data;
using NewLife.Http;
using NewLife.Serialization;
using Setting = NewLife.Agent.Setting;

namespace UnitTest;

/// <summary>面板 API 响应契约测试（AOT-2）</summary>
/// <remarks>
/// 锁定 DTO 化后各端点的 JSON 契约：键集合与关键值必须与改造前的匿名对象逐键一致；
/// 同时校验 AotRoots 保留清单对全部响应 DTO 的覆盖（防漏登记漂移）。
/// </remarks>
[Collection("WebPanel")]
public class WebPanelContractTests
{
    #region 辅助
    /// <summary>创建带鉴权 token 的控制器实例</summary>
    private static (AgentWebPanel panel, ApiController controller) CreateController(Func<ServiceBase, AgentWebPanel> panelFactory = null)
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
            var panel = panelFactory != null ? panelFactory(mockSvc.Object) : new AgentWebPanel(mockSvc.Object);
            panel.UserName = "admin";
            panel.Password = "123456";
            var token = panel.IssueToken("admin", "123456");

            // 构造带鉴权令牌的请求上下文（完整报头以 CRLFCRLF 终止）
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

    /// <summary>序列化响应并按 JSON 契约解析</summary>
    private static JsonElement Parse(Object result)
    {
        using var doc = JsonDocument.Parse(result.ToJson());
        return doc.RootElement.Clone();
    }

    /// <summary>断言对象的键集合与期望完全一致（无序）</summary>
    private static void AssertExactKeys(JsonElement element, params String[] expected)
    {
        Assert.Equal(expected.OrderBy(n => n).ToArray(), element.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }
    #endregion

    /// <summary>测试用扩展面板：注册一个扩展 Tab，验证扩展项键名契约</summary>
    private sealed class ExtensionTestPanel : AgentWebPanel
    {
        /// <summary>实例化</summary>
        /// <param name="service">服务对象</param>
        public ExtensionTestPanel(ServiceBase service) : base(service) { }

        /// <summary>注册扩展面板</summary>
        /// <returns>扩展面板列表</returns>
        protected override List<PanelExtension> GetExtensions() =>
        [
            new() { Id = "apps", Name = "应用管理", Icon = "📦", ApiEndpoint = "/api/ext/apps", Order = 10 }
        ];
    }

    #region 鉴权契约
    [Fact]
    [DisplayName("契约_登录成功_code+data.token")]
    public void Login_Success_Contract()
    {
        var (_, _) = CreateController();
        var controller = new ApiController { Context = CreateContext("/api/login") };

        var root = Parse(controller.Login("admin", "123456"));

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "token");
        Assert.False(data.GetProperty("token").GetString().IsNullOrEmpty());
    }

    [Fact]
    [DisplayName("契约_登录失败_code+message")]
    public void Login_Failure_Contract()
    {
        var (_, _) = CreateController();
        var controller = new ApiController { Context = CreateContext("/api/login") };

        var root = Parse(controller.Login("admin", "wrongpass"));

        AssertExactKeys(root, "code", "message");
        Assert.Equal(401, root.GetProperty("code").GetInt32());
        Assert.Equal("Invalid credentials", root.GetProperty("message").GetString());
    }

    [Fact]
    [DisplayName("契约_登出_code+message")]
    public void Logout_Contract()
    {
        var (_, _) = CreateController();
        var controller = new ApiController { Context = CreateContext("/api/logout") };

        var root = Parse(controller.Logout());

        AssertExactKeys(root, "code", "message");
        Assert.Equal(0, root.GetProperty("code").GetInt32());
        Assert.Equal("ok", root.GetProperty("message").GetString());
    }
    #endregion

    #region 状态契约
    [Fact]
    [DisplayName("契约_服务状态_全键集合精确")]
    public void Status_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.Status());

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data,
            "serviceName", "displayName", "description", "running", "uptime", "uptimeSeconds",
            "processId", "memoryMB", "memoryTotalMB", "threadCount", "handleCount", "startTime",
            "hostMachine", "platform", "osVersion", "cpuName", "cpuCount", "cpuRate", "cpuRateValue",
            "totalMemory", "availableMemory", "freeMemory", "board", "machineGuid",
            "uplinkSpeed", "downlinkSpeed", "tcpConnections", "tcpTimeWait", "tcpCloseWait",
            "diskIops", "hostUptime", "port");

        Assert.Equal("ApiTest", data.GetProperty("serviceName").GetString());
        Assert.Equal("API 测试服务", data.GetProperty("displayName").GetString());
        Assert.True(data.GetProperty("processId").GetInt32() > 0);
        Assert.Equal(Environment.ProcessorCount, data.GetProperty("cpuCount").GetInt32());
    }

    [Fact]
    [DisplayName("契约_健康指标_嵌套键集合精确")]
    public void Health_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.Health());

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data,
            "memoryMB", "memoryLimitMB", "threadCount", "threadLimit", "handleCount", "handleLimit",
            "totalProcessorTime", "privilegedProcessorTime", "userProcessorTime", "gcTotalMemory", "gcCollections");
        AssertExactKeys(data.GetProperty("gcCollections"), "gen0", "gen1", "gen2");
    }
    #endregion

    #region 控制契约
    [Fact]
    [DisplayName("契约_控制_未知操作_400")]
    public void Control_UnknownAction_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.Control("unknown"));

        AssertExactKeys(root, "code", "message");
        Assert.Equal(400, root.GetProperty("code").GetInt32());
        Assert.Equal("Unknown action: unknown", root.GetProperty("message").GetString());
    }

    [Fact]
    [DisplayName("契约_控制_空操作_400")]
    public void Control_EmptyAction_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.Control(""));

        AssertExactKeys(root, "code", "message");
        Assert.Equal(400, root.GetProperty("code").GetInt32());
    }
    #endregion

    #region 配置契约
    [Fact]
    [DisplayName("契约_配置元数据_项键集合精确")]
    public void ConfigMetadata_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.ConfigMetadata());

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "items");

        var items = data.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        AssertExactKeys(items[0], "name", "displayName", "description", "type", "value");
        Assert.Contains(items, e => e.GetProperty("name").GetString() == "ServiceName");
        Assert.DoesNotContain(items, e => e.GetProperty("name").GetString() == "WebPassword");
    }
    #endregion

    #region 日志契约
    [Fact]
    [DisplayName("契约_日志内容_键集合精确")]
    public void Logs_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.Logs(10, null, null));

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "fileName", "count", "lines");
        Assert.Equal("latest", data.GetProperty("fileName").GetString());
        Assert.Equal(JsonValueKind.Array, data.GetProperty("lines").ValueKind);
    }

    [Fact]
    [DisplayName("契约_日志文件列表_键集合精确")]
    public void LogFiles_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.LogFiles());

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "files");
        Assert.Equal(JsonValueKind.Array, data.GetProperty("files").ValueKind);
    }
    #endregion

    #region 看门狗与扩展契约
    [Fact]
    [DisplayName("契约_看门狗_键集合精确")]
    public void WatchDog_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.WatchDog());

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "services");
        Assert.Equal(JsonValueKind.Array, data.GetProperty("services").ValueKind);
    }

    [Fact]
    [DisplayName("契约_扩展面板列表_键集合精确")]
    public void Extensions_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.Extensions());

        AssertExactKeys(root, "code", "data");
        Assert.Equal(0, root.GetProperty("code").GetInt32());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "panels");
        Assert.Equal(JsonValueKind.Array, data.GetProperty("panels").ValueKind);
    }

    [Fact]
    [DisplayName("契约_扩展面板项_camelCase键集合精确")]
    public void Extensions_PanelItem_CamelCaseKeys()
    {
        var (_, controller) = CreateController(svc => new ExtensionTestPanel(svc));

        var root = Parse(controller.Extensions());

        AssertExactKeys(root, "code", "data");
        var panels = root.GetProperty("data").GetProperty("panels");
        Assert.Equal(1, panels.GetArrayLength());

        var item = panels[0];
        AssertExactKeys(item, "id", "name", "icon", "apiEndpoint", "order", "mode");
        Assert.Equal("apps", item.GetProperty("id").GetString());
        Assert.Equal("/api/ext/apps", item.GetProperty("apiEndpoint").GetString());
        Assert.Equal(10, item.GetProperty("order").GetInt32());
    }
    #endregion

    #region 释放内存契约
    [Fact]
    [DisplayName("契约_释放内存_code+message+data 三键形态")]
    public void FreeMemory_Contract()
    {
        var (_, controller) = CreateController();

        var root = Parse(controller.FreeMemory());

        AssertExactKeys(root, "code", "message", "data");
        Assert.False(root.GetProperty("message").GetString().IsNullOrEmpty());

        var data = root.GetProperty("data");
        AssertExactKeys(data, "beforeMB", "afterMB", "freedMB");
    }
    #endregion

    #region 防漂移
    [Fact]
    [DisplayName("AotRoots_响应DTO_全部登记DynamicDependency")]
    public void AotRoots_AllResponseDtos_Registered()
    {
        var assembly = typeof(ApiController).Assembly;

        // 约定：全部响应 DTO 位于 WebPanel.Models 命名空间
        var expected = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "NewLife.Agent.WebPanel.Models")
            .ToList();
        Assert.True(expected.Count >= 16, $"响应 DTO 数量异常：{expected.Count}");

        var method = assembly.GetType("NewLife.Agent.WebPanel.AotRoots")
            ?.GetMethod("Ensure", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var registered = method.GetCustomAttributes<DynamicDependencyAttribute>()
            .Select(e => e.Type)
            .Where(e => e != null)
            .ToHashSet();

        // 静态工厂/保留清单漏登记会导致 NativeAOT 下该 DTO 序列化为空对象
        foreach (var type in expected)
        {
            Assert.Contains(type, registered);
        }
    }
    #endregion
}
#endif
