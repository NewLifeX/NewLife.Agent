using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Agent.WebPanel;
using NewLife.Configuration;
using NewLife.Log;
using Setting = NewLife.Agent.Setting;

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
            Assert.Equal("跨平台服务守护", Localization.Translate("Cross-platform Service Agent"));
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
    [DisplayName("语言设置_Core配置_保存后立即生效_无词表保持自动")]
    public void CoreSetting_Language_AppliesCulture()
    {
        using var scope = new SettingScope();

        var set = NewLife.Setting.Current;
        var prevUICulture = CultureInfo.CurrentUICulture;
        try
        {
            set.Language = "zh-Hans";
            set.Save();

            Assert.Equal("zh-CN", Localization.Culture?.Name);
            Assert.Equal("zh-CN", Localization.LogCulture?.Name);
            Assert.Equal("安装服务", Localization.Translate("Install service"));

            // 清空配置后恢复自动识别，不把进程锁在中文
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            set.Language = "";
            set.Save();

            Assert.Null(Localization.Culture);
            Assert.Null(Localization.ForcedLanguage);
            Assert.Equal("Install service", Localization.Translate("Install service"));

            // 没有词表的语言保持自动识别，不把进程锁在英文
            set.Language = "eo";
            set.Save();

            Assert.Null(Localization.Culture);
            Assert.Null(Localization.ForcedLanguage);
            Assert.Equal("Install service", Localization.Translate("Install service"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = prevUICulture;
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("命令菜单_描述在显示时翻译_切换语言立即生效")]
    public void CommandMenu_TranslatesAtRender()
    {
        var svc = new ServiceBaseTester { Host = new TestHost() };
        try
        {
            UseCulture("zh-Hans");
            var menu = svc.Command.GetShortcutMenu().First(e => e.Cmd == CommandConst.Install);
            Assert.Equal("安装服务", menu.Name);

            // 命令描述不再在构造时冻结，切换语言后菜单跟随变化
            UseCulture("fr");
            menu = svc.Command.GetShortcutMenu().First(e => e.Cmd == CommandConst.Install);
            Assert.Equal("Installer le service", menu.Name);
        }
        finally
        {
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("Web面板_首页_按固定语言输出界面")]
    public async Task WebPanel_Index_FollowsForcedCulture()
    {
        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;
        try
        {
            set.WebPort = 0;
            set.EnableWebPanel = false;
            Localization.Culture = null;

            var svc = new Mock<ServiceBase> { CallBase = true }.Object;
            svc.ServiceName = "LocTest";

            var panel = new AgentWebPanel(svc) { Level = AuthLevel.None };
            panel.Start();
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{panel.Port}") };

                static async Task<String> GetIndex(HttpClient client)
                {
                    var response = await client.GetAsync("/");
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }

                static String Visible(String html)
                {
                    html = System.Text.RegularExpressions.Regex.Replace(html, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
                    html = System.Text.RegularExpressions.Regex.Replace(html, "/\\*.*?\\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
                    return System.Text.RegularExpressions.Regex.Replace(html, "(?m)^\\s*//.*$", "");
                }

                Localization.Culture = CultureInfo.GetCultureInfo("en-US");
                var en = Visible(await GetIndex(client));
                Assert.Contains("Service status", en);
                Assert.DoesNotMatch("[\\u4e00-\\u9fff]", en);
                Assert.Contains("<html lang=\"en\">", en);
                Assert.Contains("🌐 Auto", en);
                Assert.Contains("window.__panelLanguage", en);

                Localization.Culture = CultureInfo.GetCultureInfo("ja-JP");
                var ja = Visible(await GetIndex(client));
                Assert.Contains("サービスの状態", ja);
                Assert.DoesNotContain("Service status", ja);

                Localization.Culture = CultureInfo.GetCultureInfo("ar-SA");
                var ar = await GetIndex(client);
                Assert.Contains("dir=\"rtl\"", ar);

                Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
                var zh = await GetIndex(client);
                Assert.Contains("服务状态", zh);
                Assert.DoesNotContain("Service status", zh);
                Assert.Contains("<html lang=\"zh-Hans\">", zh);
                Assert.Contains("🌐 自动", zh);
            }
            finally
            {
                panel.Stop("TestDone");
            }
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("Web面板_语言接口_列表与切换立即生效")]
    public async Task WebPanel_LanguageApi_ListsAndSwitches()
    {
        using var scope = new SettingScope();

        var set = Setting.Current;
        var prevPort = set.WebPort;
        var prevEnable = set.EnableWebPanel;
        try
        {
            set.WebPort = 0;
            set.EnableWebPanel = false;
            Localization.Culture = null;

            var svc = new Mock<ServiceBase> { CallBase = true }.Object;
            svc.ServiceName = "LocTest";

            var panel = new AgentWebPanel(svc) { Level = AuthLevel.Full, UserName = "admin", Password = "123456" };
            panel.Start();
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{panel.Port}") };

                // 语言列表不鉴权，未登录也可读取
                var list = await client.GetStringAsync("/api/languages");
                Assert.Contains("\"code\":\"zh-CN\"", list);

                var token = panel.IssueToken("admin", "123456");
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/setLanguage?language=fr");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var json = await (await client.SendAsync(request)).Content.ReadAsStringAsync();
                Assert.Contains("\"code\":0", json);
                Assert.Equal("fr-FR", Localization.Culture?.Name);

                // 固定语言后忽略请求语言
                using var indexRequest = new HttpRequestMessage(HttpMethod.Get, "/");
                indexRequest.Headers.TryAddWithoutValidation("Accept-Language", "en-US");
                var html = await (await client.SendAsync(indexRequest)).Content.ReadAsStringAsync();
                Assert.Contains("<html lang=\"fr\">", html);
            }
            finally
            {
                panel.Stop("TestDone");
            }
        }
        finally
        {
            set.WebPort = prevPort;
            set.EnableWebPanel = prevEnable;
            ResetCulture();
        }
    }

    [Fact]
    [DisplayName("Web面板_界面词表_键集合一致且覆盖源文")]
    public void UiDictionaries_CompleteAndConsistent()
    {
        var assembly = typeof(AgentWebPanel).Assembly;
        var prefix = "NewLife.Agent.WebPanel.wwwroot";
        var baseline = UiLocalization.LoadDictionary(assembly, prefix, "en");
        Assert.NotEmpty(baseline);

        // 所有界面词表必须与英文基准键集合一致
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith($"{prefix}.i18n.") || !name.EndsWith(".json") || name.Contains(".en.json")) continue;

            var language = name[(name.IndexOf(".i18n.", StringComparison.Ordinal) + 6)..^5];
            var map = UiLocalization.LoadDictionary(assembly, prefix, language);

            Assert.True(map.Count > 0, $"{language} 词表为空");
            var missing = baseline.Keys.Where(e => !map.ContainsKey(e)).ToArray();
            var extra = map.Keys.Where(e => !baseline.ContainsKey(e)).ToArray();
            Assert.True(missing.Length == 0, $"{language} 缺少键：{String.Join(", ", missing.Take(5))}");
            Assert.True(extra.Length == 0, $"{language} 多余键：{String.Join(", ", extra.Take(5))}");

            foreach (var item in map)
            {
                Assert.False(String.IsNullOrWhiteSpace(item.Value), $"{language} 空译文：{item.Key}");
                Assert.DoesNotContain('<', item.Value);
                Assert.DoesNotContain('>', item.Value);
                Assert.DoesNotContain('&', item.Value);
                // 词条直接替换进 HTML 文本、属性与脚本字符串，引号/反引号/转义符/模板插值会破坏页面
                Assert.DoesNotContain('\'', item.Value);
                Assert.DoesNotContain('"', item.Value);
                Assert.DoesNotContain('`', item.Value);
                Assert.DoesNotContain('\\', item.Value);
                Assert.DoesNotContain("${", item.Value);
            }
        }

        // 英文基准词表的每个键都必须出现在英文源文页面中，避免出现死条目
        using var stream = assembly.GetManifestResourceStream($"{prefix}.index.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();
        foreach (var key in baseline.Keys)
        {
            Assert.Contains(key, html);
        }

        // 英文源文除注释外不得出现中文，确保所有界面文案都以英文记录
        html = System.Text.RegularExpressions.Regex.Replace(html, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        html = System.Text.RegularExpressions.Regex.Replace(html, "/\\*.*?\\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        html = System.Text.RegularExpressions.Regex.Replace(html, "(?m)^\\s*//.*$", "");
        Assert.DoesNotMatch("[\\u4e00-\\u9fff]", html);

        // 简体中文不是源文，必须有独立词表
        var zhHans = UiLocalization.LoadDictionary(assembly, prefix, "zh-Hans");
        Assert.Equal("📊 服务状态", zhHans["📊 Service status"]);
    }

    [Fact]
    [DisplayName("Web面板_界面词表_单次替换_译文不被二次替换")]
    public void UiLocalization_ReplacesOnce_NoDoubleTranslation()
    {
        var assembly = typeof(AgentWebPanel).Assembly;
        const String prefix = "NewLife.Agent.WebPanel.wwwroot";
        using var stream = assembly.GetManifestResourceStream($"{prefix}.index.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        // pt 的 Confirm 译作 Confirmar，若译文再次参与替换会得到 Confirmarar
        var page = UiLocalization.Localize(html, assembly, prefix, CultureInfo.GetCultureInfo("pt-BR"));

        Assert.Contains("Confirmar nova senha", page);
        Assert.DoesNotContain("Confirmarar", page);
        Assert.Contains("<html lang=\"pt\">", page);

        // 界面词条不能命中脚本标识符中的同名片段
        Assert.Contains("d.totalMemory", page);
        Assert.Contains("function freeMemory", page);
        Assert.Contains("changeLanguage", page);
        Assert.Contains("oldPassword", page);
    }

    [Fact]
    [DisplayName("Web面板_全部语言词表_逐一套用渲染通过")]
    public void UiLocalization_AllLanguages_Render()
    {
        var assembly = typeof(AgentWebPanel).Assembly;
        const String prefix = "NewLife.Agent.WebPanel.wwwroot";
        using var stream = assembly.GetManifestResourceStream($"{prefix}.index.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();
        const String sentinel = "📊 Service status";

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith($"{prefix}.i18n.") || !name.EndsWith(".json")) continue;

            var language = name[(name.IndexOf(".i18n.", StringComparison.Ordinal) + 6)..^5];
            if (language == "en") continue;

            var map = UiLocalization.LoadDictionary(assembly, prefix, language);
            Assert.False(String.IsNullOrWhiteSpace(map[sentinel]), $"{language} 缺少哨兵译文");

            var page = UiLocalization.Localize(html, assembly, prefix, new CultureInfo(language));
            Assert.Contains($"<html lang=\"{language}\"", page);
            Assert.Contains(map[sentinel], page);
            Assert.DoesNotContain(sentinel, page);

            if (language is "ar" or "he" or "fa") Assert.Contains("dir=\"rtl\"", page);
        }
    }

    [Fact]
    [DisplayName("Web面板_语言状态注入_供前端首屏使用")]
    public async Task WebPanel_Index_InjectsLanguageState()
    {
        using var scope = new SettingScope();
        var panelSet = Setting.Current;
        var prevPort = panelSet.WebPort;
        var prevEnable = panelSet.EnableWebPanel;
        var coreSet = NewLife.Setting.Current;
        var prevLanguage = coreSet.Language;
        try
        {
            panelSet.WebPort = 0;
            panelSet.EnableWebPanel = false;
            coreSet.Language = "";
            coreSet.Save();
            Localization.Culture = CultureInfo.GetCultureInfo("en-US");

            var svc = new Mock<ServiceBase> { CallBase = true }.Object;
            svc.ServiceName = "LangState";
            var panel = new AgentWebPanel(svc) { Level = AuthLevel.None };
            panel.Start();
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{panel.Port}") };
                var html = await client.GetStringAsync("/");
                Assert.Contains("window.__panelLanguage={selected:'',effective:'en-US',forced:false}", html);
            }
            finally
            {
                panel.Stop("TestDone");
            }
        }
        finally
        {
            panelSet.WebPort = prevPort;
            panelSet.EnableWebPanel = prevEnable;
            coreSet.Language = prevLanguage;
            coreSet.Save();
            Localization.Culture = null;
        }
    }

    [Fact]
    [DisplayName("应用标题_仅显示时翻译_原始配置不变")]
    public void DisplayTitle_TranslatesForDisplayOnly()
    {
        try
        {
            UseCulture("zh-Hans");
            var svc = new ServiceBaseTester { DisplayName = "Cross-platform Service Agent" };

            Assert.Equal("跨平台服务守护", svc.DisplayTitle);
            Assert.Equal("Cross-platform Service Agent", svc.DisplayName);

            // 用户自定义名称没有词条时原样返回
            svc.DisplayName = "My Company Agent";
            Assert.Equal("My Company Agent", svc.DisplayTitle);
        }
        finally
        {
            ResetCulture();
        }
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
