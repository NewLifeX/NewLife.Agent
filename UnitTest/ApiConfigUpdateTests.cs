using System.ComponentModel;
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

/// <summary>ApiController.UpdateConfig 单元测试</summary>
/// <remarks>
/// 覆盖鉴权拦截、空参数校验、类型转换、密码字段白名单、未知字段忽略与内部异常兜底。
/// UpdateConfig 直接修改进程级单例 Setting.Current 并落盘，凡会改变配置的用例均用 SettingScope 快照还原。
/// </remarks>
[Collection("WebPanel")]
public class ApiConfigUpdateTests
{
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

            // 构造带鉴权令牌的请求上下文（完整报头以 CRLFCRLF 终止）
            var request = new HttpRequest();
            request.Parse(new ArrayPacket($"GET /api/config HTTP/1.1\r\nHost: localhost\r\nAuthorization: Bearer {token}\r\n\r\n".GetBytes()));

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

    /// <summary>从响应结果按 JSON 契约读取 code</summary>
    private static Int32 GetCode(Object result)
    {
        using var doc = JsonDocument.Parse(result.ToJson());
        return doc.RootElement.GetProperty("code").GetInt32();
    }

    /// <summary>从响应结果按 JSON 契约读取 message</summary>
    private static String GetMessage(Object result)
    {
        using var doc = JsonDocument.Parse(result.ToJson());
        return doc.RootElement.GetProperty("message").GetString();
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
    #endregion

    #region 鉴权
    [Fact]
    [DisplayName("UpdateConfig_未鉴权_返回401")]
    public void UpdateConfig_NoToken_Returns401()
    {
        // 先构造面板（固定 camelCase 序列化策略并作为 Current），再换用无鉴权头的上下文
        var (_, _) = CreateController();

        var before = Setting.Current.MaxThread;

        var controller = new ApiController { Context = CreateContext("/api/config") };
        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["MaxThread"] = 123 });

        Assert.Equal(401, GetCode(result));
        Assert.Equal(L.T("Unauthorized"), GetMessage(result));
        Assert.Equal(before, Setting.Current.MaxThread);
    }
    #endregion

    #region 参数校验
    [Fact]
    [DisplayName("UpdateConfig_空参数_返回400")]
    public void UpdateConfig_MissingConfig_Returns400()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        var resultNull = controller.UpdateConfig(null);
        Assert.Equal(400, GetCode(resultNull));
        Assert.Equal(L.T("Missing config"), GetMessage(resultNull));

        var resultEmpty = controller.UpdateConfig(new Dictionary<String, Object>());
        Assert.Equal(400, GetCode(resultEmpty));
        Assert.Equal(L.T("Missing config"), GetMessage(resultEmpty));
    }
    #endregion

    #region 更新行为
    [Fact]
    [DisplayName("UpdateConfig_整型更新_写入Setting")]
    public void UpdateConfig_ValidIntUpdate_AppliesValue()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        Setting.Current.MaxThread = 1000;

        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["MaxThread"] = 123 });

        Assert.Equal(0, GetCode(result));
        Assert.Equal(123, Setting.Current.MaxThread);
    }

    [Fact]
    [DisplayName("UpdateConfig_字符串值_按属性类型转换")]
    public void UpdateConfig_StringValues_ConvertedToPropertyType()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        Setting.Current.MaxThread = 1000;
        Setting.Current.EnableWebPanel = true;

        var updates = new Dictionary<String, Object>
        {
            ["MaxThread"] = "456",
            ["EnableWebPanel"] = "false",
        };
        var result = controller.UpdateConfig(updates);

        Assert.Equal(0, GetCode(result));
        Assert.Equal(456, Setting.Current.MaxThread);
        Assert.False(Setting.Current.EnableWebPanel);
    }

    [Fact]
    [DisplayName("UpdateConfig_非白名单字段_整体拒绝且不部分生效")]
    public void UpdateConfig_WebPassword_IgnoredByWhitelist()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        var beforePassword = Setting.Current.WebPassword;
        var beforeThread = Setting.Current.MaxThread;
        var beforeAfter = Setting.Current.AfterStart;

        // AfterStart 是非白名单字段（会在服务启动时执行命令），同批的其他字段也不允许部分生效
        var result = controller.UpdateConfig(new Dictionary<String, Object>
        {
            ["MaxThread"] = 456,
            ["WebPassword"] = "hacked",
            ["AfterStart"] = "cmd /c whoami",
        });

        Assert.Equal(403, GetCode(result));
        Assert.Equal(beforePassword, Setting.Current.WebPassword);
        Assert.Equal(beforeThread, Setting.Current.MaxThread);
        Assert.Equal(beforeAfter, Setting.Current.AfterStart);
    }

    [Fact]
    [DisplayName("UpdateConfig_未知字段_忽略且不报错")]
    public void UpdateConfig_UnknownField_Ignored()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["NoSuchProperty123"] = "x" });

        Assert.Equal(0, GetCode(result));
    }
    #endregion

    #region 异常兜底
    [Fact]
    [DisplayName("UpdateConfig_非数字字符串_宽松转为0")]
    public void UpdateConfig_NonNumericString_ConvertsToZero()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        Setting.Current.MaxThread = 1000;

        // NewLife 转换策略为“失败返回默认值”：“abc” 被 Reflect.ChangeType 宽松转为 0，不会抛异常
        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["MaxThread"] = "abc" });

        Assert.Equal(0, GetCode(result));
        Assert.Equal(0, Setting.Current.MaxThread);
    }

    [Fact]
    [DisplayName("UpdateConfig_服务名不可从面板修改")]
    public void UpdateConfig_ServiceName_Rejected()
    {
        using var scope = new SettingScope();
        var (_, controller) = CreateController();

        var before = Setting.Current.ServiceName;

        // 服务名会拼接进服务控制命令与平台启动脚本，不能从面板改写
        var result = controller.UpdateConfig(new Dictionary<String, Object> { ["ServiceName"] = "evil & calc" });

        Assert.Equal(403, GetCode(result));
        Assert.Equal(before, Setting.Current.ServiceName);
    }
    #endregion
}
