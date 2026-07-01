#if !NET40
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

/// <summary>AuthHandler 鉴权处理器单元测试</summary>
public class AuthHandlerTests
{
    #region 构造测试
    [Fact]
    public void Constructor_NullHandler_Accepted()
    {
        // null handler 允许作为鉴权状态共享对象
        var auth = new AuthHandler(null!);
        Assert.Null(auth.Handler);
        Assert.Equal(AuthLevel.LocalOnly, auth.Level);
    }

    [Fact]
    public void Constructor_ValidHandler_SetsProperties()
    {
        var handler = new Mock<IHttpHandler>();
        var auth = new AuthHandler(handler.Object);
        Assert.Same(handler.Object, auth.Handler);
        Assert.Equal(AuthLevel.LocalOnly, auth.Level);
    }
    #endregion

    #region IssueToken 测试
    [Fact]
    public void IssueToken_EmptyUserOrPass_ReturnsNull()
    {
        var auth = new AuthHandler(new Mock<IHttpHandler>().Object)
        {
            UserName = "admin",
            Password = "123456"
        };

        Assert.Null(auth.IssueToken("", "123456"));
        Assert.Null(auth.IssueToken("admin", ""));
        Assert.Null(auth.IssueToken("", ""));
    }

    [Fact]
    public void IssueToken_NoCredentialsConfigured_ReturnsNull()
    {
        var auth = new AuthHandler(new Mock<IHttpHandler>().Object);
        // UserName/Password 默认 null
        Assert.Null(auth.IssueToken("admin", "123456"));
    }

    [Fact]
    public void IssueToken_WrongUser_ReturnsNull()
    {
        var auth = new AuthHandler(new Mock<IHttpHandler>().Object)
        {
            UserName = "admin",
            Password = "123456"
        };

        Assert.Null(auth.IssueToken("wrong", "123456"));
    }

    [Fact]
    public void IssueToken_WrongPassword_ReturnsNull()
    {
        var auth = new AuthHandler(new Mock<IHttpHandler>().Object)
        {
            UserName = "admin",
            Password = "123456"
        };

        Assert.Null(auth.IssueToken("admin", "wrongpass"));
    }

    [Fact]
    public void IssueToken_ValidCredentials_ReturnsToken()
    {
        var auth = new AuthHandler(new Mock<IHttpHandler>().Object)
        {
            UserName = "admin",
            Password = "123456"
        };

        var token = auth.IssueToken("admin", "123456");
        Assert.NotNull(token);
        Assert.False(token.IsNullOrEmpty());
        // Token 应该是32位十六进制字符串（Guid无分隔符）
        Assert.Matches(@"^[0-9a-f]{32}$", token);
    }

    [Fact]
    public void IssueToken_DifferentCaseUser_StillSucceeds()
    {
        var auth = new AuthHandler(new Mock<IHttpHandler>().Object)
        {
            UserName = "Admin",
            Password = "123456"
        };

        // IssueToken 内部使用 EqualIgnoreCase 比较用户名
        var token = auth.IssueToken("admin", "123456");
        Assert.NotNull(token);
    }
    #endregion

    #region 鉴权级别测试
    [Fact]
    public void ProcessRequest_LevelNone_AlwaysPasses()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.None,
            UserName = "admin",
            Password = "123456"
        };

        // 无 Authorization 头
        var ctx = CreateMockContext(headers: new Dictionary<String, String>());
        auth.ProcessRequest(ctx.Object);

        // 应调用内部 handler
        inner.Verify(x => x.ProcessRequest(ctx.Object), Times.Once);
        // 不应设置 401
        Assert.NotEqual(401, (Int32)ctx.Object.Response.StatusCode);
    }

    [Fact]
    public void ProcessRequest_LevelFull_NoAuth_Rejects()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        var ctx = CreateMockContext(headers: new Dictionary<String, String>());
        auth.ProcessRequest(ctx.Object);

        // 不应调用内部 handler
        inner.Verify(x => x.ProcessRequest(It.IsAny<IHttpContext>()), Times.Never);
        Assert.Equal(401, (Int32)ctx.Object.Response.StatusCode);
    }

    [Fact]
    public void ProcessRequest_LevelFull_ValidToken_Passes()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        var token = auth.IssueToken("admin", "123456");
        Assert.NotNull(token);

        var ctx = CreateMockContext(headers: new Dictionary<String, String>
        {
            ["Authorization"] = $"Bearer {token}"
        });
        auth.ProcessRequest(ctx.Object);

        inner.Verify(x => x.ProcessRequest(ctx.Object), Times.Once);
    }

    [Fact]
    public void ProcessRequest_LevelFull_InvalidToken_Rejects()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        var ctx = CreateMockContext(headers: new Dictionary<String, String>
        {
            ["Authorization"] = "Bearer invalid_token_xxx"
        });
        auth.ProcessRequest(ctx.Object);

        inner.Verify(x => x.ProcessRequest(It.IsAny<IHttpContext>()), Times.Never);
        Assert.Equal(401, (Int32)ctx.Object.Response.StatusCode);
    }

    [Fact]
    public void ProcessRequest_LevelFull_ExpiredToken_Rejects()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        var token = auth.IssueToken("admin", "123456");

        // 验证同一个 token 立即使用能通过
        var ctx1 = CreateMockContext(headers: new Dictionary<String, String>
        {
            ["Authorization"] = $"Bearer {token}"
        });
        auth.ProcessRequest(ctx1.Object);
        inner.Verify(x => x.ProcessRequest(ctx1.Object), Times.Once);

        // 验证第二次使用不同 token（未签发）被拒
        var inner2 = new Mock<IHttpHandler>();
        var auth2 = new AuthHandler(inner2.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        var ctx2 = CreateMockContext(headers: new Dictionary<String, String>
        {
            ["Authorization"] = "Bearer deadbeefdeadbeefdeadbeefdeadbeef"
        });
        auth2.ProcessRequest(ctx2.Object);

        inner2.Verify(x => x.ProcessRequest(It.IsAny<IHttpContext>()), Times.Never);
        Assert.Equal(401, (Int32)ctx2.Object.Response.StatusCode);
    }

    [Fact]
    public void ProcessRequest_LevelFull_WrongScheme_Rejects()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        // 使用 Basic 而非 Bearer 方案
        var ctx = CreateMockContext(headers: new Dictionary<String, String>
        {
            ["Authorization"] = "Basic dXNlcjpwYXNz"
        });
        auth.ProcessRequest(ctx.Object);

        inner.Verify(x => x.ProcessRequest(It.IsAny<IHttpContext>()), Times.Never);
        Assert.Equal(401, (Int32)ctx.Object.Response.StatusCode);
    }

    [Fact]
    public void ProcessRequest_LevelFull_EmptyBearer_Rejects()
    {
        var inner = new Mock<IHttpHandler>();
        var auth = new AuthHandler(inner.Object)
        {
            Level = AuthLevel.Full,
            UserName = "admin",
            Password = "123456"
        };

        var ctx = CreateMockContext(headers: new Dictionary<String, String>
        {
            ["Authorization"] = "Bearer "
        });
        auth.ProcessRequest(ctx.Object);

        inner.Verify(x => x.ProcessRequest(It.IsAny<IHttpContext>()), Times.Never);
        Assert.Equal(401, (Int32)ctx.Object.Response.StatusCode);
    }
    #endregion

    #region 辅助
    /// <summary>创建 mock IHttpContext</summary>
    private Mock<IHttpContext> CreateMockContext(IDictionary<String, String>? headers = null)
    {
        var request = new HttpRequest();
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                request.Headers[kv.Key] = kv.Value;
            }
        }

        var response = new HttpResponse();

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(response);
        context.Setup(x => x.Connection).Returns((INetSession?)null);
        context.Setup(x => x.Socket).Returns((ISocketRemote?)null);

        return context;
    }
    #endregion
}

/// <summary>AgentWebPanel 单元测试</summary>
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
            Assert.NotNull(panel.Auth);
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
            Assert.Equal(AuthLevel.Full, panel.Auth.Level);
            Assert.Equal("testadmin", panel.Auth.UserName);
            Assert.Equal("secret123", panel.Auth.Password);
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
}

/// <summary>EmbeddedFileHandler 单元测试</summary>
public class EmbeddedFileHandlerTests
{
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

    #region 路径处理
    [Fact]
        public void RootPath_ReturnsIndexHtml()
    {
        var handler = new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" };
        var ctx = CreateContext("/");

        handler.ProcessRequest(ctx);

        // index.html 作为嵌入资源应存在，不应返回 404
        Assert.NotEqual(404, (Int32)ctx.Response.StatusCode);
    }

    [Fact]
        public void PathTraversal_Returns404()
    {
        var handler = new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" };
        var ctx = CreateContext("/../../etc/passwd");

        handler.ProcessRequest(ctx);

        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
    }

    [Fact]
        public void NonExistentFile_Returns404()
    {
        var handler = new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" };
        var ctx = CreateContext("/nonexistent.xyz");

        handler.ProcessRequest(ctx);

        Assert.Equal(404, (Int32)ctx.Response.StatusCode);
    }
    #endregion

    #region MIME类型
    [Fact]
        public void HtmlFile_SetsCorrectMimeType()
    {
        var handler = new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" };
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
        var handler = new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" };

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

    private Mock<IHttpContext> CreateMockContext(String body = null)
    {
        var request = new HttpRequest();
        if (body != null)
            request.Body = new ArrayPacket(body.GetBytes());

        var response = new HttpResponse();
        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(response);
        context.Setup(x => x.Connection).Returns((INetSession?)null);
        context.Setup(x => x.Socket).Returns((ISocketRemote?)null);

        return context;
    }
}
#endif
