#if !NET40
using System.Net;
using System.Globalization;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NewLife.Agent.WebPanel.Security;
using NewLife.Data;
using NewLife.Http;
using NewLife.Log;
using NewLife.Serialization;
using NewLife;

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

    /// <summary>鉴权级别。已废弃，所有请求统一要求登录</summary>
    public AuthLevel Level { get; set; }

    /// <summary>登录认证器。默认本地口令，可切换为操作系统账号</summary>
    public IPanelAuthenticator Authenticator { get; set; }

    /// <summary>是否支持在面板内修改口令</summary>
    public Boolean CanChangePassword => Authenticator?.CanChangePassword == true;

    /// <summary>当前认证模式名称，供界面展示</summary>
    public String AuthModeName => Authenticator?.Name ?? "Password";

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

        Level = ParseAuthLevel(set.WebAuthLevel);
        UserName = set.WebUserName;
        Password = set.WebPassword;
        Authenticator = CreateAuthenticator(set.WebAuthMode);

        RegisterRoutes();
    }

    /// <summary>根据配置创建认证器</summary>
    /// <param name="mode">认证模式，Password 或 OsAccount</param>
    /// <returns>认证器实例</returns>
    public static IPanelAuthenticator CreateAuthenticator(String mode)
    {
        return mode?.ToLower() switch
        {
            "osaccount" => new OsAccountAuthenticator(),
            _ => new LocalPasswordAuthenticator()
        };
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

        // 本地口令模式下，空口令先生成随机初始口令，再进行安全检查
        EnsureInitialPassword();

        if (!CheckSecurity()) return;

        Server.Start();

        var scheme = Server.SslProtocol != SslProtocols.None ? "https" : "http";
        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web management panel started on {0} port {1}.", scheme, Server.Port);
    }

    /// <summary>启动前的安全检查。不允许在未加密或使用默认口令的情况下把面板暴露到网络</summary>
    /// <returns>是否允许启动</returns>
    private Boolean CheckSecurity()
    {
        var set = Setting.Current;
        var loopback = IsLoopbackBinding();

        if (!PrepareTransport(loopback, set)) return false;

        // 本地口令模式：非本机监听时，空口令或默认口令一律拒绝启动
        if (!loopback && Authenticator is LocalPasswordAuthenticator && IsDefaultPassword())
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Refusing to start: the web panel listens on a non-loopback address with an empty or default password. Set WebPassword or switch WebAuthMode to OsAccount.");

            return false;
        }

        return true;
    }

    /// <summary>当前绑定地址是否仅本机回环</summary>
    private Boolean IsLoopbackBinding()
    {
        var address = Server.Local.Address;

        return address != null && IPAddress.IsLoopback(address);
    }

    /// <summary>判断当前口令是否为空或仍是默认的 admin</summary>
    private Boolean IsDefaultPassword() => Password.IsNullOrEmpty() || PanelPasswordHash.Verify("admin", Password);

    /// <summary>按配置准备传输安全（TLS）</summary>
    /// <param name="loopback">是否仅本机监听</param>
    /// <param name="set">当前配置</param>
    /// <returns>是否允许继续启动</returns>
    private Boolean PrepareTransport(Boolean loopback, Setting set)
    {
        var mode = (set.WebTls ?? "").Trim();
        if (mode.IsNullOrEmpty() || mode.EqualIgnoreCase("Auto"))
            mode = loopback ? "Off" : "SelfSigned";

        if (mode.EqualIgnoreCase("Off"))
            return AllowInsecure(loopback, set, "TLS is disabled");

        String file;
        if (mode.EqualIgnoreCase("Pfx"))
        {
            file = set.WebCertificate;
            if (file.IsNullOrEmpty() || !File.Exists(file))
            {
                XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Refusing to start: WebTls is Pfx but WebCertificate [{0}] does not exist.", file);

                return false;
            }
        }
        else
        {
            file = Path.Combine(WebPanelSecurity.GetConfigDirectory(), "panel.pfx");
        }

        if (!LoadCertificate(file, set.WebCertificatePassword))
            return AllowInsecure(loopback, set, "TLS could not be enabled");

        return true;
    }

    /// <summary>加载或生成证书并启用到服务器</summary>
    private Boolean LoadCertificate(String file, String password)
    {
        try
        {
            var cert = WebPanelSecurity.CreateSelfSigned(file, password ?? "");

            return EnableTls(cert);
        }
        catch (Exception ex)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to enable TLS: {0}", ex.Message);

            return false;
        }
    }

    /// <summary>启用 TLS。证书为空表示运行时无法生成自签证书</summary>
    private Boolean EnableTls(X509Certificate cert)
    {
        if (cert == null) return false;

#if NET5_0_OR_GREATER
        Server.SslProtocol = SslProtocols.Tls12 | SslProtocols.Tls13;
#else
        Server.SslProtocol = SslProtocols.Tls12;
#endif
        Server.Certificate = cert;

        return true;
    }

    /// <summary>未启用 TLS 时的处置。本机回环直接放行；网络监听只允许显式放开</summary>
    private static Boolean AllowInsecure(Boolean loopback, Setting set, String reason)
    {
        if (loopback) return true;

        if (set.WebAllowInsecureRemote)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Security warning: the web panel listens on a non-loopback address without TLS because WebAllowInsecureRemote is enabled ({0}).", reason);

            return true;
        }

        XTrace.Log.WriteLogPrefixed(LogLevel.Error, "[WebPanel]", "Refusing to start: the web panel listens on a non-loopback address while {0}. Configure WebTls or enable WebAllowInsecureRemote explicitly.", reason);

        return false;
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
        var authenticator = Authenticator;
        if (authenticator == null) return false;

        try
        {
            return authenticator.Authenticate(this, user, password, remoteIp);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);

            return false;
        }
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

    /// <summary>设置新口令。加盐哈希后写入配置并立即生效</summary>
    /// <param name="password">口令明文</param>
    public void SetPassword(String password)
    {
        if (password.IsNullOrEmpty()) return;

        Password = PanelPasswordHash.Hash(password);

        var set = Setting.Current;
        set.WebPassword = Password;
        set.Save();

        WebPanelSecurity.ProtectConfigFile();

        // 引导口令文件在首次成功登录后作废
        TryDeleteInitialPasswordFile();
    }

    /// <summary>历史明文口令升级为哈希。仅当面板口令来自配置文件时执行，避免改写调用方临时设置的值</summary>
    /// <param name="password">刚刚验证通过的明文口令</param>
    private void MigrateLegacyPassword(String password)
    {
        if (Authenticator is not LocalPasswordAuthenticator) return;
        if (!PanelPasswordHash.IsLegacy(Password)) return;
        if (!String.Equals(Password, Setting.Current.WebPassword, StringComparison.Ordinal)) return;

        Password = PanelPasswordHash.Hash(password);

        var set = Setting.Current;
        set.WebPassword = Password;
        set.Save();

        WebPanelSecurity.ProtectConfigFile();

        XTrace.Log.WriteLogPrefixed(LogLevel.Info, "[WebPanel]", "The web panel password has been upgraded to a salted hash.");
    }

    /// <summary>首次启动时生成随机初始口令</summary>
    /// <remarks>仅本地口令模式需要。口令明文只写入日志与仅本机可读的引导文件，登录成功后删除引导文件</remarks>
    private void EnsureInitialPassword()
    {
        if (Authenticator is not LocalPasswordAuthenticator) return;
        if (!Password.IsNullOrEmpty()) return;

        // 仅在由服务托管启用面板时自动生成，避免手工构造面板（测试/嵌入）产生副作用
        if (!Setting.Current.EnableWebPanel) return;

        var password = PanelPasswordHash.CreateRandomPassword();
        Password = PanelPasswordHash.Hash(password);

        var set = Setting.Current;
        set.WebPassword = Password;
        set.Save();

        WebPanelSecurity.ProtectConfigFile();
        WriteInitialPasswordFile(password);

        XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Generated an initial web panel password: {0}. Sign in and change it as soon as possible.", password);
    }

    private static String InitialPasswordFile => Path.Combine(WebPanelSecurity.GetConfigDirectory(), "panel.initial-password.txt");

    private static void WriteInitialPasswordFile(String password)
    {
        try
        {
            var file = InitialPasswordFile;
            file.EnsureDirectory(true);
            File.WriteAllText(file, password);
            WebPanelSecurity.ProtectFile(file);
        }
        catch (Exception ex)
        {
            XTrace.Log.WriteLogPrefixed(LogLevel.Warn, "[WebPanel]", "Failed to write the initial password file: {0}", ex.Message);
        }
    }

    private static void TryDeleteInitialPasswordFile()
    {
        try
        {
            var file = InitialPasswordFile;
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
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
