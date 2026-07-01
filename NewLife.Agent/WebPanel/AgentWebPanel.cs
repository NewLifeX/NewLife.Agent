#if !NET40
using System.Net;
using System.Reflection;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Agent.WebPanel;

/// <summary>Agent Web管理面板</summary>
/// <remarks>基于 HttpServer 提供轻量级 Web 管理界面，支持服务状态查看、启停控制、配置管理、日志查看等功能</remarks>
public class AgentWebPanel
{
    #region 属性
    /// <summary>Http服务器</summary>
    public HttpServer Server { get; }

    /// <summary>所属服务</summary>
    public ServiceBase Service { get; }

    /// <summary>鉴权处理器</summary>
    public AuthHandler Auth { get; }

    /// <summary>是否运行中</summary>
    public Boolean Active => Server.Active;

    /// <summary>实际监听端口（启动后回写）</summary>
    public Int32 Port => Server.Port;
    #endregion

    #region 构造
    /// <summary>实例化Web管理面板</summary>
    /// <param name="service">所属服务</param>
    public AgentWebPanel(ServiceBase service)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));

        var set = Setting.Current;
        var port = set.WebPort > 0 ? set.WebPort : 0;

        Server = new HttpServer
        {
            Port = port,
            ServerName = $"NewLife-Agent/{Service.ServiceName}",
            Log = XTrace.Log
        };

        Auth = new AuthHandler(null!)
        {
            Level = ParseAuthLevel(set.WebAuthLevel),
            UserName = set.WebUserName,
            Password = set.WebPassword
        };

        RegisterRoutes();
    }

    /// <summary>解析鉴权级别字符串</summary>
    /// <param name="level">鉴权级别字符串</param>
    /// <returns>鉴权级别枚举</returns>
    public static AuthLevel ParseAuthLevel(String level)
    {
        return level?.ToLower() switch
        {
            "none" => AuthLevel.None,
            "full" => AuthLevel.Full,
            _ => AuthLevel.LocalOnly
        };
    }
    #endregion

    #region 启动停止
    /// <summary>启动Web面板</summary>
    public void Start()
    {
        if (Server.Active) return;

        Server.Start();

        XTrace.WriteLine("Web管理面板已启动，端口：{0}", Server.Port);
    }

    /// <summary>停止Web面板</summary>
    /// <param name="reason">停止原因</param>
    public void Stop(String reason)
    {
        if (!Server.Active) return;

        Server.Stop(reason ?? "ServiceStop");

        XTrace.WriteLine("Web管理面板已停止：{0}", reason);
    }
    #endregion

    #region 路由注册
    /// <summary>注册所有路由</summary>
    protected virtual void RegisterRoutes()
    {
        // 登录接口（不经过鉴权）
        Server.Map("/api/login", (IHttpContext ctx) =>
        {
            var body = ctx.Request.Body;
            if (body == null)
            {
                WriteJson(ctx, new { code = 401, message = "Missing credentials" });
                return;
            }

            var json = body.ToStr();
            var dic = JsonHelper.DecodeJson(json) as IDictionary<String, Object>;
            var user = dic.TryGetValue("user", out var u) ? u + "" : "";
            var pass = dic.TryGetValue("password", out var p) ? p + "" : "";

            var token = Auth.IssueToken(user, pass);
            if (token == null)
            {
                ctx.Response.StatusCode = HttpStatusCode.Unauthorized;
                WriteJson(ctx, new { code = 401, message = "Invalid credentials" });
                return;
            }

            WriteJson(ctx, new { code = 0, data = new { token } });
        });

        // API 路由（经 AuthHandler 鉴权包装）
        MapApi("/api/status", ProcessStatus);
        MapApi("/api/control", ProcessControl);
        MapApi("/api/config", ProcessGetConfig);
        MapApi("/api/config/update", ProcessUpdateConfig);
        MapApi("/api/logs", ProcessLogs);
        MapApi("/api/health", ProcessHealth);
        MapApi("/api/watchdog", ProcessWatchDog);

        // 静态文件（根路径，API 路由优先匹配）
        Server.Map("/*", new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" });
    }

    private void MapApi(String path, HttpProcessDelegate handler)
    {
        // 每个API路由使用独立的AuthHandler拷贝，共享同一鉴权状态
        var auth = new AuthHandler(new DelegateHandler { Callback = handler })
        {
            Level = Auth.Level,
            UserName = Auth.UserName,
            Password = Auth.Password
        };
        Server.Map(path, auth);
    }
    #endregion

    #region API 处理
    /// <summary>获取服务状态</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessStatus(IHttpContext ctx)
    {
        var p = System.Diagnostics.Process.GetCurrentProcess();
        var uptime = DateTime.Now - p.StartTime;

        WriteJson(ctx, new
        {
            code = 0,
            data = new
            {
                serviceName = Service.ServiceName,
                displayName = Service.DisplayName,
                description = Service.Description,
                running = Service.Running,
                uptime = uptime.ToString(@"d\.hh\:mm\:ss"),
                uptimeSeconds = (Int64)uptime.TotalSeconds,
                processId = p.Id,
                memoryMB = p.WorkingSet64 / 1024 / 1024,
                threadCount = p.Threads.Count,
                handleCount = p.HandleCount,
                startTime = p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                hostMachine = Environment.MachineName,
                platform = Environment.OSVersion.Platform.ToString(),
                port = Server.Port
            }
        });
    }

    /// <summary>服务控制（启停重启）</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessControl(IHttpContext ctx)
    {
        var body = ctx.Request.Body?.ToStr();
        if (body.IsNullOrEmpty())
        {
            WriteJson(ctx, new { code = 400, message = "Missing action" });
            return;
        }

        var dic = JsonHelper.DecodeJson(body) as IDictionary<String, Object>;
        var action = dic.TryGetValue("action", out var v) ? v + "" : "";

        switch (action.ToLower())
        {
            case "stop":
                XTrace.WriteLine("Web面板触发服务停止");
                Service.StopWork("WebPanel");
                WriteJson(ctx, new { code = 0, message = "服务正在停止" });
                break;

            case "start":
                XTrace.WriteLine("Web面板触发服务启动");
                Service.StartWork("WebPanel");
                WriteJson(ctx, new { code = 0, message = "服务已启动" });
                break;

            case "restart":
                XTrace.WriteLine("Web面板触发服务重启");
                Service.Host.Restart(Service.ServiceName);
                WriteJson(ctx, new { code = 0, message = "服务正在重启" });
                break;

            default:
                WriteJson(ctx, new { code = 400, message = $"Unknown action: {action}" });
                break;
        }
    }

    /// <summary>获取配置</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessGetConfig(IHttpContext ctx)
    {
        var set = Setting.Current;

        WriteJson(ctx, new
        {
            code = 0,
            data = new
            {
                set.ServiceName,
                set.DisplayName,
                set.Description,
                set.WatchInterval,
                set.FreeMemoryInterval,
                set.MaxMemory,
                set.MaxThread,
                set.MaxHandle,
                set.AutoRestart,
                set.RestartTimeRange,
                set.WatchDog,
                set.AfterStart,
                set.EnableWebPanel,
                set.WebPort,
                set.WebAuthLevel
                // 不返回密码
            }
        });
    }

    /// <summary>更新配置</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessUpdateConfig(IHttpContext ctx)
    {
        var body = ctx.Request.Body?.ToStr();
        if (body.IsNullOrEmpty())
        {
            WriteJson(ctx, new { code = 400, message = "Missing config" });
            return;
        }

        try
        {
            var set = Setting.Current;
            var updates = JsonHelper.DecodeJson(body) as IDictionary<String, Object>;

            if (updates != null)
            {
                foreach (var kv in updates)
                {
                    var prop = typeof(Setting).GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanWrite) continue;

                    // 安全字段白名单
                    if (kv.Key.EqualIgnoreCase(nameof(set.WebPassword))) continue;

                    var value = kv.Value;
                    if (value != null && value.GetType() != prop.PropertyType)
                        value = Reflect.ChangeType(value, prop.PropertyType);

                    prop.SetValue(set, value, null);
                }
            }

            set.Save();

            WriteJson(ctx, new { code = 0, message = "配置已更新，部分配置需重启服务后生效" });
        }
        catch (Exception ex)
        {
            WriteJson(ctx, new { code = 500, message = ex.Message });
        }
    }

    /// <summary>获取日志</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessLogs(IHttpContext ctx)
    {
        var countStr = GetQueryParam(ctx, "count");
        var count = !countStr.IsNullOrEmpty() ? countStr.ToInt(100) : 100;
        count = Math.Min(count, 500);

        var lines = ReadLogLines(count);

        WriteJson(ctx, new { code = 0, data = new { count = lines.Count, lines } });
    }

    /// <summary>从RequestUri中获取查询参数</summary>
    private static String GetQueryParam(IHttpContext ctx, String name)
    {
        var query = ctx.Request.RequestUri != null ? ctx.Request.RequestUri.Query : null;
        if (query.IsNullOrEmpty()) return null;

        // 简单查询参数解析：?key=value&key2=value2
        var items = query.TrimStart('?').Split('&');
        foreach (var item in items)
        {
            var kv = item.Split('=');
            if (kv.Length >= 2 && kv[0].EqualIgnoreCase(name))
                return Uri.UnescapeDataString(kv[1]);
        }

        return null;
    }

    /// <summary>读取日志文件末尾行</summary>
    /// <param name="count">行数</param>
    /// <returns></returns>
    private List<String> ReadLogLines(Int32 count)
    {
        var list = new List<String>();

        try
        {
            // 查找最新日志文件
            var logDir = "Log".GetFullPath();
            if (!Directory.Exists(logDir)) return list;

            var logFile = Directory.GetFiles(logDir, "*.log")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (logFile == null) return list;

            // 读取末尾N行
            var allLines = File.ReadAllLines(logFile);
            var start = Math.Max(0, allLines.Length - count);
            for (var i = start; i < allLines.Length; i++)
            {
                list.Add(allLines[i]);
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return list;
    }

    /// <summary>获取健康指标</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessHealth(IHttpContext ctx)
    {
        var p = System.Diagnostics.Process.GetCurrentProcess();
        var set = Setting.Current;

        WriteJson(ctx, new
        {
            code = 0,
            data = new
            {
                memoryMB = p.WorkingSet64 / 1024 / 1024,
                memoryLimitMB = set.MaxMemory,
                threadCount = p.Threads.Count,
                threadLimit = set.MaxThread,
                handleCount = p.HandleCount,
                handleLimit = set.MaxHandle,
                totalProcessorTime = p.TotalProcessorTime.TotalSeconds.ToString("F1"),
                privilegedProcessorTime = p.PrivilegedProcessorTime.TotalSeconds.ToString("F1"),
                userProcessorTime = p.UserProcessorTime.TotalSeconds.ToString("F1"),
                gcTotalMemory = GC.GetTotalMemory(false) / 1024 / 1024,
                gcCollections = new
                {
                    gen0 = GC.CollectionCount(0),
                    gen1 = GC.CollectionCount(1),
                    gen2 = GC.CollectionCount(2)
                }
            }
        });
    }

    /// <summary>获取看门狗状态</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessWatchDog(IHttpContext ctx)
    {
        var set = Setting.Current;
        var dogServices = set.WatchDog?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !s.IsNullOrEmpty())
            .ToList() ?? [];

        var statuses = new List<Object>();
        foreach (var svc in dogServices)
        {
            var running = CheckServiceStatus(svc);

            statuses.Add(new { name = svc, running });
        }

        WriteJson(ctx, new { code = 0, data = new { services = statuses } });
    }

    /// <summary>检查系统服务状态</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否运行中，无法检查时返回null表示未知</returns>
    protected virtual Boolean? CheckServiceStatus(String serviceName)
    {
        // 使用进程名匹配做简易检查，避免依赖 System.ServiceProcess 程序集
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(serviceName);
            if (processes.Length > 0) return true;
        }
        catch { }
        return null;
    }
    #endregion

    #region 辅助
    /// <summary>写JSON响应</summary>
    /// <param name="ctx">Http上下文</param>
    /// <param name="data">数据对象</param>
    private static void WriteJson(IHttpContext ctx, Object data)
    {
        var json = JsonHelper.ToJson(data);
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Body = new ArrayPacket(json.GetBytes());
    }
    #endregion
}

/// <summary>内嵌资源文件处理器</summary>
public class EmbeddedFileHandler : IHttpHandler
{
    /// <summary>路径前缀，如 /panel/</summary>
    public String Prefix { get; set; } = "";

    /// <summary>资源名前缀</summary>
    public String ContentPath { get; set; } = "";

    private static readonly Dictionary<String, String> _mimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
    };

    /// <summary>处理请求</summary>
    /// <param name="ctx">Http上下文</param>
    public void ProcessRequest(IHttpContext ctx)
    {
        var file = ctx.Path;
        if (file.StartsWithIgnoreCase(Prefix))
            file = file[Prefix.Length..];

        if (file.IsNullOrEmpty() || file == "/")
            file = "index.html";

        // 安全：防止路径穿越
        if (file.Contains(".."))
        {
            ctx.Response.StatusCode = HttpStatusCode.NotFound;
            return;
        }

        var resourceName = $"{ContentPath}.{file.Replace('/', '.')}";
        var asm = Assembly.GetExecutingAssembly();

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            ctx.Response.StatusCode = HttpStatusCode.NotFound;
            return;
        }

        var ext = Path.GetExtension(file) ?? "";
        var contentType = _mimeTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
        ctx.Response.ContentType = contentType;

        using var ms2 = new MemoryStream();
        stream.CopyTo(ms2);
        ctx.Response.Body = new ArrayPacket(ms2.ToArray());
    }
}
#endif
