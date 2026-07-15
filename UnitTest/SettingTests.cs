using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>Setting 配置模型单元测试</summary>
/// <remarks>验证 Setting 配置模型的属性和默认值</remarks>
public class SettingTests
{
    [Fact]
    [DisplayName("Setting_单例_同一配置多次获取相同")]
    public void Setting_Singleton_ReturnsSame()
    {
        var set1 = Setting.Current;
        var set2 = Setting.Current;

        Assert.NotNull(set1);
        Assert.Same(set1, set2);
    }

    [Fact]
    [DisplayName("Setting_ServiceName_默认空")]
    public void Setting_ServiceName_DefaultEmpty()
    {
        var set = new Setting();
        Assert.Equal("", set.ServiceName);
    }

    [Fact]
    [DisplayName("Setting_DisplayName_默认空")]
    public void Setting_DisplayName_DefaultEmpty()
    {
        var set = new Setting();
        Assert.Equal("", set.DisplayName);
    }

    [Fact]
    [DisplayName("Setting_UseAutorun_默认false")]
    public void Setting_UseAutorun_DefaultFalse()
    {
        var set = new Setting();
        Assert.False(set.UseAutorun);
    }

    [Fact]
    [DisplayName("Setting_WatchInterval_默认10秒")]
    public void Setting_WatchInterval_Default10()
    {
        var set = new Setting();
        Assert.Equal(10, set.WatchInterval);
    }

    [Fact]
    [DisplayName("Setting_FreeMemoryInterval_默认600秒")]
    public void Setting_FreeMemoryInterval_Default600()
    {
        var set = new Setting();
        Assert.Equal(600, set.FreeMemoryInterval);
    }

    [Fact]
    [DisplayName("Setting_MaxMemory_默认0（不限）")]
    public void Setting_MaxMemory_Default0()
    {
        var set = new Setting();
        Assert.Equal(0, set.MaxMemory);
    }

    [Fact]
    [DisplayName("Setting_MaxThread_默认1000")]
    public void Setting_MaxThread_Default1000()
    {
        var set = new Setting();
        Assert.Equal(1000, set.MaxThread);
    }

    [Fact]
    [DisplayName("Setting_MaxHandle_默认10000")]
    public void Setting_MaxHandle_Default10000()
    {
        var set = new Setting();
        Assert.Equal(10000, set.MaxHandle);
    }

    [Fact]
    [DisplayName("Setting_AutoRestart_默认0（关闭）")]
    public void Setting_AutoRestart_Default0()
    {
        var set = new Setting();
        Assert.Equal(0, set.AutoRestart);
    }

    [Fact]
    [DisplayName("Setting_WatchDog_默认空")]
    public void Setting_WatchDog_DefaultEmpty()
    {
        var set = new Setting();
        Assert.Equal("", set.WatchDog);
    }

    [Fact]
    [DisplayName("Setting_AfterStart_默认空")]
    public void Setting_AfterStart_DefaultEmpty()
    {
        var set = new Setting();
        Assert.Equal("", set.AfterStart);
    }

    [Fact]
    [DisplayName("Setting_EnableWebPanel_默认true")]
    public void Setting_EnableWebPanel_DefaultTrue()
    {
        var set = new Setting();
        Assert.True(set.EnableWebPanel);
    }

    [Fact]
    [DisplayName("Setting_WebPort_默认5580")]
    public void Setting_WebPort_Default5580()
    {
        var set = new Setting();
        Assert.Equal(5580, set.WebPort);
    }

    [Fact]
    [DisplayName("Setting_WebAuthLevel_默认LocalOnly")]
    public void Setting_WebAuthLevel_DefaultLocalOnly()
    {
        var set = new Setting();
        Assert.Equal("LocalOnly", set.WebAuthLevel);
    }

    [Fact]
    [DisplayName("Setting_WebUserName_默认admin")]
    public void Setting_WebUserName_DefaultAdmin()
    {
        var set = new Setting();
        Assert.Equal("admin", set.WebUserName);
    }

    [Fact]
    [DisplayName("Setting_WebPassword_默认admin")]
    public void Setting_WebPassword_DefaultAdmin()
    {
        var set = new Setting();
        Assert.Equal("admin", set.WebPassword);
    }

    [Fact]
    [DisplayName("Setting_属性可读可写")]
    public void Setting_Properties_ReadWrite()
    {
        var set = new Setting();

        set.ServiceName = "MyService";
        set.DisplayName = "我的服务";
        set.Description = "服务描述";
        set.MaxMemory = 512;

        Assert.Equal("MyService", set.ServiceName);
        Assert.Equal("我的服务", set.DisplayName);
        Assert.Equal("服务描述", set.Description);
        Assert.Equal(512, set.MaxMemory);
    }
}
