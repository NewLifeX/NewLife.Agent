using System.Text;
using Moq;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Models;

namespace UnitTest;

public class SystemdTests
{
    [Fact]
    public void FullTest()
    {
        var systemd = new Systemd();
        Assert.Equal("systemd", systemd.Name);

        var serviceName = "testService";

        if (Runtime.Windows)
        {
            //Assert.False(Systemd.Available);
            //Assert.Null(Systemd.ServicePath);

            //return;

            // Mock the service path
            Systemd.ServicePath = "/etc/systemd/system";
        }
        else
        {
            Assert.NotNull(Systemd.ServicePath);
            Assert.Equal("/etc/systemd/system", Systemd.ServicePath);
        }

        var file = Systemd.ServicePath.CombinePath($"{serviceName}.service");
        file.EnsureDirectory(true);
        File.WriteAllText(file.GetFullPath(), "dummy content");

        var path = Systemd.GetServicePath(serviceName);
        Assert.Equal(file, path);

        var result = systemd.IsInstalled(serviceName);
        Assert.True(result);

        // Clean up
        File.Delete(file);
    }

    [Fact]
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
