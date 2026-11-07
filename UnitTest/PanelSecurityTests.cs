using System.ComponentModel;
using System.Text.Json;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Agent.WebPanel.Security;
using NewLife.Data;
using NewLife.Http;
using NewLife.Serialization;
using Setting = NewLife.Agent.Setting;
using Xunit;

namespace UnitTest;

/// <summary>Web面板口令哈希、认证器与安全前置检查单元测试</summary>
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
        request.Parse(new ArrayPacket($"GET /api/configMetadata HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer {token}\r\n\r\n".GetBytes()));

        var context = new Mock<IHttpContext>();
        context.Setup(x => x.Request).Returns(request);
        context.Setup(x => x.Response).Returns(new HttpResponse());

        return context.Object;
    }
    #endregion

    #region 口令哈希
    [Fact]
    [DisplayName("口令哈希_生成后可验证且不是明文")]
    public void Hash_Roundtrip()
    {
        var stored = PanelPasswordHash.Hash("P@ssw0rd");

        Assert.True(PanelPasswordHash.IsHash(stored));
        Assert.DoesNotContain("P@ssw0rd", stored);
        Assert.True(PanelPasswordHash.Verify("P@ssw0rd", stored));
        Assert.False(PanelPasswordHash.Verify("p@ssw0rd", stored));
    }

    [Fact]
    [DisplayName("口令哈希_历史明文仍可验证且被识别为待升级")]
    public void Verify_LegacyPlaintext()
    {
        Assert.True(PanelPasswordHash.Verify("admin", "admin"));
        Assert.False(PanelPasswordHash.Verify("admin2", "admin"));
        Assert.True(PanelPasswordHash.IsLegacy("admin"));
        Assert.False(PanelPasswordHash.IsLegacy(PanelPasswordHash.Hash("admin")));
    }

    [Fact]
    [DisplayName("口令哈希_两次哈希盐值不同")]
    public void Hash_UsesRandomSalt()
    {
        var first = PanelPasswordHash.Hash("123456");
        var second = PanelPasswordHash.Hash("123456");

        Assert.NotEqual(first, second);
        Assert.True(PanelPasswordHash.Verify("123456", first));
        Assert.True(PanelPasswordHash.Verify("123456", second));
    }

    [Fact]
    [DisplayName("随机口令_长度与字符集符合预期")]
    public void CreateRandomPassword_Length()
    {
        var password = PanelPasswordHash.CreateRandomPassword(16);

        Assert.Equal(16, password.Length);
        Assert.DoesNotContain('0', password);
        Assert.DoesNotContain('O', password);
        Assert.DoesNotContain('l', password);
    }
    #endregion

    #region 证书
    [Fact]
    [DisplayName("证书_用户提供的Pfx不存在时不生成也不创建文件")]
    public void CreateSelfSigned_DisallowedCreate_MissingFile()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pfx");
        try
        {
            Assert.Null(WebPanelSecurity.CreateSelfSigned(file, "", false));
            Assert.False(File.Exists(file));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    [DisplayName("证书_自签生成后可重复加载且指纹一致")]
    public void CreateSelfSigned_GeneratesAndReloads()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pfx");
        try
        {
            var first = WebPanelSecurity.CreateSelfSigned(file, "");
            Assert.NotNull(first);
            Assert.True(File.Exists(file));

            var second = WebPanelSecurity.CreateSelfSigned(file, "", false);
            Assert.NotNull(second);
            Assert.Equal(first.GetCertHashString(), second.GetCertHashString());
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
    #endregion

    #region 认证器
    [Fact]
    [DisplayName("认证器_按配置创建本地口令或系统账号")]
    public void CreateAuthenticator_ByMode()
    {
        Assert.IsType<LocalPasswordAuthenticator>(AgentWebPanel.CreateAuthenticator("Password"));
        Assert.IsType<LocalPasswordAuthenticator>(AgentWebPanel.CreateAuthenticator(null));
        Assert.IsType<OsAccountAuthenticator>(AgentWebPanel.CreateAuthenticator("OsAccount"));
    }

    [Fact]
    [DisplayName("认证器_系统账号模式不允许面板改密")]
    public void OsAccount_CanChangePasswordFalse()
    {
        var authenticator = new OsAccountAuthenticator();

        Assert.False(authenticator.CanChangePassword);
        Assert.Equal("OsAccount", authenticator.Name);
    }

    [Fact]
    [DisplayName("认证器_系统账号用户名非法字符被拒绝")]
    public void OsAccount_InvalidUserName_Rejected()
    {
        Assert.False(OsAccountAuthenticator.IsValidUserName("root; rm -rf /"));
        Assert.False(OsAccountAuthenticator.IsValidUserName("-u"));
        Assert.True(OsAccountAuthenticator.IsValidUserName("stone"));
        Assert.True(OsAccountAuthenticator.IsValidUserName("DOMAIN\\stone"));
    }

    [Fact]
    [DisplayName("登录_哈希口令可签发令牌")]
    public void IssueToken_HashedPassword()
    {
        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = PanelPasswordHash.Hash("123456");

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
        Assert.True(PanelPasswordHash.IsHash(panel.Password));
        Assert.True(PanelPasswordHash.IsHash(set.WebPassword));
        Assert.True(PanelPasswordHash.Verify("legacy-pass", set.WebPassword));
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
    [DisplayName("配置元数据_不返回密码与密钥字段")]
    public void ConfigMetadata_ExcludesSecrets()
    {
        var panel = CreatePanel();
        panel.UserName = "admin";
        panel.Password = PanelPasswordHash.Hash("123456");
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
        Assert.DoesNotContain("WebCertificatePassword", names);
        Assert.Contains("WebTls", names);
    }
    #endregion

    #region 启动安全前置
    [Fact]
    [DisplayName("启动检查_非本机监听且未启用TLS时拒绝启动")]
    public void Start_NonLoopback_WithoutTls_Refuses()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "0.0.0.0";
        set.WebTls = "Off";
        set.WebAllowInsecureRemote = false;
        set.WebPassword = PanelPasswordHash.Hash("123456");

        var panel = CreatePanel();
        panel.Start();

        Assert.False(panel.Active);
    }

    [Fact]
    [DisplayName("启动检查_非本机监听且使用默认口令时拒绝启动")]
    public void Start_NonLoopback_DefaultPassword_Refuses()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "0.0.0.0";
        set.WebTls = "Off";
        set.WebAllowInsecureRemote = true;
        set.WebPassword = "";

        var panel = CreatePanel();
        panel.Start();

        Assert.False(panel.Active);
    }

    [Fact]
    [DisplayName("启动检查_本机回环使用默认口令仍可启动")]
    public void Start_Loopback_DefaultPassword_Starts()
    {
        using var scope = new SettingScope();
        var set = Setting.Current;
        set.EnableWebPanel = false;
        set.WebBind = "127.0.0.1";
        set.WebPort = 0;
        set.WebTls = "Auto";
        set.WebPassword = "";

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
    #endregion
}
