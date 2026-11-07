#if !NET40
namespace NewLife.Agent.WebPanel.Security;

/// <summary>本地口令认证器</summary>
/// <remarks>
/// 校验 <see cref="AgentWebPanel.UserName"/> 与 <see cref="AgentWebPanel.Password"/>。
/// 口令字段可能是历史明文，也可能是 <see cref="PanelPasswordHash"/> 生成的哈希串，两种都支持。
/// </remarks>
public class LocalPasswordAuthenticator : IPanelAuthenticator
{
    #region 属性
    /// <summary>认证模式名称</summary>
    public String Name => "Password";

    /// <summary>是否支持在面板内修改口令</summary>
    public Boolean CanChangePassword => true;
    #endregion

    #region 方法
    /// <summary>验证用户凭据</summary>
    /// <param name="panel">所属面板</param>
    /// <param name="user">用户名</param>
    /// <param name="password">口令明文</param>
    /// <param name="remoteIp">客户端地址，仅用于审计</param>
    /// <returns>认证是否通过</returns>
    public Boolean Authenticate(AgentWebPanel panel, String user, String password, String remoteIp)
    {
        if (panel == null || user.IsNullOrEmpty() || password.IsNullOrEmpty()) return false;
        if (panel.UserName.IsNullOrEmpty() || panel.Password.IsNullOrEmpty()) return false;

        // 用户名不区分大小写，与历史行为保持一致
        if (!user.EqualIgnoreCase(panel.UserName)) return false;

        return PanelPasswordHash.Verify(password, panel.Password);
    }
    #endregion
}
#endif
