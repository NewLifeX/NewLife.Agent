using System.ComponentModel;
using System.Globalization;
using NewLife;
using NewLife.Agent;
using NewLife.Configuration;
using NewLife.Log;

namespace UnitTest;

/// <summary>本地化与带前缀日志单元测试</summary>
/// <remarks>
/// 语言表由 NewLife.Core 源生成器在编译期从 NewLife.Agent/Locales 生成，
/// 这里验证运行时翻译、日志语言切换，以及前缀日志“先翻译后拼前缀”的语义。
/// Localization 集合禁止并行，避免切换全局语言影响其他用例。
/// </remarks>
[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }

[Collection("Localization")]
public class LocalizationTests
{
    static LocalizationTests()
    {
        // 触发 NewLife.Agent 模块初始化，注册编译期语言表；模块加载前不注册
        _ = L.T("Install service");
    }

    private sealed class CaptureLog : Logger
    {
        public List<String> Messages { get; } = [];

        protected override String Format(String format, Object[] args) => base.Format(format, args);

        protected override void OnWrite(LogLevel level, String format, params Object[] args) => Messages.Add(Format(format, args));
    }

    private static void UseCulture(String culture)
    {
        Localization.Culture = new CultureInfo(culture);
        Localization.LogCulture = new CultureInfo(culture);
    }

    private static void ResetCulture()
    {
        Localization.Culture = null;
        Localization.LogCulture = null;
    }

    [Fact]
    [DisplayName("本地化_简体中文_翻译组件词表")]
    public void Translate_SimplifiedChinese()
    {
        try
        {
            UseCulture("zh-Hans");

            Assert.Equal("安装服务", Localization.Translate("Install service"));
            Assert.Equal("未安装", Localization.Translate("Not installed"));
            Assert.Equal("无效的命令序号 [{0}]，请重新输入！", Localization.Translate("Invalid command number [{0}]; please try again."));
            Assert.Equal("服务启动：模拟运行开始", Localization.Translate("Service started: {0}", "模拟运行开始"));
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("本地化_繁体中文_翻译组件词表")]
    public void Translate_TraditionalChinese()
    {
        try
        {
            UseCulture("zh-Hant");

            Assert.Equal("安裝服務", Localization.Translate("Install service"));
            Assert.Equal("未安裝", Localization.Translate("Not installed"));
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("本地化_未知语言_回退英文原文")]
    public void Translate_UnknownCulture_FallsBackToEnglish()
    {
        try
        {
            UseCulture("fr");

            // fr 词表存在，先确认可用；再切换到不存在的语言确认回退
            Assert.Equal("Installer le service", Localization.Translate("Install service"));

            UseCulture("eo");
            Assert.Equal("Install service", Localization.Translate("Install service"));
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("本地化_配置项描述_按当前语言翻译")]
    public void Translate_ConfigDescriptions()
    {
        try
        {
            UseCulture("zh-Hans");

            Assert.Equal("监控时间间隔。服务资源占用检测周期（秒），默认 10。", Localization.Translate("Watch interval. Resource usage check period in seconds. Default 10."));
            Assert.Equal("Web 面板密码。", Localization.Translate("Web panel password."));

            UseCulture("fr");
            Assert.Equal("Intervalle de surveillance. Période de vérification de l'utilisation des ressources, en secondes. Par défaut 10.", Localization.Translate("Watch interval. Resource usage check period in seconds. Default 10."));
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("本地化_Hosting扩展包消息_已注册词表")]
    public void Translate_HostingPackageMessages()
    {
        try
        {
            // Hosting 扩展包复用主库词表；引用类型确保扩展包可正常加载
            _ = typeof(NewLife.Extensions.Hosting.AgentService.ServiceLifetime);

            UseCulture("zh-Hans");
            Assert.Equal("正常退出菜单", Localization.Translate("Menu exited normally"));
            Assert.Equal("应用已启动。宿主环境：Production", Localization.Translate("Application started. Hosting environment: {0}", "Production"));
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("配置保存_Xml注释_按当前语言输出")]
    public void XmlConfig_Save_LocalizesComments()
    {
        var file = Path.GetTempFileName();
        try
        {
            UseCulture("zh-Hans");

            var prv = new XmlConfigProvider { FileName = file };
            prv.Save(new NewLife.Agent.Setting());

            var xml = File.ReadAllText(file);
            Assert.Contains("<!--服务名-->", xml);
            Assert.Contains("<!--监控时间间隔。服务资源占用检测周期（秒），默认 10。-->", xml);
        }
        finally
        {
            ResetCulture();
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("Watch interval. Resource usage check period in seconds. Default 10.", "Watch interval")]
    [InlineData("Web panel password.", "Web panel password")]
    [InlineData("监控时间间隔。服务资源占用检测周期（秒），默认 10。", "监控时间间隔")]
    [InlineData("সেবার নাম। Windows-এ, স্বয়ংক্রিয় চালু লগইনের পরে ডেস্কটপে চলে।", "সেবার নাম")]
    [InlineData("ช่วงเวลาตรวจสอบ ช่วงเวลาตรวจสอบการใช้ทรัพยากรเป็นวินาที ค่าเริ่มต้น 10", "ช่วงเวลาตรวจสอบ")]
    [InlineData("No punctuation", "No punctuation")]
    public void GetDisplayName_FirstSentence(String description, String expected)
    {
        Assert.Equal(expected, NewLife.Agent.WebPanel.ApiController.GetDisplayName(description));
    }

    [Fact]
    [DisplayName("前缀日志_先翻译后拼前缀_前缀不参与本地化")]
    public void WriteLogPrefixed_TranslatesMessageBeforePrefix()
    {
        try
        {
            UseCulture("zh-Hans");

            var log = new CaptureLog();
            log.WriteLogPrefixed("[Agent] ", "Service started: {0}", "Simulation");

            var message = Assert.Single(log.Messages);
            Assert.Equal("[Agent] 服务启动：Simulation", message);
            // 前缀原样保留，未参与查表
            Assert.StartsWith("[Agent] ", message);
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("服务基类_前缀日志_转发到日志对象")]
    public void ServiceBase_WriteLogPrefixed_ForwardsToLog()
    {
        try
        {
            UseCulture("zh-Hans");

            var log = new CaptureLog();
            var svc = new ServiceBaseTester { Log = log };
            svc.WriteLogPrefixed("[Agent] ", "Install service");

            var message = Assert.Single(log.Messages);
            Assert.Equal("[Agent] 安装服务", message);
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("前缀日志_空日志对象_不抛异常")]
    public void WriteLogPrefixed_NullLog_NoThrow()
    {
        ILog log = null;
        log.WriteLogPrefixed("[Agent] ", "Service started: {0}", "Simulation");
    }

    [Fact]
    [DisplayName("前缀日志_日志级别不足_不输出")]
    public void WriteLogPrefixed_BelowLevel_Ignored()
    {
        var log = new CaptureLog { Level = LogLevel.Warn };
        log.WriteLogPrefixed(LogLevel.Debug, "[Agent] ", "Service started: {0}", "Simulation");

        Assert.Empty(log.Messages);
    }
}
