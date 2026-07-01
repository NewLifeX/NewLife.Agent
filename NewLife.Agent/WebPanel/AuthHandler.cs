#if !NET40
using System.Net;
using NewLife.Data;
using NewLife.Http;
using NewLife.Net;

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

/// <summary>鉴权处理器，包装真实Handler，在调用前检查Bearer Token</summary>
public class AuthHandler : IHttpHandler
{
    #region 属性
    /// <summary>被包装的真实处理器</summary>
    public IHttpHandler Handler { get; set; }

    /// <summary>鉴权级别</summary>
    public AuthLevel Level { get; set; } = AuthLevel.LocalOnly;

    /// <summary>用户名</summary>
    public String UserName { get; set; }

    /// <summary>密码</summary>
    public String Password { get; set; }

    private readonly Dictionary<String, TokenInfo> _tokens = new();
    private readonly Object _lock = new();
    #endregion

    /// <summary>实例化鉴权处理器</summary>
    /// <param name="handler">被包装的真实处理器，可为null仅用作鉴权状态共享</param>
    public AuthHandler(IHttpHandler handler)
    {
        Handler = handler;
    }

    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    public void ProcessRequest(IHttpContext context)
    {
        if (!CheckAuth(context))
        {
            context.Response.StatusCode = System.Net.HttpStatusCode.Unauthorized;
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            context.Response.Body = new ArrayPacket("Unauthorized".GetBytes());
            return;
        }

        Handler?.ProcessRequest(context);
    }

    #region 鉴权检查
    /// <summary>检查鉴权</summary>
    /// <param name="context">Http上下文</param>
    /// <returns>是否通过</returns>
    protected virtual Boolean CheckAuth(IHttpContext context)
    {
        if (Level == AuthLevel.None) return true;

        // LocalOnly 模式下本地请求免鉴权
        if (Level == AuthLevel.LocalOnly && IsLocalRequest(context)) return true;

        // 检查 Bearer Token
        var auth = context.Request.Headers["Authorization"];
        if (auth.IsNullOrEmpty()) return false;

        if (!auth.StartsWithIgnoreCase("Bearer ")) return false;

        var token = auth.Substring("Bearer ".Length).Trim();
        if (token.IsNullOrEmpty()) return false;

        return ValidateToken(token);
    }

    /// <summary>是否本地请求</summary>
    /// <param name="context">Http上下文</param>
    /// <returns></returns>
    protected virtual Boolean IsLocalRequest(IHttpContext context)
    {
        // 通过Socket获取远程端点判断是否本机
        var socket = context.Socket;
        if (socket?.Remote == null || socket.Remote.Address == null) return true;

        // IPAddress.IsLoopback 判断本机回环地址
        if (IPAddress.IsLoopback(socket.Remote.Address)) return true;

        return false;
    }
    #endregion

    #region Token管理
    /// <summary>令牌信息</summary>
    private class TokenInfo
    {
        public String User { get; set; } = "";
        public DateTime Expire { get; set; }
    }

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
            {
                _tokens.Remove(key);
            }

            _tokens[token] = info;
        }

        return token;
    }

    /// <summary>验证Token</summary>
    /// <param name="token">Token字符串</param>
    /// <returns></returns>
    protected virtual Boolean ValidateToken(String token)
    {
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
    #endregion
}

#endif
