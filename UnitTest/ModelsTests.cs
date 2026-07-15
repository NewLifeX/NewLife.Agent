using System.ComponentModel;
using NewLife.Agent.Models;

namespace UnitTest;

/// <summary>数据模型单元测试</summary>
/// <remarks>验证 ServiceModel、ServiceConfig、SystemdSetting、Menu 等数据模型</remarks>
public class ModelsTests
{
    [Fact]
    [DisplayName("ServiceModel_构造_默认值正确")]
    public void ServiceModel_Defaults()
    {
        var model = new ServiceModel();

        Assert.Null(model.ServiceName);
        Assert.Null(model.DisplayName);
        Assert.Null(model.Description);
        Assert.Null(model.FileName);
        Assert.Null(model.Arguments);
        Assert.Null(model.WorkingDirectory);
        Assert.Null(model.User);
        Assert.Null(model.Group);
    }

    [Fact]
    [DisplayName("ServiceModel_属性可读写")]
    public void ServiceModel_ReadWrite()
    {
        var model = new ServiceModel
        {
            ServiceName = "Test",
            DisplayName = "测试",
            Description = "desc",
            FileName = "/app/test",
            Arguments = "-s",
            WorkingDirectory = "/app",
            User = "root",
            Group = "root"
        };

        Assert.Equal("Test", model.ServiceName);
        Assert.Equal("测试", model.DisplayName);
        Assert.Equal("desc", model.Description);
        Assert.Equal("/app/test", model.FileName);
        Assert.Equal("-s", model.Arguments);
        Assert.Equal("/app", model.WorkingDirectory);
        Assert.Equal("root", model.User);
        Assert.Equal("root", model.Group);
    }

    [Fact]
    [DisplayName("ServiceConfig_构造_默认值正确")]
    public void ServiceConfig_Defaults()
    {
        var config = new ServiceConfig();

        Assert.Null(config.Name);
        Assert.Null(config.DisplayName);
        Assert.Null(config.FilePath);
        Assert.Null(config.Arguments);
        Assert.False(config.AutoStart);
        Assert.Null(config.Command);
    }

    [Fact]
    [DisplayName("ServiceConfig_属性可读写")]
    public void ServiceConfig_ReadWrite()
    {
        var config = new ServiceConfig
        {
            Name = "Test",
            DisplayName = "测试",
            FilePath = "/app/test.exe",
            Arguments = "-s",
            AutoStart = true,
            Command = "/app/test.exe -s"
        };

        Assert.Equal("Test", config.Name);
        Assert.Equal("测试", config.DisplayName);
        Assert.Equal("/app/test.exe", config.FilePath);
        Assert.Equal("-s", config.Arguments);
        Assert.True(config.AutoStart);
        Assert.Equal("/app/test.exe -s", config.Command);
    }

    [Fact]
    [DisplayName("SystemdSetting_继承ServiceModel_额外属性正确")]
    public void SystemdSetting_InheritsServiceModel()
    {
        var setting = new SystemdSetting
        {
            ServiceName = "Test",
            Type = "simple",
            Restart = "always",
            RestartSec = 3f,
            KillMode = "process",
            Network = true
        };

        Assert.Equal("Test", setting.ServiceName);
        Assert.Equal("simple", setting.Type);
        Assert.Equal("always", setting.Restart);
        Assert.Equal(3f, setting.RestartSec);
        Assert.Equal("process", setting.KillMode);
        Assert.True(setting.Network);
    }

    [Fact]
    [DisplayName("SystemdSetting_Build_生成有效systemd配置")]
    public void SystemdSetting_Build_ValidOutput()
    {
        var setting = new SystemdSetting
        {
            ServiceName = "TestService",
            DisplayName = "测试服务",
            FileName = "/usr/bin/dotnet",
            Arguments = "/app/TestService.dll -s",
            WorkingDirectory = "/app",
            Type = "simple",
            Restart = "always",
            Network = true,
            User = "root"
        };

        var output = setting.Build();

        Assert.NotNull(output);
        Assert.Contains("[Unit]", output);
        Assert.Contains("Description=测试服务", output);
        Assert.Contains("After=network.target", output);
        Assert.Contains("[Service]", output);
        Assert.Contains("Type=simple", output);
        Assert.Contains("ExecStart=/usr/bin/dotnet /app/TestService.dll -s", output);
        Assert.Contains("WorkingDirectory=/app", output);
        Assert.Contains("User=root", output);
        Assert.Contains("Restart=always", output);
    }

    [Fact]
    [DisplayName("Menu_构造_正确设置属性")]
    public void Menu_Constructor_SetsProperties()
    {
        var menu = new Menu('1', "显示状态", "-status", () => { });

        Assert.Equal('1', menu.Key);
        Assert.Equal("显示状态", menu.Name);
        Assert.Equal("-status", menu.Cmd);
        Assert.NotNull(menu.Callback);
    }

    [Fact]
    [DisplayName("Menu_比较_按键排序")]
    public void Menu_CompareByKey()
    {
        var m1 = new Menu('1', "A", "-a", () => { });
        var m2 = new Menu('2', "B", "-b", () => { });

        Assert.True(m1.CompareTo(m2) < 0);
        Assert.True(m2.CompareTo(m1) > 0);
        Assert.Equal(0, m1.CompareTo(m1));
    }
}
