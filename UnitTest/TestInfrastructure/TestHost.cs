using NewLife.Agent;
using NewLife.Agent.Models;

namespace UnitTest;

/// <summary>测试宿主。记录调用序列并可配置返回值，替代 Mock&lt;DefaultHost&gt;{ CallBase = true } 的测试替身</summary>
/// <remarks>Restart 保留基类语义（Stop 成功后 Start），调用序列可从 Calls 读取</remarks>
public class TestHost : DefaultHost
{
    /// <summary>调用记录。按发生顺序追加方法名</summary>
    public List<String> Calls { get; } = [];

    /// <summary>IsInstalled 返回值</summary>
    public Boolean Installed { get; set; }

    /// <summary>IsRunning 返回值</summary>
    public Boolean Running { get; set; }

    /// <summary>Install 返回值</summary>
    public Boolean InstallSuccess { get; set; }

    /// <summary>Remove 返回值</summary>
    public Boolean RemoveSuccess { get; set; }

    /// <summary>Start 返回值</summary>
    public Boolean StartSuccess { get; set; }

    /// <summary>Stop 返回值</summary>
    public Boolean StopSuccess { get; set; }

    /// <summary>最近一次 Install 传入的服务模型</summary>
    public ServiceModel LastInstallModel { get; private set; }

    /// <summary>QueryConfig 返回值</summary>
    public ServiceConfig Config { get; set; }

    /// <inheritdoc/>
    public override Boolean IsInstalled(String serviceName)
    {
        Calls.Add(nameof(IsInstalled));
        return Installed;
    }

    /// <inheritdoc/>
    public override Boolean IsRunning(String serviceName)
    {
        Calls.Add(nameof(IsRunning));
        return Running;
    }

    /// <inheritdoc/>
    public override Boolean Install(ServiceModel service)
    {
        Calls.Add(nameof(Install));
        LastInstallModel = service;
        return InstallSuccess;
    }

    /// <inheritdoc/>
    public override Boolean Remove(String serviceName)
    {
        Calls.Add(nameof(Remove));
        return RemoveSuccess;
    }

    /// <inheritdoc/>
    public override Boolean Start(String serviceName)
    {
        Calls.Add(nameof(Start));
        return StartSuccess;
    }

    /// <inheritdoc/>
    public override Boolean Stop(String serviceName)
    {
        Calls.Add(nameof(Stop));
        return StopSuccess;
    }

    /// <inheritdoc/>
    public override ServiceConfig QueryConfig(String serviceName)
    {
        Calls.Add(nameof(QueryConfig));
        return Config;
    }
}
