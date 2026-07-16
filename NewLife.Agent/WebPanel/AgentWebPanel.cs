#if !NET40
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.Agent.WebPanel;

/// <summary>Web面板鉴权级别</summary>
public enum AuthLevel
{
    /// <summary>不鉴权</summary>
    None,

    /// <summary>本地免鉴权，远程需鉴权</summary>
    LocalOnly,

    /// <summary>全部鉴权</summary>
    Full
}

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

    /// <summary>鉴权级别</summary>
    public AuthLevel Level { get; set; }

    /// <summary>用户名</summary>
    public String UserName { get; set; }

    /// <summary>密码</summary>
    public String Password { get; set; }

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

        Level = ParseAuthLevel(set.WebAuthLevel);
        UserName = set.WebUserName;
        Password = set.WebPassword;

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

    #region Token管理
    /// <summary>令牌信息</summary>
    private class TokenInfo
    {
        public String User { get; set; } = "";
        public DateTime Expire { get; set; }
    }

    private static readonly Dictionary<String, TokenInfo> _tokens = [];
    private static readonly Object _lock = new();

    /// <summary>签发Token</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>Token字符串，失败返回null</returns>
    public String IssueToken(String user, String password)
    {
        if (user.IsNullOrEmpty() || password.IsNullOrEmpty()) return null;

        if (UserName.IsNullOrEmpty() || Password.IsNullOrEmpty()) return null;

        if (!user.EqualIgnoreCase(UserName)) return null;
        if (password != Password) return null;

        var token = Guid.NewGuid().ToString("N");
        var info = new TokenInfo
        {
            User = user,
            Expire = DateTime.Now.AddHours(24)
        };

        lock (_lock)
        {
            // 清理过期Token
            var expired = _tokens.Where(e => e.Value.Expire < DateTime.Now).Select(e => e.Key).ToList();
            foreach (var key in expired)
                _tokens.Remove(key);

            _tokens[token] = info;
        }

        return token;
    }

    /// <summary>验证Token</summary>
    /// <param name="token">Token字符串</param>
    /// <returns></returns>
    public Boolean ValidateToken(String token)
    {
        if (token.IsNullOrEmpty()) return false;

        lock (_lock)
        {
            if (!_tokens.TryGetValue(token, out var info)) return false;

            if (info.Expire < DateTime.Now)
            {
                _tokens.Remove(token);
                return false;
            }

            return true;
        }
    }

    /// <summary>吊销Token（退出登录）</summary>
    /// <param name="token">Token字符串</param>
    public void RevokeToken(String token)
    {
        if (token.IsNullOrEmpty()) return;

        lock (_lock)
        {
            _tokens.Remove(token);
        }
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
    /// <summary>嵌入式静态文件的资源命名空间前缀。子类重写以使用自己的静态文件</summary>
    protected virtual String EmbeddedResourcePrefix => "NewLife.Agent.WebPanel.wwwroot";

    /// <summary>嵌入式静态文件所在程序集的标记类型。子类重写以使用自己程序集中的嵌入资源</summary>
    protected virtual Type EmbeddedResourceAssemblyType => typeof(AgentWebPanel);

    /// <summary>注册所有路由</summary>
    /// <remarks>
    /// 路由匹配规则：精确匹配优先于通配符。
    /// /api/* 通配匹配 → ApiController（控制器自行鉴权，Login 方法不鉴权）
    /// /* 降级匹配 → 静态文件
    /// 
    /// 子类可重写此方法并在 base.RegisterRoutes() 之后追加自定义控制器路由，
    /// 例如 Server.MapController&lt;MyController&gt;("/api/my")。
    /// </remarks>
    protected virtual void RegisterRoutes()
    {
        // API 控制器路由（自动处理 /api/* 通配，控制器自行鉴权）
        Server.MapController<ApiController>("/api");

        // 静态文件（根路径，API 路由优先匹配；传 "/" 让 MapEmbedded 内部转为 "/*" 通配）
        Server.MapEmbedded("/", EmbeddedResourcePrefix, EmbeddedResourceAssemblyType.Assembly);
    }
    #endregion

    #region 扩展面板
    /// <summary>获取用户注册的扩展面板列表，子类重写以添加自定义面板</summary>
    /// <returns>扩展面板列表</returns>
    protected internal virtual List<PanelExtension> GetExtensions() => [];
    #endregion

    #region 运行时凭据更新
    /// <summary>运行时更新凭据，立即生效无需重启</summary>
    /// <param name="user">新用户名，null 或空则不更新</param>
    /// <param name="password">新密码，null 或空则不更新</param>
    public void UpdateCredentials(String user, String password)
    {
        if (!user.IsNullOrEmpty()) UserName = user;
        if (!password.IsNullOrEmpty()) Password = password;
    }
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
#endif
