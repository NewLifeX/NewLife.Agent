using System.ComponentModel;
using System.Diagnostics;
using NewLife.Log;

namespace NewLife.Agent;

/// <summary>服务主机。用于管理控制服务</summary>
public class DefaultHost : DisposeBase, IHost
{
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>
    /// 主服务
    /// </summary>
    public ServiceBase Service { get; set; }

    /// <summary>
    /// 是否以服务形式运行
    /// </summary>
    public Boolean InService { get; set; }

    /// <summary>实例化</summary>
    public DefaultHost() => Name = GetType().Name;

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
    /// <param name="fileName">文件路径</param>
    /// <param name="arguments">命令参数</param>
    /// <param name="description">描述信息</param>
    /// <returns></returns>
    public virtual Boolean Install(String serviceName, String displayName, String fileName, String arguments, String description) => false;

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
    public virtual Boolean Restart(String serviceName)
    {
        if (!Stop(serviceName)) return false;

        return Start(serviceName);
    }

    /// <summary>开始执行服务</summary>
    /// <param name="service"></param>
    public virtual void Run(ServiceBase service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        // 以服务运行
        InService = true;

        try
        {
            // 启动初始化
            service.StartLoop();

            // 阻塞
            service.DoLoop();

            // 停止
            service.StopLoop();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>查询服务配置</summary>
    /// <param name="serviceName">服务名</param>
    public virtual ServiceConfig QueryConfig(String serviceName) => null;

    /// <summary>获取进程（捕获异常）</summary>
    /// <param name="processId"></param>
    /// <returns></returns>
    protected static Process GetProcessById(Int32 processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch { }

        return null;
    }

    /// <summary>进程是否已退出（捕获异常）</summary>
    /// <param name="process"></param>
    /// <returns></returns>
    protected static Boolean GetHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (Win32Exception)
        {
            return true;
        }
        //catch
        //{
        //    return false;
        //}
    }
}