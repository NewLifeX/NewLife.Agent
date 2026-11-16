using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.WebPanel;
using NewLife.Serialization;

namespace Test;

/// <summary>Web 面板 NativeAOT 冒烟</summary>
/// <remarks>
/// AOT 发布件以 <c>-panel</c> 启动自检，覆盖面板在裁剪后的关键路径：
/// 内嵌首页与 i18n 词表、面板 DTO 的 JSON 源生成序列化、全局配置反射（语言切换写入 Core.config）。
/// musl 发布按 RID 固定 <c>InvariantGlobalization</c>，没有具体语言数据，此时只校验语言切换接口，
/// 跳过首页按目标语言渲染的断言。
/// 通过输出 <c>AOT_PANEL_PROBE=OK</c>，CI 的 aot 作业据此判定。
/// </remarks>
internal static class PanelSmoke
{
    /// <summary>启动面板并自检，返回进程退出码</summary>
    /// <returns>0 表示全部通过</returns>
    public static Int32 Run()
    {
        var set = NewLife.Agent.Setting.Current;
        set.EnableWebPanel = true;
        // 端口交给系统分配，避免 CI 机器上固定端口被占用
        set.WebPort = 0;
        set.WebBind = "127.0.0.1";
        // 显式设置非默认口令：默认 admin 会在启动时被随机口令替换，冒烟无法再用 admin 登录
        set.WebPassword = "admin123";

        var svc = new MyServices();
        var panel = new AgentWebPanel(svc);
        panel.Start();

        try
        {
            Check(panel.Port);

            Console.WriteLine("AOT_PANEL_PROBE=OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AOT_PANEL_PROBE=FAIL({ex.GetType().Name}: {ex.Message})");
            return 1;
        }
        finally
        {
            panel.Stop("AOT smoke");
        }
    }

    private static void Check(Int32 port)
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(30) };

        // 首页：内嵌资源 + i18n 词典替换 + 服务端注入面板语言状态
        var html = http.GetStringAsync("").Result;
        if (!html.Contains("window.__panelLanguage")) throw new Exception("首页缺少面板语言注入");
        if (!html.Contains("<html lang=")) throw new Exception("首页缺少 lang 标记");

        // 登录：响应 DTO 走 JsonHelper 源生成成员访问器
        var body = new StringContent("{\"user\":\"admin\",\"password\":\"admin123\"}", Encoding.UTF8, "application/json");
        var login = http.PostAsync("api/Login", body).Result.Content.ReadAsStringAsync().Result;
        var token = GetToken(login);
        if (token.IsNullOrEmpty()) throw new Exception("登录失败：" + login);

        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Status/ConfigMetadata/Languages：面板 DTO 序列化 + Setting 反射枚举 + 语言列表
        foreach (var api in new[] { "api/Status", "api/ConfigMetadata", "api/Languages" })
        {
            var text = http.GetStringAsync(api).Result;
            if (!text.Contains("\"code\":0")) throw new Exception(api + " 返回异常：" + text);
        }

        // 首次设置状态：公开接口且 DTO 走源生成序列化；正常模式下必须已初始化
        var setup = http.GetStringAsync("api/SetupStatus").Result;
        if (!setup.Contains("\"needsSetup\":false")) throw new Exception("SetupStatus 返回异常：" + setup);

        // 语言切换：写入 NewLife.Setting 并落盘 Core.config（AotRoots 保留全局配置成员）
        var langBody = new StringContent("{\"language\":\"de\"}", Encoding.UTF8, "application/json");
        var lang = http.PostAsync("api/SetLanguage", langBody).Result.Content.ReadAsStringAsync().Result;
        if (!lang.Contains("\"code\":0")) throw new Exception("语言切换失败：" + lang);

        // 不变全球化（musl AOT 按 RID 固定 InvariantGlobalization）没有具体语言数据，
        // 切换只落盘、页面仍按英文输出，故仅跳过渲染断言，接口与落盘照常校验
        if (!IsInvariantGlobalization())
        {
            var page = http.GetStringAsync("").Result;
            if (!page.Contains("<html lang=\"de\">")) throw new Exception("语言切换后首页未按德语输出");
        }
        else
            Console.WriteLine("[PanelSmoke] 不变全球化：跳过首页按目标语言渲染的断言");
    }

    /// <summary>当前是否处于全球化不变模式</summary>
    /// <remarks>InvariantGlobalization 下 CultureInfo 取具体语言会抛异常，据此判断</remarks>
    private static Boolean IsInvariantGlobalization()
    {
        if (AppContext.TryGetSwitch("System.Globalization.Invariant", out var invariant) && invariant) return true;

        try
        {
            return CultureInfo.GetCultureInfo("de").Name.IsNullOrEmpty();
        }
        catch (CultureNotFoundException)
        {
            return true;
        }
    }

    private static String GetToken(String json)
    {
        if (JsonParser.Decode(json) is not IDictionary<String, Object> dic) return null;
        if (!dic.TryGetValue("data", out var data)) return null;
        if (data is not IDictionary<String, Object> dataDic) return null;
        if (!dataDic.TryGetValue("token", out var token)) return null;

        return token + "";
    }
}
