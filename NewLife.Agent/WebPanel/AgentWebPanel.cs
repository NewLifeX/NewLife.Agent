#if !NET40
using System.Net;
using System.Globalization;
using System.Security.Authentication;
using System.Text;
using NewLife.Agent.WebPanel.Security;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using NewLife;

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

    /// <summary>口令哈希提供者。默认加盐 SHA512，可在启动前替换为更强的算法实现</summary>
    public IPasswordProvider PasswordProvider { get; set; } = new SaltPasswordProvider();

    /// <summary>当前口令是否仍为默认值（空或 admin）</summary>
    public Boolean IsDefaultPassword
    {
        get
        {
            if (Password.IsNullOrEmpty()) return true;

            try
            {
                return PasswordProvider.Verify("admin", Password);
            }
            catch
            {
                // 历史哈希格式（如已废除的 PBKDF2 串）当前提供者无法识别，不按默认口令处理
                return false;
            }
        }
    }

    /// <summary>用户名</summary>
    public String UserName { get; set; }

    /// <summary>密码</summary>
    public String Password { get; set; }

    /// <summary>是否运行中</summary>
    public Boolean Active => Server.Active;

    /// <summary>实际监听端口（启动后回写）</summary>
    public Int32 Port => Server.Port;

    private String _indexHtml;
    #endregion

    #region 构造
    /// <summary>实例化Web管理面板</summary>
    /// <param name="service">所属服务</param>
    public AgentWebPanel(ServiceBase service)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
        Current = this;

        // AOT：保留响应 DTO 元数据（NativeAOT 下 JSON 序列化依赖属性可枚举性）
        AotRoots.Ensure();

        // 面板 JSON 键名统一 camelCase：显式固定 JsonOptions 命名策略（主流 Web 约定），
        // 未标注特性的 DTO（如 PanelExtension）同样按 CamelCase 输出，与前端 index.html 读取一致，防止全局默认策略变化导致契约漂移
        JsonHelper.Default.Options.PropertyNaming = PropertyNaming.CamelCase;

        var set = Setting.Current;
        var port = set.WebPort > 0 ? set.WebPort : 0;

        Server = new HttpServer
        {
            Port = port,
            ServerName = $"NewLife-Agent/{Service.ServiceName}",
            Log = XTrace.Log
        };

        // 绑定地址：默认仅本机访问；配置为“*”时监听所有网卡；无法识别的值回退本机，宁可连不上也不误开放
        var bind = set.WebBind;
        if (!bind.IsNullOrEmpty() && bind != "*" && !IPAddress.TryParse(bind, out _))
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Invalid WebBind value [{0}]; falling back to listening on loopback 127.0.0.1 only.", bind);

        var address = ParseBindAddress(bind);
        if (address != null) Server.Local.Address = address;

        UserName = set.WebUserName;
        Password = set.WebPassword;

        RegisterRoutes();
        UseAuthentication();
    }

    /// <summary>解析面板绑定地址</summary>
    /// <remarks>空值或无法识别的值回退 127.0.0.1 环回地址，避免配置笔误把管理面板误开放到局域网</remarks>
    /// <param name="bind">绑定地址。支持具体IP地址，或 * 表示监听所有网卡</param>
    /// <returns>要绑定的地址；null 表示监听所有网卡</returns>
    public static IPAddress ParseBindAddress(String bind)
    {
        if (bind.IsNullOrEmpty()) return IPAddress.Loopback;

        bind = bind.Trim();
        if (bind == "*") return null;

        if (IPAddress.TryParse(bind, out var address)) return address.IsAny() ? null : address;

        return IPAddress.Loopback;
    }

    /// <summary>判断绑定地址是否仅本机回环</summary>
    /// <param name="bind">绑定地址。支持具体IP、* 表示所有网卡</param>
    /// <returns>是否仅本机回环</returns>
    public static Boolean IsLoopbackBind(String bind)
    {
        var address = ParseBindAddress(bind);

        return address != null && IPAddress.IsLoopback(address);
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
    public String IssueToken(String user, String password) => IssueToken(user, password, null);

    /// <summary>签发Token</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="remoteIp">客户端地址，仅用于审计</param>
    /// <returns>Token字符串，失败返回null</returns>
    public String IssueToken(String user, String password, String remoteIp)
    {
        if (user.IsNullOrEmpty() || password.IsNullOrEmpty()) return null;

        if (!Authenticate(user, password, remoteIp)) return null;

        // 历史明文口令在首次登录成功后升级为哈希
        MigrateLegacyPassword(password);

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
    /// <returns>Token是否有效</returns>
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

        if (!CheckSecurity()) return;

        Server.Start();

        var scheme = Server.SslProtocol != SslProtocols.None ? "https" : "http";
        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web management panel started on {0} port {1}.", scheme, Server.Port);

        // 仍在使用默认口令时给出警告，便于用户及时修改
        if (IsDefaultPassword)
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Security warning: the web panel is still using the default password. Change it from the panel or set WebPassword in the configuration file.");
    }

    /// <summary>启动前的安全检查。非本机监听且仍使用默认口令时拒绝启动</summary>
    /// <remarks>
    /// 面板具备服务控制与配置查看能力，等同于管理员权限。默认口令一旦暴露到网络，任何人都可以先登录并改掉口令。
    /// 需要远程访问时，应先修改口令，或改用反向代理/SSH隧道仅暴露本机回环端口。子类可重写本方法放开限制。
    /// </remarks>
    /// <returns>是否允许启动</returns>
    protected virtual Boolean CheckSecurity()
    {
        if (IsLoopbackBinding()) return true;

        if (!IsDefaultPassword) return true;

        XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Refusing to start: the web panel listens on a non-loopback address while still using the default password. Set WebPassword in the configuration file (plain text is accepted and upgraded to a hash after sign-in), or sign in on 127.0.0.1 and change it first.");

        return false;
    }

    /// <summary>当前绑定地址是否仅本机回环</summary>
    private Boolean IsLoopbackBinding()
    {
        var address = Server.Local.Address;

        return address != null && IPAddress.IsLoopback(address);
    }

    /// <summary>变更监听地址与端口并热重启Http服务器，无需重启应用</summary>
    /// <remarks>
    /// 先返回响应再重启，避免当前请求的连接被自己断开。重启失败时回滚到原有监听，配置不落盘。
    /// </remarks>
    /// <param name="bind">绑定地址。空或 * 表示监听所有网卡，其它值支持具体IP。null 表示不修改</param>
    /// <param name="port">端口，0 表示随机分配，null 表示不修改</param>
    /// <returns>是否已受理重启请求</returns>
    public Boolean RequestRebind(String bind, Int32? port)
    {
        if (Interlocked.CompareExchange(ref _rebinding, 1, 0) != 0)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "The web panel is already rebinding, ignore this request.");

            return false;
        }

        // 延迟重启：当前响应需要先写回客户端
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(800).ConfigureAwait(false);

                Rebind(bind, port);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _rebinding, 0);
            }
        });

        return true;
    }

    private Int32 _rebinding;

    /// <summary>执行监听地址与端口的热重启</summary>
    /// <param name="bind">绑定地址</param>
    /// <param name="port">端口</param>
    private void Rebind(String bind, Int32? port)
    {
        var oldAddress = Server.Local.Address;
        var oldPort = Server.Port;

        Server.Stop("Rebind");

        if (!bind.IsNullOrEmpty()) Server.Local.Address = ParseBindAddress(bind) ?? IPAddress.Any;
        if (port != null) Server.Port = port.Value > 0 ? port.Value : 0;

        try
        {
            Server.Start();
        }
        catch (Exception ex)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Failed to listen on the new address, rolling back: {0}", ex.Message);

            Server.Stop("RebindRollback");
            Server.Local.Address = oldAddress ?? IPAddress.Any;
            Server.Port = oldPort;

            try
            {
                Server.Start();
            }
            catch (Exception ex2)
            {
                XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Failed to restore the previous address, the web panel is unavailable until the service restarts: {0}", ex2.Message);

                throw;
            }

            return;
        }

        // 启动成功后再落盘，保证配置文件与实际监听一致
        var set = Setting.Current;
        if (!bind.IsNullOrEmpty()) set.WebBind = bind.Trim();
        set.WebPort = Server.Port;
        set.Save();

        var scheme = Server.SslProtocol != SslProtocols.None ? "https" : "http";
        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web management panel has moved to {0}://{1}:{2}.", scheme, Server.Local.Host, Server.Port);
    }

    /// <summary>停止Web面板</summary>
    /// <param name="reason">停止原因</param>
    public void Stop(String reason)
    {
        if (!Server.Active) return;

        Server.Stop(reason ?? "ServiceStop");

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web management panel has stopped: {0}", reason);
    }
    #endregion

    #region 请求鉴权
    /// <summary>注册默认全量鉴权中间件</summary>
    /// <remarks>
    /// 面板与下游扩展的全部接口默认要求登录，只有登录页、静态资源和少量公开接口例外。
    /// 这样下游追加的控制器（如 /star/*）无需自行实现鉴权，避免漏写导致管理员权限接口裸奔。
    /// </remarks>
    private void UseAuthentication() => Server.Use(async (ctx, next) =>
    {
        var request = ctx.Request;
        var (path, query) = SplitRequestUri(request);

        if (IsPublicPath(path) || IsStaticResource(path, request))
        {
            await next().ConfigureAwait(false);

            return;
        }

        if (ValidateToken(ExtractToken(request, query)))
        {
            await next().ConfigureAwait(false);

            return;
        }

        var json = JsonHelper.ToJson(new Models.ApiResult { Code = 401, Message = L.T("Unauthorized") });
        ctx.Response.StatusCode = HttpStatusCode.Unauthorized;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Body = new ArrayPacket(json.GetBytes());
    });

    /// <summary>判断是否为公开路径。子类可重写以放开更多匿名接口</summary>
    /// <param name="path">请求路径（不含查询字符串）</param>
    /// <returns>是否无需登录</returns>
    protected virtual Boolean IsPublicPath(String path)
    {
        if (path.IsNullOrEmpty()) return true;

        // 登录页与前端词表必须在未登录时可访问
        if (path.EqualIgnoreCase("/", "/index.html", "/favicon.ico")) return true;
        if (path.StartsWithIgnoreCase("/i18n/")) return true;

        // 登录与语言列表是登录前的必需接口
        if (path.EqualIgnoreCase("/api/Login", "/api/Languages")) return true;

        return false;
    }

    /// <summary>判断请求是否命中静态资源处理器</summary>
    private Boolean IsStaticResource(String path, HttpRequest request)
    {
        if (path.IsNullOrEmpty()) return false;

        return Server.MatchHandler(path, request) is StaticFilesHandler or EmbeddedFileHandler;
    }

    /// <summary>拆分请求地址为路径与查询字符串</summary>
    /// <remarks>请求侧的 Uri 是相对地址，读取 AbsolutePath/Query 会抛异常，只能从原始字符串拆分</remarks>
    private static (String path, String query) SplitRequestUri(HttpRequest request)
    {
        var raw = request.RequestUri?.OriginalString;
        if (raw.IsNullOrEmpty()) return (null, null);

        // 经反向代理转发时可能是完整地址，先取其中的路径与查询
        if (raw.StartsWithIgnoreCase("http://") || raw.StartsWithIgnoreCase("https://"))
        {
            if (Uri.TryCreate(raw, UriKind.Absolute, out var abs))
                return (abs.AbsolutePath, abs.Query.IsNullOrEmpty() ? null : abs.Query.TrimStart('?'));
        }

        var index = raw.IndexOf('?');

        return index < 0 ? (raw, null) : (raw[..index], raw[(index + 1)..]);
    }

    /// <summary>从请求头或查询字符串中提取访问令牌</summary>
    /// <remarks>WebSocket/EventSource 无法自定义请求头，因此额外支持 access_token 查询参数</remarks>
    private static String ExtractToken(HttpRequest request, String query)
    {
        var auth = request.Headers["Authorization"];
        if (!auth.IsNullOrEmpty() && auth.StartsWithIgnoreCase("Bearer ")) return auth.Substring("Bearer ".Length).Trim();

        if (!query.IsNullOrEmpty())
        {
            foreach (var item in query.Split('&'))
            {
                if (item.StartsWithIgnoreCase("access_token=")) return Uri.UnescapeDataString(item["access_token=".Length..]);
            }
        }

        return null;
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

        // 首页按请求语言输出；精确路由优先于下方通配静态文件
        Server.MapGet("/", WriteIndex);
        Server.MapGet("/index.html", WriteIndex);

        // 静态文件（根路径，API 路由优先匹配；传 "/" 让 MapEmbedded 内部转为 "/*" 通配）
        Server.MapEmbedded("/", EmbeddedResourcePrefix, EmbeddedResourceAssemblyType.Assembly);
    }

    /// <summary>输出按当前请求语言本地化的首页</summary>
    /// <param name="ctx">Http上下文</param>
    protected virtual void WriteIndex(IHttpContext ctx)
    {
        _indexHtml ??= ReadIndexHtml();

        var html = UiLocalization.Localize(_indexHtml, EmbeddedResourceAssemblyType.Assembly, EmbeddedResourcePrefix, Localization.Culture ?? CultureInfo.CurrentUICulture);
        html = InjectLanguageState(html);

        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Body = new ArrayPacket(html.GetBytes());
    }

    /// <summary>注入当前语言状态，供前端首屏渲染下拉框</summary>
    /// <param name="html">已本地化的页面</param>
    /// <returns>注入语言状态后的页面</returns>
    private static String InjectLanguageState(String html)
    {
        var index = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (index < 0) return html;

        var forced = PanelLanguage.Forced ? "true" : "false";
        var script = $"<script>window.__panelLanguage={{selected:'{PanelLanguage.Selected}',effective:'{PanelLanguage.Effective}',forced:{forced}}};</script>";

        return html.Insert(index, script);
    }

    /// <summary>读取嵌入的首页源文</summary>
    /// <returns>首页 HTML；资源不存在时返回空串</returns>
    private String ReadIndexHtml()
    {
        using var stream = EmbeddedResourceAssemblyType.Assembly.GetManifestResourceStream($"{EmbeddedResourcePrefix}.index.html");
        if (stream == null) return "";

        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
    #endregion

    #region 扩展面板
    /// <summary>获取用户注册的扩展面板列表，子类重写以添加自定义面板</summary>
    /// <returns>扩展面板列表</returns>
    protected internal virtual List<PanelExtension> GetExtensions() => [];
    #endregion

    #region 运行时凭据更新
    /// <summary>验证用户凭据</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="remoteIp">客户端地址，仅用于审计</param>
    /// <returns>认证是否通过</returns>
    public Boolean Authenticate(String user, String password, String remoteIp = null)
    {
        try
        {
            return OnAuthenticate(user, password, remoteIp);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            return false;
        }
    }

    /// <summary>验证用户凭据。子类可重写以接入域账号、单点登录等外部认证</summary>
    /// <param name="user">用户名</param>
    /// <param name="password">口令明文</param>
    /// <param name="remoteIp">客户端地址，仅用于审计</param>
    /// <returns>认证是否通过</returns>
    protected virtual Boolean OnAuthenticate(String user, String password, String remoteIp)
    {
        if (user.IsNullOrEmpty() || password.IsNullOrEmpty()) return false;
        if (UserName.IsNullOrEmpty() || Password.IsNullOrEmpty()) return false;

        // 用户名不区分大小写，与历史行为保持一致
        if (!user.EqualIgnoreCase(UserName)) return false;

        return PasswordProvider.Verify(password, Password);
    }

    /// <summary>运行时更新凭据，立即生效无需重启</summary>
    /// <remarks>口令将以加盐哈希保存，调用方传入明文即可</remarks>
    /// <param name="user">新用户名，null 或空则不更新</param>
    /// <param name="password">新密码明文，null 或空则不更新</param>
    public void UpdateCredentials(String user, String password)
    {
        if (!user.IsNullOrEmpty()) UserName = user;
        if (!password.IsNullOrEmpty()) SetPassword(password);
    }

    /// <summary>设置新口令。哈希后写入配置并立即生效</summary>
    /// <param name="password">口令明文</param>
    public void SetPassword(String password)
    {
        if (password.IsNullOrEmpty()) return;

        Password = PasswordProvider.Hash(password);

        var set = Setting.Current;
        set.WebPassword = Password;
        set.Save();

        WebPanelSecurity.ProtectConfigFile();
    }

    /// <summary>历史明文口令升级为哈希。仅当口令来自配置文件时执行，避免改写调用方临时设置的值</summary>
    /// <param name="password">刚刚验证通过的明文口令</param>
    private void MigrateLegacyPassword(String password)
    {
        if (!String.Equals(Password, password, StringComparison.Ordinal)) return;
        if (!String.Equals(Password, Setting.Current.WebPassword, StringComparison.Ordinal)) return;

        var hash = PasswordProvider.Hash(password);

        // 口令提供者若不散列（如默认 PasswordProvider），不必拆腾配置
        if (String.Equals(hash, Password, StringComparison.Ordinal)) return;

        Password = hash;

        var set = Setting.Current;
        set.WebPassword = Password;
        set.Save();

        WebPanelSecurity.ProtectConfigFile();

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel password has been upgraded to a salted hash.");
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
