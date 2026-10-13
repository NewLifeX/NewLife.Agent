using System.ComponentModel;
using NewLife.Agent;

namespace UnitTest;

/// <summary>Setting 配置落盘持久化单元测试</summary>
/// <remarks>
/// 验证 Save 后配置真实写入 Config/Agent.config，且能从文件读回。
/// SettingScope 在用例结束时还原快照并再次 Save，内存与文件同时自清理。
/// </remarks>
[Collection("WebPanel")]
public class SettingPersistenceTests
{
    /// <summary>配置文件路径。Setting 的 Config 特性为 Agent.config，FileConfigProvider 自动补 Config 目录，基于应用基础目录</summary>
    private static String ConfigFile => "Config/Agent.config".GetBasePath();

    /// <summary>验证服务名修改后落盘，可从配置文件读回</summary>
    [Fact]
    [DisplayName("Setting_Save_服务名落盘到配置文件")]
    public void Save_ServiceName_PersistsToFile()
    {
        using var scope = new SettingScope();

        var name = "UT-Persist-" + Guid.NewGuid().ToString("N");
        Setting.Current.ServiceName = name;
        Setting.Current.Save();

        var file = ConfigFile;
        Assert.True(File.Exists(file), $"配置文件不存在：{file}");

        var text = File.ReadAllText(file);
        Assert.Contains(name, text);
    }

    /// <summary>验证 Web 密码修改（ChangePassword 的落盘路径）后可从配置文件读回</summary>
    [Fact]
    [DisplayName("Setting_Save_Web密码落盘到配置文件")]
    public void Save_WebPassword_PersistsToFile()
    {
        using var scope = new SettingScope();

        var pass = "UT-Pass-" + Guid.NewGuid().ToString("N");
        Setting.Current.WebPassword = pass;
        Setting.Current.Save();

        var file = ConfigFile;
        Assert.True(File.Exists(file), $"配置文件不存在：{file}");

        var text = File.ReadAllText(file);
        Assert.Contains(pass, text);
    }
}
