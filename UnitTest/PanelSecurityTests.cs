using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Agent.WebPanel.Security;
using NewLife.Data;
using NewLife.Http;
using NewLife.Net;
using NewLife.Security;
using NewLife.Serialization;
using Setting = NewLife.Agent.Setting;
using Xunit;

namespace UnitTest;

/// <summary>Web面板口令哈希、认证钩子与安全前置检查单元测试</summary>
/// <remarks>会读取/修改全局 Setting，串行执行；涉及真实启动面板的用例仅绑定回环地址。</remarks>
[Collection("WebPanel")]
public class PanelSecurityTests
{
    #region 辅助
    private static AgentWebPanel CreatePanel()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "PanelSec";

        return new AgentWebPanel(mockSvc.Object);
    }

    private static IHttpContext CreateContext(String token)
    {
        var request = new HttpRequest();
        request.Parse(new ArrayPacket($"POST /api/updateConfig HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer {token}\r\n\r\n".GetBytes()));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(new HttpResponse());

        return context.Object;
    }

    private static IHttpContext CreateSetupContext(IPAddress remote, String forwarded = null)
    {
        var headers = "Host: 127.0.0.1:5580\r\nX-Panel-Setup: 1\r\n";
        if (!forwarded.IsNullOrEmpty()) headers += $"X-Forwarded-For: {forwarded}\r\n";

        var request = new HttpRequest();
        request.Parse(new ArrayPacket($"POST /api/Setup HTTP/1.1\r\n{headers}\r\n".GetBytes()));

        var socket = new Mock<ISocketRemote>();
        socket.Setup(x => x.Remote).Returns(new NetUri(NetType.Tcp, new IPEndPoint(remote, 12345)));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(new HttpResponse());
        context.Setup(x => x.Socket).Returns(socket.Object);

        return context.Object;
    }

    private static Int32 GetCode(Object result) => (Int32)result.GetType().GetProperty("Code").GetValue(result);
    #endregion

    #region 口令哈希
    [Fact]
    [DisplayName("口令哈希_加盐散列后可验证且不是明文")]
    public void PasswordProvider_Roundtrip()
    {
        var provider = new SaltPasswordProvider();
        var stored = provider.Hash("P@ssw0rd");

        Assert.NotEqual("P@ssw0rd", stored);
        Assert.True(provider.Verify("P@ssw0rd", stored));
        Assert.False(provider.Verify("p@ssw0rd", stored));
    }

    [Fact]
    [DisplayName("默认口令判断_同一口令值只验证一次")]
    public void IsDefaultPassword_CachesResult()
    {
        var panel = CreatePanel();
        var provider = new Mock<IPasswordProvider>();
        provider.Setup(x => x.Verify("admin", "MyPass")).Returns(false);

        panel.PasswordProvider = provider.Object;
        panel.Password = "MyPass";

        Assert.False(panel.IsDefaultPassword);
        Assert.False(panel.IsDefaultPassword);

        // 公开的 SetupStatus 会反复读取该属性，不能每次都执行口令散列验证
        provider.Verify(x => x.Verify("admin", "MyPass"), Times.Once);
    }

    [Fact]
    [DisplayName("口令哈希_相同口令两次散列盐值不同")]
    public void PasswordProvider_UsesRandomSalt()
    {
        var provider = new SaltPasswordProvider();
        var first = provider.Hash("123456");
        var second = provider.Hash("123456");

        Assert.NotEqual(first, second);
        Assert.True(provider.Verify("123456", first));
        Assert.True(provider.Verify("123456", second));
    }

    [Fact]
    [DisplayName("口令哈希_历史明文仍可验证")]
    public void PasswordProvider_LegacyPlaintext()
    {
        var provider = new SaltPasswordProvider();

        Assert.True(provider.Verify("admin", "admin"));
        Assert.False(provider.Verify("admin2", "admin"));
    }

    [Fact]
    [DisplayName("口令提供者_可替换为自定义实现")]
    public void PasswordProvider_Replaceable()
    {
        var panel = CreatePanel();
        panel.PasswordProvider = new StubPasswordProvider();
        panel.UserName = "admin";
        panel.Password = "stub:123456";

        Assert.True(panel.Authenticate("admin", "123456"));
        Assert.False(panel.Authenticate("admin", "654321"));
    }

    [Fact]
    [DisplayName("口令哈希_默认PBKDF2_格式与恒定校验")]
    public void PasswordProvider_DefaultPbkdf2()
    {
        var panel = CreatePanel();
        var provider = Assert.IsType<Pbkdf2PasswordProvider>(panel.PasswordProvider);

        var stored = provider.Hash("P@ssw0rd");

        Assert.StartsWith(Pbkdf2PasswordProvider.Prefix, stored);
        Assert.True(provider.Verify("P@ssw0rd", stored));
        Assert.False(provider.Verify("p@ssw0rd", stored));
        Assert.False(provider.Verify("P@ssw0rd", stored + "x"));
        Assert.False(provider.Verify("P@ssw0rd", "$pbkdf2-sha256$bad"));
    }

    [Theory]
    [DisplayName("口令哈希_迭代次数越界_拒绝生成")]
    [InlineData(999)]
    [InlineData(2_000_001)]
    public void PasswordProvider_IterationsOutOfRange_Rejected(Int32 iterations)
    {
        var provider = new Pbkdf2PasswordProvider { Iterations = iterations };

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Hash("pass"));
    }

    [Fact]
    [DisplayName("口令哈希_篡改迭代次数_拒绝验证")]
    public void PasswordProvider_TamperedIterations_Rejected()
    {
        var provider = new Pbkdf2PasswordProvider { Iterations = 1_000 };
        var stored = provider.Hash("pass");

        Assert.False(provider.Verify("pass", stored.Replace("$1000$", "$1$")));
    }

    [Fact]
    [DisplayName("口令哈希_低迭代PBKDF2_登录后按新工作因子升级")]
    public void PasswordProvider_LowIterations_UpgradesOnLogin()
    {
        using var scope = new SettingScope();

        // 低迭代模拟历史配置，避免测试等待默认工作因子
        var provider = new Pbkdf2PasswordProvider { Iterations = 2_000 };
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebUserName = "admin";
        set.WebPassword = new Pbkdf2PasswordProvider { Iterations = 1_000 }.Hash("old-pass");

        var panel = CreatePanel();
        panel.PasswordProvider = provider;

        Assert.NotNull(panel.IssueToken("admin", "old-pass"));
        Assert.StartsWith("$pbkdf2-sha256$2000$", panel.Password);
        Assert.StartsWith("$pbkdf2-sha256$2000$", set.WebPassword);
        Assert.True(provider.Verify("old-pass", set.WebPassword));
    }

    [Fact]
    [DisplayName("口令哈希_PBKDF2兼容旧加盐SHA512并登录后升级")]
    public void PasswordProvider_LegacySha512_Migrates()
    {
        using var scope = new SettingScope();

        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebUserName = "admin";
        set.WebPassword = new SaltPasswordProvider().Hash("legacy-pass");

        var panel = CreatePanel();
        Assert.False(panel.IsDefaultPassword);

        Assert.NotNull(panel.IssueToken("admin", "legacy-pass"));
        Assert.StartsWith(Pbkdf2PasswordProvider.Prefix, panel.Password);
        Assert.StartsWith(Pbkdf2PasswordProvider.Prefix, set.WebPassword);
        Assert.True(panel.PasswordProvider.Verify("legacy-pass", set.WebPassword));
    }

    private class StubPasswordProvider : IPasswordProvider
    {
        public String Hash(String password) => "stub:" + password;

        public Boolean Verify(String password, String hash) => hash == "stub:" + password;
    }
    #endregion

    #region 认证
    [Fact]
    [DisplayName("登录_哈希口令可签发令牌")]
    public void IssueToken_HashedPassword()
    {
        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = panel.PasswordProvider.Hash("123456");

        Assert.NotNull(panel.IssueToken("admin", "123456"));
        Assert.Null(panel.IssueToken("admin", "654321"));
    }

    [Fact]
    [DisplayName("登录_历史明文口令成功后自动升级为哈希")]
    public void IssueToken_LegacyPassword_Migrates()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebUserName = "admin";
        set.WebPassword = "legacy-pass";

        var panel = CreatePanel();
        Assert.Equal("legacy-pass", panel.Password);

        Assert.NotNull(panel.IssueToken("admin", "legacy-pass"));
        Assert.NotEqual("legacy-pass", panel.Password);
        Assert.NotEqual("legacy-pass", set.WebPassword);
        Assert.True(panel.PasswordProvider.Verify("legacy-pass", set.WebPassword));
    }

    [Fact]
    [DisplayName("认证_子类可通过重写接入外部认证")]
    public void OnAuthenticate_Overridable()
    {
        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "PanelSec";

        var panel = new ExternalPanel(mockSvc.Object);

        Assert.True(panel.Authenticate("domain\\user", "token"));
        Assert.False(panel.Authenticate("domain\\user", "bad"));
    }

    private class ExternalPanel(ServiceBase service) : AgentWebPanel(service)
    {
        protected override Boolean OnAuthenticate(String user, String password, String remoteIp) => user == "domain\\user" && password == "token";
    }
    #endregion

    #region 默认口令
    [Fact]
    [DisplayName("默认口令_空串与admin均被识别")]
    public void IsDefaultPassword_Detects()
    {
        var panel = CreatePanel();

        panel.Password = "admin";
        Assert.True(panel.IsDefaultPassword);

        panel.Password = "";
        Assert.True(panel.IsDefaultPassword);

        panel.Password = panel.PasswordProvider.Hash("admin");
        Assert.True(panel.IsDefaultPassword);

        panel.Password = panel.PasswordProvider.Hash("MyPass");
        Assert.False(panel.IsDefaultPassword);
    }
    #endregion

    #region 敏感配置
    [Fact]
    [DisplayName("敏感字段_密码与密钥不进入配置面板")]
    public void IsSensitive_DetectsSecrets()
    {
        Assert.True(ApiController.IsSensitive("WebPassword"));
        Assert.True(ApiController.IsSensitive("WebCertificatePassword"));
        Assert.True(ApiController.IsSensitive("Secret"));
        Assert.False(ApiController.IsSensitive("WebPort"));
    }

    [Fact]
    [DisplayName("配置元数据_不返回密码字段")]
    public void ConfigMetadata_ExcludesSecrets()
    {
        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = panel.PasswordProvider.Hash("123456");
        var token = panel.IssueToken("admin", "123456");

        var controller = new ApiController { Context = CreateContext(token) };
        var json = controller.ConfigMetadata().ToJson();

        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToArray();

        Assert.NotEmpty(names);
        Assert.DoesNotContain("WebPassword", names);
        Assert.Contains("WebBind", names);
    }
    #endregion

    #region 启动安全前置
    [Fact]
    [DisplayName("启动检查_非本机监听且默认口令_拒绝启动且不生成随机口令")]
    public void Start_NonLoopback_DefaultPassword_Refuses()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "0.0.0.0";
        set.WebPort = 0;
        set.WebPassword = "admin";
        set.AllowInsecureRemote = false;

        var panel = CreatePanel();
        panel.Start();

        Assert.False(panel.Active);
        Assert.Equal("admin", set.WebPassword);
        Assert.True(panel.NeedsSetup);
    }

    [Fact]
    [DisplayName("启动检查_非本机监听未显式允许明文远程_拒绝")]
    public void Start_NonLoopback_WithoutOptIn_Refuses()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "0.0.0.0";
        set.WebPort = 0;
        set.WebPassword = "MyPass";
        set.AllowInsecureRemote = false;

        var panel = CreatePanel();
        panel.Start();

        Assert.False(panel.Active);
    }

    [Fact]
    [DisplayName("启动检查_非本机监听显式允许明文远程_可启动")]
    public void Start_NonLoopback_WithOptIn_Starts()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "0.0.0.0";
        set.WebPort = 0;
        set.WebPassword = "MyPass";
        set.AllowInsecureRemote = true;

        var panel = CreatePanel();
        panel.Start();
        try
        {
            Assert.True(panel.Active);
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }

    [Fact]
    [DisplayName("首次启动_本机回环默认口令_进入设置模式且admin不可登录")]
    public void Start_Loopback_DefaultPassword_SetupMode()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 0;
        set.WebPassword = "admin";

        var panel = CreatePanel();
        panel.Start();
        try
        {
            Assert.True(panel.Active);
            Assert.True(panel.NeedsSetup);

            // 设置完成前默认口令不能换到令牌
            Assert.Null(panel.IssueToken("admin", "admin"));
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }

    [Fact]
    [DisplayName("首次设置页_提交新口令_签发令牌并退出设置模式")]
    public async Task SetupPage_PostPassword_Succeeds()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 0;
        set.WebPassword = "admin";

        var panel = CreatePanel();
        panel.Start();
        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{panel.Port}/"),
                Timeout = TimeSpan.FromSeconds(30),
            };

            var status = await http.GetStringAsync("api/SetupStatus");
            using (var doc = JsonDocument.Parse(status))
            {
                Assert.True(doc.RootElement.GetProperty("data").GetProperty("needsSetup").GetBoolean());
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/Setup");
            request.Headers.Add("X-Panel-Setup", "1");
            request.Content = new StringContent("{\"password\":\"newpass123\"}", Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            using var result = JsonDocument.Parse(body);
            Assert.Equal(0, result.RootElement.GetProperty("code").GetInt32());

            var token = result.RootElement.GetProperty("data").GetProperty("token").GetString();
            Assert.False(token.IsNullOrEmpty());
            Assert.False(panel.NeedsSetup);
            Assert.NotNull(panel.IssueToken("admin", "newpass123"));
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }

    [Fact]
    [DisplayName("首次设置_带转发头_拒绝")]
    public void Setup_ForwardedHeader_Refused()
    {
        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = "admin";

        var controller = new ApiController { Context = CreateSetupContext(IPAddress.Loopback, "203.0.113.9") };
        var result = controller.Setup("newpass123");

        Assert.Equal(403, GetCode(result));
        Assert.True(panel.NeedsSetup);
    }

    [Fact]
    [DisplayName("首次设置_非回环连接_拒绝")]
    public void Setup_NonLoopback_Refused()
    {
        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = "admin";

        var controller = new ApiController { Context = CreateSetupContext(IPAddress.Parse("192.168.1.20")) };
        var result = controller.Setup("newpass123");

        Assert.Equal(403, GetCode(result));
        Assert.True(panel.NeedsSetup);
    }
    #endregion

    #region 开放远程拦截
    [Fact]
    [DisplayName("配置更新_默认口令下改为监听所有网卡被拒绝")]
    public void UpdateConfig_DefaultPassword_OpenRemote_Refused()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 5580;
        set.WebPassword = "admin";

        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = "testpass123";
        var token = panel.IssueToken("admin", "testpass123");

        var controller = new ApiController { Context = CreateContext(token) };
        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["WebBind"] = "*" });

        Assert.Equal(400, GetCode(result));
        Assert.Equal("127.0.0.1", set.WebBind);
    }

    [Fact]
    [DisplayName("配置更新_端口非法被拒绝")]
    public void UpdateConfig_InvalidPort_Refused()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 5580;
        set.WebPassword = "admin";

        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = "testpass123";
        var token = panel.IssueToken("admin", "testpass123");

        var controller = new ApiController { Context = CreateContext(token) };
        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["WebPort"] = 0 });

        Assert.Equal(400, GetCode(result));
        Assert.Equal(5580, set.WebPort);
    }
    #endregion

    #region 监听地址热重启
    [Fact]
    [DisplayName("配置更新_修改端口_热重启面板并切换监听")]
    public async Task UpdateConfig_ChangePort_RebindsWithoutRestart()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 0;
        set.WebUserName = "admin";
        set.WebPassword = "123456";

        var mockSvc = new Mock<ServiceBase> { CallBase = true };
        mockSvc.Object.ServiceName = "RebindSec";

        var panel = new AgentWebPanel(mockSvc.Object);
        panel.Start();
        var oldPort = panel.Port;
        Assert.NotEqual(0, oldPort);

        try
        {
            set.WebPort = oldPort;
            var token = panel.IssueToken("admin", "123456");
            var controller = new ApiController { Context = CreateContext(token) };

            var newPort = GetFreePort();
            var result = controller.UpdateConfig(new Dictionary<String, Object> { ["WebPort"] = newPort });
            Assert.Equal(0, GetCode(result));

            // 等待后台热重启完成
            for (var i = 0; i < 50 && panel.Port != newPort; i++) await Task.Delay(100);

            Assert.Equal(newPort, panel.Port);
            Assert.True(panel.Active);

            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{newPort}") };
            var res = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        finally
        {
            panel.Stop("TestDone");
        }
    }

    /// <summary>取一个当前空闲的本机端口</summary>
    private static Int32 GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
    #endregion
}
