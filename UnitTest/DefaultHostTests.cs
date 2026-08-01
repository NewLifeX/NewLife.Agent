using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>DefaultHost 兜底宿主单元测试</summary>
/// <remarks>验证默认宿主不提供真实服务管理能力时的默认行为</remarks>
public class DefaultHostTests
{
    [Fact]
    [DisplayName("DefaultHost_默认未安装")]
    public void IsInstalled_ReturnsFalse()
    {
        var host = new DefaultHost();
        Assert.False(host.IsInstalled("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_默认未运行")]
    public void IsRunning_ReturnsFalse()
    {
        var host = new DefaultHost();
        Assert.False(host.IsRunning("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_安装返回false")]
    public void Install_ReturnsFalse()
    {
        var host = new DefaultHost();
        Assert.False(host.Install(null));
    }

    [Fact]
    [DisplayName("DefaultHost_卸载返回false")]
    public void Remove_ReturnsFalse()
    {
        var host = new DefaultHost();
        Assert.False(host.Remove("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_启动返回false")]
    public void Start_ReturnsFalse()
    {
        var host = new DefaultHost();
        Assert.False(host.Start("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_停止返回false")]
    public void Stop_ReturnsFalse()
    {
        var host = new DefaultHost();
        Assert.False(host.Stop("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_重启_停止失败返回false")]
    public void Restart_StopFails_ReturnsFalse()
    {
        var host = new DefaultHost();
        // 停止返回 false，重启短路返回 false
        Assert.False(host.Restart("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_查询配置返回null")]
    public void QueryConfig_ReturnsNull()
    {
        var host = new DefaultHost();
        Assert.Null(host.QueryConfig("test"));
    }

    [Fact]
    [DisplayName("DefaultHost_名称_默认类名")]
    public void Name_DefaultsToTypeName()
    {
        var host = new DefaultHost();
        Assert.Equal(nameof(DefaultHost), host.Name);
    }
}
