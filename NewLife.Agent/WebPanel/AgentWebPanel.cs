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
    /// <summary>当前Web面板实例（供控制器访问）</summary>
    public static AgentWebPanel Current { get; private set; }

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
        Current = this;

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
    /// <remarks>
    /// 路由匹配规则：精确匹配优先于通配符。
    /// /api/login 精确匹配（不走鉴权）→ ApiController.Login()
    /// /api/* 通配匹配（经 AuthHandler 鉴权）→ ApiController 其他方法
    /// /* 降级匹配 → 静态文件
    /// </remarks>
    protected virtual void RegisterRoutes()
    {
        // 登录接口：精确路由，不走鉴权（先注册，精确匹配优先于后面的 /api/* 通配）
        Server.Map("/api/login", new AgentControllerHandler { ControllerType = typeof(ApiController) });

        // API 控制器路由：经 AuthHandler 鉴权包装
        // AgentControllerHandler 自动将路径 /api/{MethodName} 映射到 ApiController 的对应方法
        var controllerHandler = new AgentControllerHandler { ControllerType = typeof(ApiController) };
        var authHandler = new AuthHandler(controllerHandler)
        {
            Level = Auth.Level,
            UserName = Auth.UserName,
            Password = Auth.Password
        };
        Server.Map("/api/*", authHandler);

        // 静态文件（根路径，API 路由优先匹配）
        Server.Map("/*", new EmbeddedFileHandler { Prefix = "/", ContentPath = "NewLife.Agent.WebPanel.wwwroot" });
    }
    #endregion

    #region 扩展面板
    /// <summary>获取用户注册的扩展面板列表，子类重写以添加自定义面板</summary>
    /// <returns>扩展面板列表</returns>
    protected internal virtual List<PanelExtension> GetExtensions() => [];
    #endregion

    #region 辅助
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
