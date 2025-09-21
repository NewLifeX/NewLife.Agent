# NewLife.Agent - 守护服务管理组件

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.agent?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.agent?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.agent?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.agent?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.agent?label=dev%20nuget&logo=nuget)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Extensions.Hosting.AgentService?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Extensions.Hosting.AgentService?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Extensions.Hosting.AgentService?label=dev%20nuget&logo=nuget)

使用教程：[https://newlifex.com/core/agent](https://newlifex.com/core/agent)  

---
## 目录
- [简介](#简介)
- [适用场景](#适用场景)
- [核心特性](#核心特性)
- [架构概览](#架构概览)
- [快速开始](#快速开始)
  - [最小控制台服务](#最小控制台服务)
  - [.NET Web/Worker 集成示例](#net-webworker-集成示例)
- [命令行与交互菜单](#命令行与交互菜单)
- [配置文件说明](#配置文件说明)
- [健康监控与重启策略](#健康监控与重启策略)
- [看门狗 WatchDog](#看门狗-watchdog)
- [自启动 vs 系统服务](#自启动-vs-系统服务)
- [跨平台主机适配策略](#跨平台主机适配策略)
- [多种守护体系实现](#多种守护体系实现)
- [与 NSSM / srvany 对比](#与-nssm--srvany-对比)
- [常见问题 FAQ](#常见问题-faq)
- [关于 net8+ 的使用](#关于net8的使用)
- [快速拥有](#快速拥有)
- [新生命项目矩阵](#新生命项目矩阵)
- [新生命开发团队](#新生命开发团队)

---
## 简介
NewLife.Agent 是一个跨平台服务/守护进程开发与运行框架，帮助普通控制台 / Web / Worker / 数据处理等长期运行应用快速注册为 **Windows 服务** 或 **Linux Systemd**（以及其它 init 系统）并提供：安装/卸载/启动/停止/调试、健康监控、资源超限重启、定时重启、看门狗、多实例部署等能力。支持 `.NET Framework 4.x` 与 `.NET (Core) 3.1+` 直至最新版本，多 Target 框架长期维护。

---
## 适用场景
- 后台常驻：RPC 服务端、MQTT 服务器、网关、调度/爬虫、IoT 采集、Cache/消息消费、数据处理流水线
- ASP.NET Core 网站 / API / Blazor / Minimal API 需要以系统服务形式长期运行
- Worker Service / BackgroundService 长期任务统一管理
- 需要统一的健康监控（内存/线程/句柄）与自动重启策略
- 需要简单可靠的看门狗守护其它已安装服务
- 单一程序在多目录部署为多个独立服务实例（通过配置覆写 ServiceName / DisplayName）

---
## 核心特性
NewLife.Agent主要功能：  
1. 注册应用为系统服务或守护进程，随系统自动启动  
1. 支持控制台菜单控制安装、卸载、启动、停止，以及查看状态  
1. 支持控制台调试应用，解决Windows服务难以调试的问题  
1. 支持健康检测，限制内存、线程数、句柄数，超限时重启应用服务  
1. 支持应用服务定时重启，通过配置指定  
1. 支持看门狗WatchDog，通过配置指定要守护的目标应用服务，如果目标停止则启动  
1. 支持配置文件修改服务名，一个应用程序可在多个目录上部署为不同的系统服务  

> 与同类工具不同：Agent 是“开发框架 + 运行管理”二合一，可直接在代码中扩展逻辑与命令；无需额外包装可执行文件。

---
## 架构概览
核心基类 `ServiceBase` 负责：
- 统一入口 `Main(args)`：解析命令 → 交互菜单 / 命令执行
- 主机适配：根据平台与配置动态选择 `IHost` 实现（WindowsService / Systemd / OSXLaunch / Procd / RcInit / DefaultHost / WindowsAutorun）
- 管理循环：周期性健康检查（内存/线程/句柄/定时重启/看门狗）
- 命令体系：`CommandFactory` + 各种 `*CommandHandler`（安装、卸载、启动、停止、重启、运行、状态、看门狗等）
- 资源超限策略：超限时通过 `Host.Restart(ServiceName)` 触发平滑重启
- 退出处理：`Host.RegisterExit` 捕捉进程退出并清理日志

主要流程：
```
Main → InitService → Init(选择 Host + 载入/保存配置) → 解析命令或显示菜单 → StartLoop/StopLoop → DoCheck(健康/看门狗)
```

---
## 快速开始
### 最小控制台服务
```csharp
public class MyService : ServiceBase
{
    public MyService()
    {
        ServiceName = "DemoAgent";        // 服务名
        DisplayName = "演示服务";          // 显示名
        Description = "演示用后台服务";    // 描述
    }

    public override void StartWork(String reason)
    {
        WriteLog("业务启动: {0}", reason);
        // TODO: 启动定时任务 / 网络监听 / 队列消费者等
        base.StartWork(reason);
    }

    public override void StopWork(String reason)
    {
        WriteLog("业务停止: {0}", reason);
        // TODO: 清理资源
        base.StopWork(reason);
    }
}

public static class Program
{
    public static void Main(String[] args)
    {
#if DEBUG
        if (args == null || args.Length == 0) args = new[] { "-run" }; // 调试快速进入模拟运行
#endif
        new MyService().Main(args);
    }
}
```
运行 `dotnet run` 后出现交互菜单，可一键安装/启动。部署后使用 `-install` / `-start` 等命令行参数实现无人值守操作。

### .NET Web/Worker 集成示例
以 ASP.NET Core 为例（参见仓库 `Zero.Web` 示例）：
```csharp
public class WebAgent : ServiceBase
{
    public Func<IHostBuilder> BuildHost { get; set; }

    public WebAgent()
    {
        ServiceName = "WebAgent";
        DisplayName = "Web服务代理";
        Description = "承载 ASP.NET Core 的系统服务";
    }

    public override void StartWork(String reason)
    {
        var tokenSrc = new CancellationTokenSource();
        BuildHost?.Invoke()?.Build().RunAsync(tokenSrc.Token); // 非阻塞运行
        base.StartWork(reason);
    }
}

public static class Program
{
    public static void Main(String[] args)
    {
#if DEBUG
        if (args?.Length == 0) args = new[] { "-run" };
#endif
        new WebAgent { BuildHost = () => Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(w => w.UseStartup<Startup>()) }.Main(args);
    }
}
```
> 通过非阻塞 `RunAsync` 嵌入宿主，实现 Web + Agent 统一的安装/运行/重启/监控治理。

---
## 命令行与交互菜单
运行无参数进入交互模式（黄色菜单），快捷键默认：
- 1：显示状态（相当于 `-status`）
- 2：安装/卸载（`-install` / `-uninstall`）
- 3：启动/停止（`-start` / `-stop`）
- 4：重启（`-restart`）
- 5：模拟运行（`-run`，在当前进程内直接调用 StartWork，便于调试）
- 7：看门狗检查（仅在配置 WatchDog 非空才显示）
- 0：退出

常用无人值守命令（可脚本化）：
```
MyApp.exe -install
MyApp.exe -start
MyApp.exe -stop
MyApp.exe -restart
MyApp.exe -status
MyApp.exe -uninstall
MyApp.exe -run          // 控制台调试
MyApp.exe -autorun      // 使用“登录自启动”模式（仅 Windows）
MyApp.exe -watchdog     // 立即执行一次看门狗检查
```
> 所有操作需在管理员/root 权限下执行（安装/卸载/启动/停止）。

---
## 配置文件说明
配置由 `Setting.Current` 提供（自动存储于应用配置目录），典型字段：
| 字段 | 说明 |
| ---- | ---- |
| ServiceName | 服务名（可通过部署目录复制后修改实现多实例） |
| DisplayName | 服务显示名 |
| Description | 服务描述 |
| UseAutorun | 是否使用登录自启动（Windows） |
| WatchInterval | 监控循环间隔（秒）默认数秒级，影响资源检查频率 |
| FreeMemoryInterval | 间隔触发一次主动内存回收（秒）0=关闭 |
| MaxMemory | 进程工作集上限（MB），超限自动重启 |
| MaxThread | 线程数上限，超限自动重启 |
| MaxHandle | 句柄数上限，超限自动重启（Windows） |
| AutoRestart | 定时重启间隔（分钟）0=关闭 |
| RestartTimeRange | 允许执行定时重启的时间段，例如 `02:00-05:00`（避免高峰重启） |
| WatchDog | 逗号/分号分隔的需要守护的其它系统服务名列表 |
| AfterStart | 服务启动后要额外拉起的命令或进程（如外部脚本/子进程） |

示例（伪 JSON 仅说明，实际以项目配置格式存储）：
```json
{
  "ServiceName": "DemoAgent",
  "DisplayName": "演示服务",
  "WatchInterval": 5,
  "MaxMemory": 1024,
  "MaxThread": 500,
  "MaxHandle": 20000,
  "AutoRestart": 720,
  "RestartTimeRange": "02:00-05:00",
  "WatchDog": "Redis,nginx",
  "AfterStart": "dotnet SomeWorker.dll"
}
```

---
## 健康监控与重启策略
周期任务 `DoCheck` 按顺序执行：
1. 内存检查（WorkingSet 超阈值 → Restart）
2. 线程数检查
3. 句柄数检查
4. 定时重启（运行分钟数达到 AutoRestart，并在允许时间段内）
5. 看门狗（守护其它服务）

一旦任一检查触发重启，后续检查当次跳过。重启通过底层 `Host.Restart(ServiceName)` 调用平台服务管理器完成，提升稳定性（避免内部状态损坏时继续运行）。

内存回收：按 `FreeMemoryInterval` 主动执行一次 GC + LOH Compact，并在 Windows 下调用 `EmptyWorkingSet` 释放工作集。

---
## 看门狗 WatchDog
配置 `WatchDog = "ServiceA,ServiceB"` 后：
- 管理循环会检查目标服务是否“已安装但未运行”
- 发现停止自动调用对应主机 API 启动
- 在 Systemd + SysVinit 并存环境中会智能探测实际托管方式

适用于：主服务代理统一守护 Nginx / Redis / 业务自建服务 等关键进程。

---
## 自启动 vs 系统服务
- 系统服务（默认）：无需登录即可运行；适合服务器场景
- 自启动（`-autorun` / UseAutorun=true）：写入登录启动项，适合需要访问交互式桌面或简化权限的本地开发/桌面场景

---
## 跨平台主机适配策略
`Init()` 中按顺序选择：
1. Windows: `WindowsAutorun` (当 UseAutorun) 否则 `WindowsService`
2. macOS: `OSXLaunch`
3. Linux: `Systemd` -> `Procd` -> `RcInit`（若均不可用则 `DefaultHost`）

这样在不同发行版仍保持统一命令与运维体验。

---
## 多种守护体系实现
下面列出各平台对应的守护实现及其行为特征，便于按需选择或排障：

- WindowsService（Windows 服务）
  - 通过 Win32 服务控制管理器（SCM）注册和运行，内部使用 `StartServiceCtrlDispatcher`/`RegisterServiceCtrlHandlerEx` 等 API。
  - 支持电源事件、会话变更、时间变更等系统事件回调；安装时自动配置“失败重启”动作。
  - 安装/启停：需要管理员；对应命令 `sc`/`net start|stop`。适合服务器长期托管。

- WindowsAutorun（Windows 登录自启动）
  - 将应用写入注册表 `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`，随登录自动启动。
  - 使用 `.pid` 文件跟踪进程并实现停止；更适合桌面/开发环境或需要交互桌面的场景。
  - 安装/卸载：写入 HKLM 需管理员；启停不依赖 SCM。

- Systemd（Linux systemd）
  - 生成 `<ServiceName>.service` 单元文件到常见目录（优先于 `/etc/systemd/system` 等），并执行 `daemon-reload`/`enable`。
  - 支持设置 `WorkingDirectory`、`ExecStart`、`User/Group` 等；启动/停止/重启通过 `systemctl` 执行。
  - 适用大多数现代 Linux 发行版，是 Linux 优先选择的托管方式。

- RcInit（Linux SysV init 兼容）
  - 生成 `ServiceName.sh` 控制脚本（当前应用目录），并在 `/etc/rc*.d/` 或 `/etc/rc.d/` 创建 `S50/K50` 级别的软链接。
  - 以 `sh ServiceName.sh start|stop|restart` 方式管理；用 `.pid` 文件跟踪进程。
  - 适合仍使用 SysV init 或 BusyBox 的传统/裁剪系统。

- Procd（OpenWrt 专用）
  - 运行时探测 PID 1 是否为 `procd` 判定环境；生成 `ServiceName.sh` 并在 `/etc/rc.d/` 创建 `S50` 链接。
  - 以 `nohup` 启动，`.pid` 文件跟踪，`kill` 停止；贴合 OpenWrt 生态的守护方式。

- OSXLaunch（macOS launchd）
  - 在 `~/Library/LaunchAgents/` 写入 `ServiceName.plist`，设定 `ProgramArguments` 与 `RunAtLoad`，通过 `launchctl` 管理。
  - 适合用户级代理（LaunchAgents）；如需系统级可按需调整到 LaunchDaemons。

- DefaultHost（默认主机）
  - 无平台集成能力，仅作为兜底执行 `Run/Stop` 循环；通常只有在无法检测到任何平台守护体系时才会落到此实现。

> WatchDog 在 `Systemd` 主机中守护其它服务时，会智能探测被守护服务实际托管方式（Systemd/SysV），避免“已安装但未检测到”的误判。

---
## 与 NSSM / srvany 对比
| 项目 | 角色定位 | 是否需包装外部程序 | 代码内可扩展 | 健康监控 | 看门狗 | 多实例配置 |
| ---- | -------- | ------------------ | ------------ | -------- | ------ | ---------- |
| NewLife.Agent | 框架+运行 | 否（直接引用库） | 是（命令/逻辑可扩展） | 内置（内存/线程/句柄/定时） | 是 | 是 |
| NSSM / srvany | 外部包装工具 | 是 | 否 | 否 | 否 | 需额外脚本 |

---
## 常见问题 FAQ
1. 安装/启动失败？确保以管理员（Windows）或 root（Linux sudo）运行。
2. 服务名重复？修改配置的 ServiceName 或复制目录后再安装。
3. 调试困难？使用 `-run` 在当前控制台内直接执行业务逻辑。
4. 如何查看日志？默认控制台输出 + 文本文件日志（根据 NewLife.Core 配置），可自定义 `XTrace.Log`。
5. 健康阈值如何关闭？将对应 MaxMemory / MaxThread / MaxHandle / AutoRestart 设为 0。
6. 重启频繁？检查是否阈值过低或业务内存泄漏；可暂时调大阈值并观察日志。
7. WatchDog 不生效？确认被守护服务已正确安装且名称与配置大小写/空格一致。
8. 多实例部署？复制程序目录并分别修改配置（或启动参数）中的 ServiceName 后各自安装。

---
## 服务控制
一个服务代理示例跑起来的样子  
![image.png](https://cdn.nlark.com/yuque/0/2020/png/1144030/1585307461258-ca6e7880-b2ef-4d87-aed7-35bace08a149.png#align=left&display=inline&height=202&name=image.png&originHeight=404&originWidth=926&size=60071&status=done&style=none&width=463)  
![image.png](https://cdn.nlark.com/yuque/0/2020/png/1144030/1585547897654-f6b570c5-cbfd-4f4e-86c2-a9d711a53c58.png#align=left&display=inline&height=207&name=image.png&originHeight=414&originWidth=1129&size=60665&status=done&style=none&width=564.5)  
这是Agent的标准控制台（Windows和Centos）。上面是该服务的状态信息，下面是控制菜单。  
示例分析：  
- 服务名 XAgent/StarAgent，可以命令启动停止，Windows是`net start XAgent/net stop XAgent`，Linux是`systemctl start StarAgent/systemctl stop StarAgent`。
- 显示名“新生命服务代理”是在windows服务控制板里面看到的名字
- 下一段信息给出了NewLife.Agent和当前应用的版本信息和编译时间
- 黄色菜单可通过按键选择相应操作，内置012345，可自定义其它按键操作
- 菜单1，显示状态，按下1后刷新状态信息
- 菜单2，安装服务或卸载服务，安装成功后，显示信息变为卸载服务，反之亦然
- 菜单3，启动服务或停止服务，安装后才可以看见
- 菜单4，重启服务，安装且运行后可以看见
- 菜单5，模拟运行，在当前进程启动应用主逻辑，用于业务逻辑调试，等同于Windows服务调用
- 菜单0，退出应用服务

---
## 关于net8+的使用
MyServices8+.cs、Program8+.cs是net8+的demo，暂时是注释的，有需要可以参考

**！！！注意，服务安装、卸载、启动、停止，在Windows/Linux上需要管理员权限运行**  

服务应用在Windows上以本地用户权限运行，有最高权限；  
服务应用在Linux上以root权限运行，有最高权限；  
该设计尽管带来了一定安全风险，但能够避免绝大部分初级用户的简单问题，优先易用性。  

---
## 快速拥有
​ 使用NewLife组件的最简便方式是从Nuget引用，例如在项目Nuget管理中搜索`NewLife.Agent` 并引入。  
​ NewLife组件由社区共创20多年，使用MIT开源协议，**任何人可任意修改并再次发行**（无需声明来源）！许多企业基于此构建内部开发框架时，甚至可通过批量替换源码中所有`NewLife`字符串为贵公司名实现私有化定制。  
​ 团队始终秉承开放态度，不仅支持VisualStudio（最新正式版）打开解决方案编译，也兼容`dotnet build`命令行编译，项目文件摒弃复杂功能以追求简单易用，真正做到开箱即用。  
​ 我们公开强命名证书`newlife.snk`以支持独自编译替换程序集。  

​ 命令行中运行以下命令快速体验NewLife组件：
```
dotnet new install NewLife.Templates
dotnet new service --name agent
cd agent
dotnet run
```

---
## 新生命项目矩阵
各项目默认支持net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                        |
| :--------------------------------------------------------------: | :---: | ------------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                                  |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                         |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表，读写分离 |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp长连接）                     |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | 协议通信库，提供CS应用通信框架，支持Http/RPC通信框架，高吞吐，物联网设备低开销易接入        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证               |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd                   |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板NewLife.Templates，Web、WebApi、Service        |
|                              中间件                              |       | 对接知名中间件平台                                                                          |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证                   |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                     |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                               |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                             |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持ZeroIoT平台和IoTEdge网关            |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                      |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图、腾讯地图、天地图                                        |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | 音频编解码库，PCM/ADPCMA/G711A/G722U/WAV/AAC                                                |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                      |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心                   |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证         |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                           |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus、MQTT                      |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | 简易文件存储，支持分布式系统中文件集中存储。                                                |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                           |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                            |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                           |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                      |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET8主机，应用于工业、农业、交通、医疗             |
|                          FIoT物联网平台                          | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证      |
|                        UWB高精度室内定位                         | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证             |

---
## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的80多个开源项目已被广泛应用于各行业，Nuget累计下载量高达400余万次。  
团队开发的大数据中间件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台FIoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IoT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
