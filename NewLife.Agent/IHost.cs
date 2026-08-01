using NewLife.Agent.Models;

namespace NewLife.Agent;

/// <summary>服务主机</summary>
public interface IHost
{
    /// <summary>名称</summary>
    String Name { get; }

    /// <summary>服务是否已安装</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否已安装</returns>
    Boolean IsInstalled(String serviceName);

    /// <summary>服务是否已启动</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否已启动</returns>
    Boolean IsRunning(String serviceName);

    /// <summary>安装服务</summary>
    /// <param name="service">服务</param>
    /// <returns>是否安装成功</returns>
    Boolean Install(ServiceModel service);

    /// <summary>卸载服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否卸载成功</returns>
    Boolean Remove(String serviceName);

    /// <summary>启动服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否启动成功</returns>
    Boolean Start(String serviceName);

    /// <summary>停止服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否停止成功</returns>
    Boolean Stop(String serviceName);

    /// <summary>重启服务</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>是否重启成功</returns>
    Boolean Restart(String serviceName);

    /// <summary>开始执行服务</summary>
    /// <param name="service">服务</param>
    void Run(ServiceBase service);

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    /// <returns>服务配置</returns>
    ServiceConfig QueryConfig(String serviceName);
}