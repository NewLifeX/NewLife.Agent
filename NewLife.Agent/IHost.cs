namespace NewLife.Agent;

/// <summary>服务主机</summary>
public interface IHost
{
    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    Boolean IsInstalled(String serviceName);

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    Boolean IsRunning(String serviceName);

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="binPath">文件路径</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    Boolean Install(String serviceName, String displayName, String binPath, String description);

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    Boolean Remove(String serviceName);

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    Boolean Start(String serviceName);

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    Boolean Stop(String serviceName);

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    Boolean Restart(String serviceName);

    /// <summary>开始执行服务</summary>
    /// <param name="service"></param>
    void Run(ServiceBase service);

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    ServiceConfig QueryConfig(String serviceName);
}

/// <summary>服务主机。用于管理控制服务</summary>
public abstract class Host : DisposeBase, IHost
{
    /// <summary>
    /// 主服务
    /// </summary>
    public ServiceBase Service { get; set; }

    /// <summary>
    /// 是否以服务形式运行
    /// </summary>
    public Boolean InService { get; set; }

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public virtual Boolean IsInstalled(String serviceName) => false;

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public virtual Boolean IsRunning(String serviceName) => false;

    /// <summary>安装服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="displayName">显示名</param>
    /// <param name="binPath">文件路径</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public virtual Boolean Install(String serviceName, String displayName, String binPath, String description) => false;

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public virtual Boolean Remove(String serviceName) => false;

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public virtual Boolean Start(String serviceName) => false;

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns></returns>
    public virtual Boolean Stop(String serviceName) => false;

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    public virtual Boolean Restart(String serviceName) => false;

    /// <summary>开始执行服务</summary>
    /// <param name="service"></param>
    public abstract void Run(ServiceBase service);

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    public virtual ServiceConfig QueryConfig(String serviceName) => null;
}