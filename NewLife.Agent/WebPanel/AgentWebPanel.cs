#if !NET40
using System.Diagnostics;
using System.Net;
using System.Reflection;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Net;
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
        MapApi("/api/config/metadata", ProcessConfigMetadata);
        MapApi("/api/config/update", ProcessUpdateConfig);
        MapApi("/api/logs", ProcessLogs);
        MapApi("/api/logs/files", ProcessLogFiles);
        MapApi("/api/health", ProcessHealth);
        MapApi("/api/watchdog", ProcessWatchDog);
        MapApi("/api/extensions", ProcessExtensions);

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
        var p = Process.GetCurrentProcess();
        var uptime = DateTime.Now - p.StartTime;
        var mi = MachineInfo.Current ?? MachineInfo.GetCurrent();

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
                startTime = p.StartTime.ToString("MM-dd HH:mm:ss"),
                hostMachine = Environment.MachineName,
                platform = mi.OSName ?? Environment.OSVersion.Platform.ToString(),
                osVersion = mi.OSVersion,
                cpuName = mi.Processor,
                cpuCount = Environment.ProcessorCount,
                cpuRate = mi.CpuRate > 0 ? mi.CpuRate.ToString("F1") : "",
                totalMemory = mi.Memory > 0 ? $"{mi.Memory / 1024 / 1024 / 1024} GB" : "",
                availableMemory = mi.AvailableMemory > 0 ? $"{mi.AvailableMemory / 1024 / 1024 / 1024} GB" : "",
                freeMemory = mi.FreeMemory > 0 ? $"{mi.FreeMemory / 1024 / 1024 / 1024} GB" : "",
                board = mi.Board,
                machineGuid = mi.Guid,
                uplinkSpeed = mi.UplinkSpeed > 0 ? FormatSpeed(mi.UplinkSpeed) : "",
                downlinkSpeed = mi.DownlinkSpeed > 0 ? FormatSpeed(mi.DownlinkSpeed) : "",
                hostUptime = TimeSpan.FromMilliseconds(Runtime.TickCount64).ToString(@"d\.hh\:mm\:ss"),
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
                // 仅设置Running=false让服务循环退出，不停止WebPanel（否则无法再操作）
                Service.Running = false;
                WriteJson(ctx, new { code = 0, message = "服务正在停止，Web面板仍可用" });
                break;

            case "start":
                XTrace.WriteLine("Web面板触发服务启动");
                // 通过Host重启进程来重新启动服务
                Service.Host.Restart(Service.ServiceName);
                WriteJson(ctx, new { code = 0, message = "服务正在重启" });
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
        var count = !countStr.IsNullOrEmpty() ? countStr.ToInt(200) : 200;
        count = Math.Min(count, 1000);

        var file = GetQueryParam(ctx, "file");
        var level = GetQueryParam(ctx, "level");

        var lines = ReadLogLines(count, file, level);

        WriteJson(ctx, new { code = 0, data = new { fileName = file ?? "latest", count = lines.Count, lines } });
    }

    /// <summary>获取日志文件列表</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessLogFiles(IHttpContext ctx)
    {
        var list = new List<Object>();

        try
        {
            var set = NewLife.Setting.Current;
            var logDir = set.LogPath.GetFullPath();
            if (Directory.Exists(logDir))
            {
                foreach (var file in Directory.GetFiles(logDir, "*.log").OrderByDescending(f => f))
                {
                    var fi = new FileInfo(file);
                    list.Add(new
                    {
                        name = fi.Name,
                        size = fi.Length,
                        sizeDisplay = fi.Length.ToGMK(),
                        lastModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        WriteJson(ctx, new { code = 0, data = new { files = list } });
    }

    /// <summary>获取配置元数据（DisplayName + Description + Type + Value），供前端三列布局渲染</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessConfigMetadata(IHttpContext ctx)
    {
        var set = Setting.Current;
        var items = new List<Object>();

        foreach (var prop in typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (prop.Name.EqualIgnoreCase(nameof(set.WebPassword))) continue;

            var displayName = prop.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>()?.DisplayName;
            if (displayName.IsNullOrEmpty())
            {
                var desc = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
                // 取第一句作为简短显示名
                if (!desc.IsNullOrEmpty())
                {
                    var dot = desc.IndexOf('。');
                    displayName = dot > 0 ? desc[..dot] : desc;
                }
            }
            displayName ??= prop.Name;

            var description = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? "";

            var typeName = prop.PropertyType.Name switch
            {
                "String" => "String",
                "Int32" => "Int32",
                "Boolean" => "Boolean",
                _ => prop.PropertyType.Name
            };

            items.Add(new
            {
                name = prop.Name,
                displayName,
                description,
                type = typeName,
                value = prop.GetValue(set, null)
            });
        }

        WriteJson(ctx, new { code = 0, data = new { items } });
    }

    /// <summary>获取扩展面板列表</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void ProcessExtensions(IHttpContext ctx)
    {
        var extensions = GetExtensions();
        WriteJson(ctx, new { code = 0, data = new { panels = extensions } });
    }

    /// <summary>获取用户注册的扩展面板列表，子类重写以添加自定义面板</summary>
    /// <returns>扩展面板列表</returns>
    protected virtual List<PanelExtension> GetExtensions() => [];

    /// <summary>从RequestUri中获取查询参数</summary>
    /// <param name="ctx">Http上下文</param>
    /// <param name="name">参数名</param>
    /// <returns>参数值，不存在返回null</returns>
    protected static String GetQueryParam(IHttpContext ctx, String name)
    {
        var query = ctx.Request.RequestUri?.OriginalString;
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
    /// <param name="fileName">指定日志文件名，null或空则读取最新文件</param>
    /// <param name="level">可选日志级别过滤（INFO/WARN/ERROR/DEBUG）</param>
    /// <returns></returns>
    private List<String> ReadLogLines(Int32 count, String fileName = null, String level = null)
    {
        var list = new List<String>();

        try
        {
            // 尝试多个可能的日志目录路径
            var logDir = NewLife.Setting.Current.LogPath.GetFullPath();
            if (logDir == null) return list;

            String logFile;
            if (!fileName.IsNullOrEmpty())
            {
                // 安全性：仅取文件名，防止路径穿越
                var safeName = Path.GetFileName(fileName);
                logFile = Path.Combine(logDir, safeName);
                if (!File.Exists(logFile)) logFile = null;
            }
            else
            {
                logFile = Directory.GetFiles(logDir, "*.log")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();
            }

            if (logFile == null) return list;

            // 读取末尾N行（大文件时全量加载性能可接受，后续可改为反向seek）
            var allLines = File.ReadAllLines(logFile);
            var start = Math.Max(0, allLines.Length - count);
            for (var i = start; i < allLines.Length; i++)
            {
                var line = allLines[i];
                // 级别过滤
                if (!level.IsNullOrEmpty())
                {
                    if (!line.Contains(level, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                list.Add(line);
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
        var p = Process.GetCurrentProcess();
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
            var processes = Process.GetProcessesByName(serviceName);
            if (processes.Length > 0) return true;
        }
        catch { }
        return null;
    }
    #endregion

    #region 辅助
    /// <summary>格式化文件大小为可读字符串</summary>
    /// <param name="bytes">字节数</param>
    /// <returns></returns>
    protected static String FormatFileSize(Int64 bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>格式化网络速率为可读字符串</summary>
    /// <param name="bps">比特每秒</param>
    /// <returns></returns>
    protected static String FormatSpeed(UInt64 bps)
    {
        return bps switch
        {
            < 1000UL => $"{bps} bps",
            < 1000_000UL => $"{bps / 1000.0:F1} Kbps",
            < 1000_000_000UL => $"{bps / 1000_000.0:F1} Mbps",
            _ => $"{bps / 1000_000_000.0:F2} Gbps"
        };
    }

    /// <summary>写JSON响应</summary>
    /// <param name="ctx">Http上下文</param>
    /// <param name="data">数据对象</param>
    protected static void WriteJson(IHttpContext ctx, Object data)
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
