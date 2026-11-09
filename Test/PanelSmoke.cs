using System;
using System.Collections.Generic;
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
        set.WebPassword = "admin";

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
        var body = new StringContent("{\"user\":\"admin\",\"password\":\"admin\"}", Encoding.UTF8, "application/json");
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

        // 语言切换：写入 NewLife.Setting 并落盘 Core.config（AotRoots 保留全局配置成员）
        var langBody = new StringContent("{\"language\":\"de\"}", Encoding.UTF8, "application/json");
        var lang = http.PostAsync("api/SetLanguage", langBody).Result.Content.ReadAsStringAsync().Result;
        if (!lang.Contains("\"code\":0")) throw new Exception("语言切换失败：" + lang);

        var page = http.GetStringAsync("").Result;
        if (!page.Contains("<html lang=\"de\">")) throw new Exception("语言切换后首页未按德语输出");
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
