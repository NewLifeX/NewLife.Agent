#if !NET40
namespace NewLife.Agent.WebPanel.Security;

/// <summary>Web面板登录认证器</summary>
/// <remarks>
/// 面板认证方式可插拔：默认使用本地口令（哈希保存），也可以切换到操作系统账号认证。
/// 认证通过后仍由 <see cref="AgentWebPanel"/> 统一签发令牌，业务控制器不感知认证方式。
/// </remarks>
public interface IPanelAuthenticator
{
    /// <summary>认证模式名称。用于界面展示与审计日志</summary>
    String Name { get; }

    /// <summary>是否支持在面板内修改口令</summary>
    /// <remarks>操作系统账号认证模式下口令由系统管理，面板不提供修改入口</remarks>
    Boolean CanChangePassword { get; }

    /// <summary>验证用户凭据</summary>
    /// <param name="panel">所属面板，便于读取当前配置的用户名与口令</param>
    /// <param name="user">用户名</param>
    /// <param name="password">口令明文</param>
    /// <param name="remoteIp">客户端地址，仅用于审计</param>
    /// <returns>认证是否通过</returns>
    Boolean Authenticate(AgentWebPanel panel, String user, String password, String remoteIp);
}
#endif
