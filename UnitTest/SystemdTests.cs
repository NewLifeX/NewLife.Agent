using System.Text;
using System.ComponentModel;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Models;

namespace UnitTest;

/// <summary>Systemd 宿主单元测试</summary>
/// <remarks>FullTest 会临时替换静态 SystemdPaths[0]，故归入串行集合避免与其它测试并发</remarks>
[Collection("WebPanel")]
public class SystemdTests
{
    [Fact]
    [DisplayName("Systemd_路径探测与服务文件识别")]
    public void FullTest()
    {
        var systemd = new Systemd();
        Assert.Equal("systemd", systemd.Name);

        var serviceName = "testService";

        //Assert.False(Systemd.Available);
        //Assert.Null(Systemd.ServicePath);

        //return;

        // 非 Windows 校验默认探测路径；Windows 下该路径通常不存在，不做断言
        if (!Runtime.Windows)
        {
            Assert.NotNull(Systemd.ServicePath);
            Assert.Equal("/etc/systemd/system", Systemd.ServicePath);
        }

        // GetServicePath 按 SystemdPaths 探测，临时替换首项做读写验证，避免向 /etc/systemd/system 写文件（Linux CI 非 root 无权限）
        var prevFirst = Systemd.SystemdPaths[0];
        var dir = Path.Combine(Path.GetTempPath(), $"agent-systemd-{Guid.NewGuid():N}");
        try
        {
            Systemd.SystemdPaths[0] = dir;

            var file = dir.CombinePath($"{serviceName}.service");
            file.EnsureDirectory(true);
            File.WriteAllText(file.GetFullPath(), "dummy content");

            var path = Systemd.GetServicePath(serviceName);
            Assert.Equal(file, path);

            var result = systemd.IsInstalled(serviceName);
            Assert.True(result);
        }
        finally
        {
            Systemd.SystemdPaths[0] = prevFirst;
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    [DisplayName("SystemdSetting_默认值与属性读写")]
    public void TestSystemdSetting()
    {
        var setting = new SystemdSetting
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet",
            Arguments = "/root/agent/StarAgent.dll -s",
            WorkingDirectory = "/root/agent",
            KillMode = "process",
            OOMScoreAdjust = -1000,

            RestartSec = 3,
            StartLimitInterval = 0,
            StartLimitBurst = 0,
            KillSignal = "SIGINT",
        };

        Assert.Equal("simple", setting.Type);
        Assert.Null(setting.Environment);
        Assert.Equal("always", setting.Restart);
        Assert.Equal("SIGINT", setting.KillSignal);
    }

    [Fact]
    [DisplayName("Build_标准模型_生成完整service文件")]
    public void Build_StarAgent()
    {
        // Arrange
        var setting = new SystemdSetting
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet",
            Arguments = "/root/agent/StarAgent.dll -s",
            WorkingDirectory = "/root/agent",
            KillMode = "process",
            OOMScoreAdjust = -1000,
        };

        // Act
        var result = setting.Build();

        // Assert
        var expected = """
            [Unit]
            Description=星尘代理

            [Service]
            Type=simple
            ExecStart=/usr/share/dotnet/dotnet /root/agent/StarAgent.dll -s
            WorkingDirectory=/root/agent
            Restart=always
            RestartSec=3
            StartLimitInterval=0
            KillSignal=SIGINT
            KillMode=process
            OOMScoreAdjust=-1000

            [Install]
            WantedBy=multi-user.target

            """;

        Assert.Equal(expected, result);
    }

    [Fact]
    [DisplayName("Build_File含参数_合并到ExecStart")]
    public void Build_StarAgent2()
    {
        // Arrange
        var setting = new SystemdSetting
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet /root/agent/StarAgent.dll",
            Arguments = "-s",
            WorkingDirectory = "/root/agent",
            KillMode = "process",
            OOMScoreAdjust = -1000,
        };

        // Act
        var result = setting.Build();

        // Assert
        var expected = """
            [Unit]
            Description=星尘代理

            [Service]
            Type=simple
            ExecStart=/usr/share/dotnet/dotnet /root/agent/StarAgent.dll -s
            WorkingDirectory=/root/agent
            Restart=always
            RestartSec=3
            StartLimitInterval=0
            KillSignal=SIGINT
            KillMode=process
            OOMScoreAdjust=-1000

            [Install]
            WantedBy=multi-user.target

            """;

        Assert.Equal(expected, result);
    }

    [Fact]
    [DisplayName("Install_标准模型_回填SystemdSetting")]
    public void Install_StarAgent()
    {
        var model = new ServiceModel
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet",
            Arguments = "/root/agent/StarAgent.dll -s",
            WorkingDirectory = "/root/agent",
        };

        var mb = new Mock<Systemd> { CallBase = true };
        mb.Setup(x => x.Install(It.IsNotNull<String>(), It.IsNotNull<SystemdSetting>()))
            .Returns(true);

        var syd = mb.Object;

        var rs = syd.Install(model);
        Assert.True(rs);

        Assert.Equal(model.ServiceName, syd.Setting.ServiceName);
        Assert.Equal(model.DisplayName, syd.Setting.DisplayName);
        Assert.Equal(model.Description, syd.Setting.Description);
        Assert.Equal(model.FileName, syd.Setting.FileName);
        Assert.Equal(model.Arguments, syd.Setting.Arguments);
        Assert.Equal(model.WorkingDirectory, syd.Setting.WorkingDirectory);
    }

    [Fact]
    [DisplayName("Install_File含参数_回填SystemdSetting")]
    public void Install_StarAgent2()
    {
        var model = new ServiceModel
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet /root/agent/StarAgent.dll",
            Arguments = "-s",
            WorkingDirectory = "/root/agent",
        };

        var mb = new Mock<Systemd> { CallBase = true };
        mb.Setup(x => x.Install(It.IsNotNull<String>(), It.IsNotNull<SystemdSetting>()))
            .Returns(true);

        var syd = mb.Object;

        var rs = syd.Install(model);
        Assert.True(rs);

        Assert.Equal(model.ServiceName, syd.Setting.ServiceName);
        Assert.Equal(model.DisplayName, syd.Setting.DisplayName);
        Assert.Equal(model.Description, syd.Setting.Description);
        Assert.Equal(model.FileName, syd.Setting.FileName);
        Assert.Equal(model.Arguments, syd.Setting.Arguments);
        Assert.Equal(model.WorkingDirectory, syd.Setting.WorkingDirectory);
    }

    [Fact]
    [DisplayName("Install_工作目录缺省_从参数推断")]
    public void Install_StarAgent_NoWorkingDirectory()
    {
        var model = new ServiceModel
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet",
            Arguments = "/root/agent/StarAgent.dll -s",
            //WorkingDirectory = "/root/agent",
        };

        var mb = new Mock<Systemd> { CallBase = true };
        mb.Setup(x => x.Install(It.IsNotNull<String>(), It.IsNotNull<SystemdSetting>()))
            .Returns(true);

        var syd = mb.Object;

        var rs = syd.Install(model);
        Assert.True(rs);

        Assert.Equal(model.ServiceName, syd.Setting.ServiceName);
        Assert.Equal(model.DisplayName, syd.Setting.DisplayName);
        Assert.Equal(model.Description, syd.Setting.Description);
        Assert.Equal(model.FileName, syd.Setting.FileName);
        Assert.Equal(model.Arguments, syd.Setting.Arguments);
        Assert.Equal("/root/agent", syd.Setting.WorkingDirectory);
    }

    [Fact]
    [DisplayName("Install_File含参数且无工作目录_从参数推断")]
    public void Install_StarAgent_NoWorkingDirectory2()
    {
        var model = new ServiceModel
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet /root/agent/StarAgent.dll",
            Arguments = "-s",
            //WorkingDirectory = "/root/agent",
        };

        var mb = new Mock<Systemd> { CallBase = true };
        mb.Setup(x => x.Install(It.IsNotNull<String>(), It.IsNotNull<SystemdSetting>()))
            .Returns(true);

        var syd = mb.Object;

        var rs = syd.Install(model);
        Assert.True(rs);

        Assert.Equal(model.ServiceName, syd.Setting.ServiceName);
        Assert.Equal(model.DisplayName, syd.Setting.DisplayName);
        Assert.Equal(model.Description, syd.Setting.Description);
        Assert.Equal(model.FileName, syd.Setting.FileName);
        Assert.Equal(model.Arguments, syd.Setting.Arguments);
        Assert.Equal("/root/agent", syd.Setting.WorkingDirectory);
    }

    [Fact]
    [DisplayName("Build_网络与用户配置_生成对应段落")]
    public void Build_Network()
    {
        // Arrange
        var setting = new SystemdSetting
        {
            ServiceName = "StarAgent",
            DisplayName = "星尘代理",
            Description = "",
            FileName = "/usr/share/dotnet/dotnet",
            Arguments = "/root/agent/StarAgent.dll -s",
            WorkingDirectory = "/root/agent",
            KillMode = "process",
            OOMScoreAdjust = -1000,

            Network = true,
            Environment = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true",
            User = "stone",
            Group = "newlife",

            StartLimitBurst = 5,
            StartLimitInterval = 10,
        };

        // Act
        var result = setting.Build();

        // Assert
        var expected = """
            [Unit]
            Description=星尘代理
            After=network.target

            [Service]
            Type=simple
            Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
            ExecStart=/usr/share/dotnet/dotnet /root/agent/StarAgent.dll -s
            WorkingDirectory=/root/agent
            User=stone
            Group=newlife
            Restart=always
            RestartSec=3
            StartLimitInterval=10
            StartLimitBurst=5
            KillSignal=SIGINT
            KillMode=process
            OOMScoreAdjust=-1000

            [Install]
            WantedBy=multi-user.target

            """;

        Assert.Equal(expected, result);
    }
}
