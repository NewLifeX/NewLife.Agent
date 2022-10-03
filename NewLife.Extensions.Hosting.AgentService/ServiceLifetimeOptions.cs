namespace NewLife.Extensions.Hosting.AgentService;

/// <summary>服务生命周期选项</summary>
public class ServiceLifetimeOptions
{
    /// <summary>服务名</summary>
    public String ServiceName { get; set; }

    /// <summary>显示名</summary>
    public String DisplayName { get; set; }

    /// <summary>描述</summary>
    public String Description { get; set; }
}