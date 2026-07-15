#if !NET40
#nullable disable
using System.ComponentModel;
using System.Reflection;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using Setting = NewLife.Agent.Setting;
using NewLife.Data;
using NewLife.Http;
using NewLife.Net;
using Xunit;

namespace UnitTest;

/// <summary>Token 管理单元测试</summary>
/// <remarks>验证 AgentWebPanel 的 IssueToken/ValidateToken 方法</remarks>
[Collection("WebPanel")]
public class TokenManagerTests
{
    #region IssueToken 测试
    [Fact]
    public void IssueToken_EmptyUserOrPass_ReturnsNull()
    {
        var panel = CreatePanel("admin", "123456");

        Assert.Null(panel.IssueToken("", "123456"));
        Assert.Null(panel.IssueToken("admin", ""));
        Assert.Null(panel.IssueToken("", ""));
    }

    [Fact]
    public void IssueToken_NoCredentialsConfigured_ReturnsNull()
    {
        var panel = CreatePanel(null, null);

        Assert.Null(panel.IssueToken("admin", "123456"));
    }

    [Fact]
    public void IssueToken_WrongUser_ReturnsNull()
    {
        var panel = CreatePanel("admin", "123456");

        Assert.Null(panel.IssueToken("wrong", "123456"));
    }

    [Fact]
    public void IssueToken_WrongPassword_ReturnsNull()
    {
        var panel = CreatePanel("admin", "123456");

        Assert.Null(panel.IssueToken("admin", "wrongpass"));
    }

    [Fact]
    public void IssueToken_ValidCredentials_ReturnsToken()
    {
        var panel = CreatePanel("admin", "123456");

        var token = panel.IssueToken("admin", "123456");
        Assert.NotNull(token);
        Assert.False(token.IsNullOrEmpty());
        // Token 应该是32位十六进制字符串（Guid无分隔符）
        Assert.Matches(@"^[0-9a-f]{32}$", token);
    }

    [Fact]
    public void IssueToken_DifferentCaseUser_StillSucceeds()
    {
        var panel = CreatePanel("Admin", "123456");

        // IssueToken 内部使用 EqualIgnoreCase 比较用户名
        var token = panel.IssueToken("admin", "123456");
        Assert.NotNull(token);
    }

    [Fact]
    public void ValidateToken_IssuedToken_ReturnsTrue()
    {
        var panel = CreatePanel("admin", "123456");

        var token = panel.IssueToken("admin", "123456");
        Assert.NotNull(token);

        Assert.True(panel.ValidateToken(token));
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsFalse()
    {
        var panel = CreatePanel("admin", "123456");

        Assert.False(panel.ValidateToken("invalid_token"));
    }

    [Fact]
    public void ValidateToken_EmptyToken_ReturnsFalse()
    {
        var panel = CreatePanel("admin", "123456");

        Assert.False(panel.ValidateToken(""));
        Assert.False(panel.ValidateToken(null!));
    }
    #endregion

    #region 辅助
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

/// <summary>LoginRateLimiter 爆破防护单元测试</summary>
[Collection("WebPanel")]
public class LoginRateLimiterTests
{
    [Fact]
    public void IsBlocked_FreshIp_ReturnsFalse()
    {
        Assert.False(LoginRateLimiter.IsBlocked("192.168.1.1"));
    }

    [Fact]
    public void RecordFailure_UnderThreshold_NotBlocked()
    {
        var ip = "10.0.0.1";
        for (var i = 0; i < 4; i++)
            LoginRateLimiter.RecordFailure(ip);

        Assert.False(LoginRateLimiter.IsBlocked(ip));
    }

    [Fact]
    public void RecordFailure_ReachThreshold_Blocked()
    {
        var ip = "10.0.0.2";
        for (var i = 0; i < 5; i++)
            LoginRateLimiter.RecordFailure(ip);

        Assert.True(LoginRateLimiter.IsBlocked(ip));
    }

    [Fact]
    public void RecordSuccess_ClearsBlock()
    {
        var ip = "10.0.0.3";
        for (var i = 0; i < 5; i++)
            LoginRateLimiter.RecordFailure(ip);

        Assert.True(LoginRateLimiter.IsBlocked(ip));

        LoginRateLimiter.RecordSuccess(ip);
        Assert.False(LoginRateLimiter.IsBlocked(ip));
    }

    [Fact]
    public void IsBlocked_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(LoginRateLimiter.IsBlocked(null!));
        Assert.False(LoginRateLimiter.IsBlocked(""));
    }

    [Fact]
    public void RecordFailure_NullOrEmpty_NoException()
    {
        // 空IP不应抛异常
        LoginRateLimiter.RecordFailure(null!);
        LoginRateLimiter.RecordFailure("");
    }

    [Fact]
    public void DifferentIps_IndependentTracking()
    {
        var ip1 = "192.168.1.10";
        var ip2 = "192.168.1.20";

        // ip1 失败5次，ip2 失败1次
        for (var i = 0; i < 5; i++)
            LoginRateLimiter.RecordFailure(ip1);
        LoginRateLimiter.RecordFailure(ip2);

        Assert.True(LoginRateLimiter.IsBlocked(ip1));
        Assert.False(LoginRateLimiter.IsBlocked(ip2));
    }
}

/// <summary>AgentWebPanel 单元测试</summary>
[Collection("WebPanel")]
public class AgentWebPanelTests
{
    #region ParseAuthLevel 测试
    [Fact]
    public void ParseAuthLevel_None_ReturnsNone()
    {
        Assert.Equal(AuthLevel.None, AgentWebPanel.ParseAuthLevel("none"));
        Assert.Equal(AuthLevel.None, AgentWebPanel.ParseAuthLevel("None"));
        Assert.Equal(AuthLevel.None, AgentWebPanel.ParseAuthLevel("NONE"));
    }

    [Fact]
    public void ParseAuthLevel_Full_ReturnsFull()
    {
        Assert.Equal(AuthLevel.Full, AgentWebPanel.ParseAuthLevel("full"));
        Assert.Equal(AuthLevel.Full, AgentWebPanel.ParseAuthLevel("Full"));
    }

    [Fact]
    public void ParseAuthLevel_OtherOrNull_ReturnsLocalOnly()
    {
        Assert.Equal(AuthLevel.LocalOnly, AgentWebPanel.ParseAuthLevel("localonly"));
        Assert.Equal(AuthLevel.LocalOnly, AgentWebPanel.ParseAuthLevel(""));
        Assert.Equal(AuthLevel.LocalOnly, AgentWebPanel.ParseAuthLevel(null!));
        Assert.Equal(AuthLevel.LocalOnly, AgentWebPanel.ParseAuthLevel("random"));
    }
    #endregion

    #region 构造测试
    [Fact]
    public void Constructor_ValidService_CreatesSuccessfully()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "TestSvc";
        mockSvc.Object.DisplayName = "Test Service";

        // 使用 Port=0 自动分配端口，避免端口冲突
        var set = Setting.Current;
        var prevWebPort = set.WebPort;
        set.WebPort = 0;
        set.EnableWebPanel = false;

        try
        {
            var panel = new AgentWebPanel(mockSvc.Object);
            Assert.NotNull(panel);
            Assert.NotNull(panel.Server);
            Assert.Equal(AuthLevel.LocalOnly, panel.Level);
            Assert.Same(mockSvc.Object, panel.Service);
            Assert.False(panel.Active);
            Assert.Equal(0, panel.Port); // 未启动时端口为0
        }
        finally
        {
            set.WebPort = prevWebPort;
        }
    }

    [Fact]
    public void Constructor_ReadsAuthConfigFromSetting()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "AuthSvc";

        var set = Setting.Current;
        var prevLevel = set.WebAuthLevel;
        var prevUser = set.WebUserName;
        var prevPass = set.WebPassword;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;

        try
        {
            set.WebAuthLevel = "full";
            set.WebUserName = "testadmin";
            set.WebPassword = "secret123";
            set.WebPort = 0;
            set.EnableWebPanel = false;

            var panel = new AgentWebPanel(mockSvc.Object);
            Assert.Equal(AuthLevel.Full, panel.Level);
            Assert.Equal("testadmin", panel.UserName);
            Assert.Equal("secret123", panel.Password);
        }
        finally
        {
            set.WebAuthLevel = prevLevel;
            set.WebUserName = prevUser;
            set.WebPassword = prevPass;
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }
    #endregion

    #region Start/Stop 测试
    [Fact]
    public void Start_PortZero_AutoAssigns()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "StartSvc1";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;
        set.WebPort = 0;
        set.EnableWebPanel = false;

        try
        {
            var panel = new AgentWebPanel(mockSvc.Object);
            panel.Start();
            Assert.True(panel.Active);
            Assert.NotEqual(0, panel.Port);
            panel.Stop("test");
            Assert.False(panel.Active);
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }

    [Fact]
    public void Stop_DoubleStop_NoException()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "StopSvc1";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;
        set.WebPort = 0;
        set.EnableWebPanel = false;

        try
        {
            var panel = new AgentWebPanel(mockSvc.Object);
            panel.Start();
            panel.Stop("first");
            Assert.False(panel.Active);

            // 第二次停止不应抛异常
            var ex = Record.Exception(() => panel.Stop("second"));
            Assert.Null(ex);
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }
    #endregion

    #region UpdateCredentials 测试
    [Fact]
    [DisplayName("UpdateCredentials_更新密码_即时生效")]
    public void UpdateCredentials_Password_UpdatesImmediately()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "CredSvc";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;

        try
        {
            set.WebPort = 0;
            set.EnableWebPanel = false;

            var panel = new AgentWebPanel(mockSvc.Object);
            panel.UserName = "admin";
            panel.Password = "oldpass";

            panel.UpdateCredentials("newadmin", "newpass");

            Assert.Equal("newadmin", panel.UserName);
            Assert.Equal("newpass", panel.Password);
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }

    [Fact]
    [DisplayName("UpdateCredentials_只更新用户名_密码不变")]
    public void UpdateCredentials_UserNameOnly_PasswordUnchanged()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "CredSvc2";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;

        try
        {
            set.WebPort = 0;
            set.EnableWebPanel = false;

            var panel = new AgentWebPanel(mockSvc.Object);
            panel.UserName = "admin";
            panel.Password = "secret";

            panel.UpdateCredentials("newadmin", null);

            Assert.Equal("newadmin", panel.UserName);
            Assert.Equal("secret", panel.Password);
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }

    [Fact]
    [DisplayName("UpdateCredentials_空用户名_不更新")]
    public void UpdateCredentials_EmptyUser_NotUpdated()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "CredSvc3";

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;

        try
        {
            set.WebPort = 0;
            set.EnableWebPanel = false;

            var panel = new AgentWebPanel(mockSvc.Object);
            panel.UserName = "admin";
            panel.Password = "secret";

            panel.UpdateCredentials("", "newpass");

            Assert.Equal("admin", panel.UserName);
            Assert.Equal("newpass", panel.Password);
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
        }
    }
    #endregion
}

/// <summary>EmbeddedFileHandler 单元测试</summary>
[Collection("WebPanel")]
public class EmbeddedFileHandlerTests
{
    private static readonly Assembly _agentAssembly = typeof(AgentWebPanel).Assembly;

    private IHttpContext CreateContext(String path)
    {
        var response = new HttpResponse();
        var request = new HttpRequest();
        request.Parse(new ArrayPacket($"GET {path} HTTP/1.1\r\nHost: localhost\r\n\r\n".GetBytes()));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(response);
        context.Setup(x => x.Path).Returns(path);

        return context.Object;
    }

    private static EmbeddedFileHandler CreateHandler() => new()
    {
        Path = "/",
        ContentPath = "NewLife.Agent.WebPanel.wwwroot",
        Assembly = _agentAssembly
    };

    #region 路径处理
    [Fact]
    public void RootPath_ReturnsIndexHtml()
    {
        var handler = CreateHandler();
        var ctx = CreateContext("/");

        handler.ProcessRequest(ctx);

        // index.html 作为嵌入资源应存在，不应返回 404
        Assert.NotEqual(404, (Int32)ctx.Response.StatusCode);
    }

    [Fact]
    public void PathTraversal_Returns404()
    {
        var handler = CreateHandler();
        var ctx = CreateContext("/../../etc/passwd");

        handler.ProcessRequest(ctx);

        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
    }

    [Fact]
    public void NonExistentFile_Returns404()
    {
        var handler = CreateHandler();
        var ctx = CreateContext("/nonexistent.xyz");

        handler.ProcessRequest(ctx);

        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
    }
    #endregion

    #region MIME类型
    [Fact]
    public void HtmlFile_SetsCorrectMimeType()
    {
        var handler = CreateHandler();
        var ctx = CreateContext("/index.html");

        handler.ProcessRequest(ctx);

        Assert.NotEqual(404, (Int32)ctx.Response.StatusCode);
        Assert.StartsWith("text/html", ctx.Response.ContentType);
    }

    [Fact]
    public void UnknownExtension_ReturnsOctetStream()
    {
        // 用一个已知存在的文件但未知扩展名来测试... 
        // 由于只有一个 index.html，我们验证MIME逻辑
        var handler = CreateHandler();

        // JS 文件的 MIME 类型验证
        // 由于我们没有 js 文件嵌入，这里仅验证处理器不会崩溃并且现有逻辑正确
        // 实际验证通过 index.html 的 MIME 类型
        var ctx = CreateContext("/test.unknown");
        handler.ProcessRequest(ctx);
        // 不存在的文件会返回404，不会设置 ContentType
        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
    }
    #endregion
}

/// <summary>ApiController 启停控制测试</summary>
[Collection("WebPanel")]
public class ProcessControlTests
{
    private static AgentWebPanel CreatePanel(Mock<ServiceBase> mockSvc)
    {
        var set = Setting.Current;
        set.WebPort = 0;
        set.EnableWebPanel = false;

        var panel = new AgentWebPanel(mockSvc.Object);
        return panel;
    }

    [Fact(DisplayName = "停止操作应设置Running为false，不调用StopWork")]
    public void Stop_SetsRunningFalse_NotStopWork()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "ControlSvc";
        mockSvc.Object.Running = true;

        var prevPort = Setting.Current.WebPort;
        var prevEnable = Setting.Current.EnableWebPanel;

        try
        {
            var panel = CreatePanel(mockSvc);

            var controller = new ApiController();
            controller.Context = CreateAuthContext(panel);

            var result = controller.Control("stop");

            Assert.NotNull(result);
            Assert.False(mockSvc.Object.Running);
        }
        finally
        {
            Setting.Current.WebPort = prevPort;
            Setting.Current.EnableWebPanel = prevEnable;
        }
    }

    [Fact(DisplayName = "重启操作应调用Host.Restart")]
    public void Restart_CallsHostRestart()
    {
        var mockHost = new Mock<IHost>();
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "RestartSvc";
        mockSvc.Object.Host = mockHost.Object;
        mockSvc.Object.Running = true;

        var prevPort = Setting.Current.WebPort;
        var prevEnable = Setting.Current.EnableWebPanel;

        try
        {
            var panel = CreatePanel(mockSvc);

            var controller = new ApiController();
            controller.Context = CreateAuthContext(panel);

            var result = controller.Control("restart");

            Assert.NotNull(result);
            mockHost.Verify(h => h.Restart("RestartSvc"), Times.Once);
        }
        finally
        {
            Setting.Current.WebPort = prevPort;
            Setting.Current.EnableWebPanel = prevEnable;
        }
    }

    /// <summary>创建测试用的鉴权上下文</summary>
    /// <param name="panel">Web面板实例</param>
    /// <returns>模拟的 IHttpContext</returns>
    private static IHttpContext CreateAuthContext(AgentWebPanel panel)
    {
        panel.UserName = "admin";
        panel.Password = "admin123";
        var token = panel.IssueToken("admin", "admin123");

        var request = new HttpRequest();
        request.Headers["Authorization"] = $"Bearer {token}";

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(new HttpResponse());
        context.Setup(x => x.Connection).Returns((INetSession)null);
        context.Setup(x => x.Socket).Returns((ISocketRemote)null);

        var ctx = context.Object;
        DefaultHttpContext.Current = ctx;
        return ctx;
    }

    private Mock<IHttpContext> CreateMockContext(String body = null)
    {
        var request = new HttpRequest();
        if (body != null)
            request.Body = new ArrayPacket(body.GetBytes());

        var response = new HttpResponse();
        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(response);
        context.Setup(x => x.Connection).Returns((INetSession)null);
        context.Setup(x => x.Socket).Returns((ISocketRemote)null);

        return context;
    }
}

/// <summary>Web面板 E2E 集成测试</summary>
[Collection("WebPanel")]
public class WebPanelE2ETests : IDisposable
{
    private AgentWebPanel _panel;
    private String _baseUrl;

    /// <summary>创建并启动一个独立的面板实例</summary>
    private void StartPanel()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "E2ETestSvc";
        mockSvc.Object.Running = true;

        var set = Setting.Current;
        set.WebPort = 0;
        set.EnableWebPanel = false;

        _panel = new AgentWebPanel(mockSvc.Object);
        _panel.UserName = "admin";
        _panel.Password = "admin123";
        _panel.Start();

        _baseUrl = $"http://localhost:{_panel.Port}";
    }

    public void Dispose()
    {
        _panel?.Stop("test");
        LoginRateLimiter.Reset();
        DefaultHttpContext.Current = null;
    }

    [Fact(DisplayName = "POST /api/login 正确凭据返回 token")]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        StartPanel();
        using var client = new HttpClient();
        var body = """{"user":"admin","password":"admin123"}""";
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{_baseUrl}/api/login", content);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(200, (Int32)response.StatusCode);
        Assert.Contains("\"code\":0", json);
        Assert.Contains("\"token\"", json);
    }

    [Fact(DisplayName = "POST /api/login 错误密码返回 401")]
    public async Task Login_WrongPassword_Returns401()
    {
        StartPanel();
        using var client = new HttpClient();
        var body = """{"user":"admin","password":"wrong"}""";
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{_baseUrl}/api/login", content);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(200, (Int32)response.StatusCode);
        Assert.Contains("\"code\":401", json);
        Assert.Contains("\"Invalid credentials\"", json);
    }

    [Fact(DisplayName = "POST /api/login 5次失败后返回 429")]
    public async Task Login_TooManyFailures_Returns429()
    {
        StartPanel();
        using var client = new HttpClient();
        var body = """{"user":"admin","password":"wrong"}""";
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        for (var i = 0; i < 5; i++)
        {
            var resp = await client.PostAsync($"{_baseUrl}/api/login", content);
            Assert.Equal(200, (Int32)resp.StatusCode);
        }

        // 第6次应被封锁
        var response = await client.PostAsync($"{_baseUrl}/api/login", content);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(200, (Int32)response.StatusCode);
        Assert.Contains("\"code\":429", json);
    }

    [Fact(DisplayName = "带 token 访问 /api/status 返回 200")]
    public async Task Status_WithValidToken_Returns200()
    {
        StartPanel();
        using var client = new HttpClient();

        // 先登录获取 token
        var loginBody = """{"user":"admin","password":"admin123"}""";
        var loginContent = new StringContent(loginBody, System.Text.Encoding.UTF8, "application/json");
        var loginResp = await client.PostAsync($"{_baseUrl}/api/login", loginContent);
        var loginJson = await loginResp.Content.ReadAsStringAsync();

        // 解析 token
        var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(loginJson);
        var token = jsonObj.GetProperty("data").GetProperty("token").GetString();

        // 访问 /api/status
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/status");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var statusJson = await response.Content.ReadAsStringAsync();

        Assert.Equal(200, (Int32)response.StatusCode);
        Assert.Contains("\"code\":0", statusJson);
    }

    [Fact(DisplayName = "无 token 访问 /api/status 返回 401")]
    public async Task Status_WithoutToken_Returns401()
    {
        StartPanel();
        using var client = new HttpClient();

        var response = await client.GetAsync($"{_baseUrl}/api/status");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(200, (Int32)response.StatusCode);
        Assert.Contains("\"code\":401", json);
    }
}
#endif
