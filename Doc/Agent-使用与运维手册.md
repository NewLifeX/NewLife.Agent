# NewLife.Agent 使用与运维手册

## 文档说明

本文档汇总 `NewLife.Agent` 与 `NewLife.Extensions.Hosting.AgentService` 的核心能力、架构设计与使用方法。

> 分析范围说明：遵循 `NewLife.Agent.csproj` 已排除项，不包含 `BackgroundService.cs`、`IHostedService.cs`，且不分析 `bin/obj` 目录产物。

## 目录

- [NewLife.Agent 使用与运维手册](#newlifeagent-使用与运维手册)
  - [文档说明](#文档说明)
  - [目录](#目录)
  - [产品概述](#产品概述)
    - [核心价值](#核心价值)
  - [核心架构](#核心架构)
    - [架构分层](#架构分层)
    - [核心入口](#核心入口)
    - [核心职责](#核心职责)
      - [`ServiceBase`](#servicebase)
      - [`IHost` / `DefaultHost`](#ihost--defaulthost)
      - [命令工厂](#命令工厂)
    - [扩展设计要点](#扩展设计要点)
  - [跨平台主机适配](#跨平台主机适配)
    - [主机选择策略](#主机选择策略)
    - [实现能力对比](#实现能力对比)
    - [平台关键点](#平台关键点)
      - [WindowsService](#windowsservice)
      - [WindowsAutorun](#windowsautorun)
      - [Systemd](#systemd)
      - [Procd / RcInit](#procd--rcinit)
      - [OSXLaunch](#osxlaunch)
    - [运维建议](#运维建议)
  - [命令系统与交互菜单](#命令系统与交互菜单)
    - [命令执行架构](#命令执行架构)
    - [内置命令](#内置命令)
    - [交互菜单](#交互菜单)
    - [扩展自定义命令](#扩展自定义命令)
  - [服务生命周期与健康监控](#服务生命周期与健康监控)
    - [生命周期总览](#生命周期总览)
    - [运行循环](#运行循环)
    - [健康检查项](#健康检查项)
    - [内存回收策略](#内存回收策略)
    - [退出与兜底](#退出与兜底)
    - [实战建议](#实战建议)
  - [配置文件说明](#配置文件说明)
    - [基础信息](#基础信息)
    - [运行与监控](#运行与监控)
    - [自动重启与看门狗](#自动重启与看门狗)
    - [多实例部署建议](#多实例部署建议)
    - [配置策略建议](#配置策略建议)
  - [ASP.NET Core 与 Worker 集成](#aspnet-core-与-worker-集成)
    - [关键组件](#关键组件)
    - [集成方式](#集成方式)
    - [生命周期桥接](#生命周期桥接)
    - [适用场景](#适用场景)
    - [实践建议（集成）](#实践建议集成)
  - [常见问题与排障](#常见问题与排障)
    - [1）安装或启动失败](#1安装或启动失败)
    - [2）服务已安装但无法运行](#2服务已安装但无法运行)
    - [3）重启过于频繁](#3重启过于频繁)
    - [4）WatchDog 不生效](#4watchdog-不生效)
    - [5）如何调试服务逻辑](#5如何调试服务逻辑)
    - [6）多实例如何部署](#6多实例如何部署)
    - [7）跨平台迁移注意事项](#7跨平台迁移注意事项)

---
## 产品概述

`NewLife.Agent` 是“服务开发基类 + 跨平台宿主 + 运维命令体系”的统一框架，面向长期运行应用（控制台/Web/Worker/数据处理服务）提供标准化服务治理能力。

### 核心价值

- 统一安装、卸载、启停、重启、状态查询
- 跨平台自动选择宿主实现
- 运行期健康监控与自动自愈
- 命令行与交互菜单双模式
- 可扩展命令与业务生命周期钩子

---
## 核心架构

### 架构分层

- **服务抽象层**：`ServiceBase`
- **宿主抽象层**：`IHost` + `DefaultHost`
- **平台实现层**：`WindowsService` / `WindowsAutorun` / `Systemd` / `Procd` / `RcInit` / `SysVinit` / `OSXLaunch`
- **命令执行层**：`CommandFactory` + 各 `*CommandHandler`
- **配置模型层**：`Setting`、`ServiceModel`、`ServiceConfig`、`SystemdSetting`

### 核心入口

统一入口在 `ServiceBase.Main(String[] args)`，关键流程：

1. 初始化环境 `InitService()`
2. 选择宿主并加载配置 `Init()`
3. 解析命令（如 `-install`/`-start`）或进入交互菜单
4. 启动服务循环 `StartLoop()` -> `DoLoop()` -> `StopLoop()`

### 核心职责

#### `ServiceBase`

- 统一服务元数据：`ServiceName`/`DisplayName`/`Description`
- 管理运行状态：`Running`
- 执行健康检查：内存/线程/句柄/定时重启/看门狗
- 提供业务扩展点：`StartWork(reason)`、`StopWork(reason)`

#### `IHost` / `DefaultHost`

- 约定并实现安装、卸载、启动、停止、重启、状态查询
- 按平台差异隐藏底层命令与系统调用

#### 命令工厂

- 通过反射自动发现命令处理器
- 支持命令别名与快捷键
- 支持菜单可见性控制（`IsShowMenu()`）

### 扩展设计要点

- 面向继承：业务程序通常只需继承 `ServiceBase`
- 面向组合：平台能力通过 `Host` 对象组合，不耦合业务代码
- 面向运维：命令与监控能力内置，无需额外守护包装器

---
## 跨平台主机适配

`ServiceBase.Init()` 会根据当前系统与配置自动选择最合适的宿主实现。

### 主机选择策略

1. Windows：`UseAutorun=true` 时用 `WindowsAutorun`，否则 `WindowsService`
2. macOS：`OSXLaunch`
3. Linux：优先 `Systemd`，其次 `Procd`，再 `RcInit`，最后 `DefaultHost`

### 实现能力对比

| 实现 | 适用平台 | 安装方式 | 典型场景 |
|---|---|---|---|
| `WindowsService` | Windows | SCM 服务注册 | 服务器长期无人值守运行 |
| `WindowsAutorun` | Windows | 注册表 Run 项 | 桌面场景/登录后自启动 |
| `Systemd` | Linux | `.service` + `systemctl` | 现代 Linux 发行版 |
| `Procd` | OpenWrt | init 脚本 + procd | 路由器/嵌入式 Linux |
| `RcInit` | 传统 Linux | SysV init 脚本 | 非 systemd 环境 |
| `SysVinit` | 兼容查询 | 状态识别为主 | 历史兼容 |
| `OSXLaunch` | macOS | LaunchAgents plist | macOS 用户级守护 |
| `DefaultHost` | 任意 | 不做系统注册 | 兜底运行 |

### 平台关键点

#### WindowsService

- 使用 Win32 服务控制 API 与 SCM 交互
- 支持电源、会话、时间变更等系统事件

#### WindowsAutorun

- 写入 `HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run`
- 适合需要用户会话上下文的程序

#### Systemd

- 自动生成并写入 `<ServiceName>.service`
- 调用 `daemon-reload`、`enable`、`start` 完成服务管理

#### Procd / RcInit

- 采用脚本和 pid 文件机制控制进程
- 面向轻量系统与历史体系兼容

#### OSXLaunch

- 通过 `launchctl` 管理 `~/Library/LaunchAgents/*.plist`

### 运维建议

- 服务器优先使用 `WindowsService` 或 `Systemd`
- 桌面开发调试可用 `WindowsAutorun` 或 `-run`
- 嵌入式设备优先根据发行版选择 `Procd`/`RcInit`

---
## 命令系统与交互菜单

`NewLife.Agent` 内置“命令行 + 菜单”双模式，满足自动化脚本与人工运维两类场景。

### 命令执行架构

- 命令契约：`ICommandHandler`
- 命令基类：`BaseCommandHandler`
- 命令发现与分发：`CommandFactory`

`CommandFactory` 会扫描继承 `BaseCommandHandler` 的类型，构建命令映射并处理冲突校验。

### 内置命令

| 命令 | 说明 |
|---|---|
| `-install` | 安装服务 |
| `-uninstall` / `-remove` | 卸载服务 |
| `-start` | 启动服务 |
| `-stop` | 停止服务 |
| `-restart` | 重启服务 |
| `-status` | 查看状态 |
| `-run` | 当前进程模拟运行（调试） |
| `-watchdog` | 立即执行一次看门狗检查 |
| `-installstart` | 安装并启动（组合命令） |
| `-reinstall` | 重新安装 |

### 交互菜单

程序无参数启动时进入交互模式：

- 展示当前服务状态（是否安装、是否运行）
- 显示可执行菜单项（根据命令可见性动态生成）
- 用户按键后执行对应命令处理器

### 扩展自定义命令

1. 新建命令处理器并继承 `BaseCommandHandler`
2. 定义 `Cmd`、`Description`、`ShortcutKey`
3. 实现 `Process(String[] args)`
4. 程序启动后将被命令工厂自动发现

---
## 服务生命周期与健康监控

本章描述服务从启动到退出的完整行为，以及运行期自愈策略。

### 生命周期总览

核心调用顺序：

1. `Main(args)`
2. `InitService()`
3. `Init()`
4. `StartLoop()`
5. `DoLoop()`（循环）
6. `StopLoop()`

业务程序通常只需重写：

- `StartWork(String reason)`：启动业务逻辑
- `StopWork(String reason)`：停止并释放资源

### 运行循环

`DoLoop()` 负责：

- 按 `WatchInterval` 周期执行检查
- 持续观察进程健康
- 满足重启条件时通过 `Host.Restart(ServiceName)` 触发外部重启

### 健康检查项

`DoCheck()` 默认顺序：

1. `CheckMemory()`：工作集内存超过 `MaxMemory`（MB）
2. `CheckThread()`：线程数超过 `MaxThread`
3. `CheckHandle()`：句柄数超过 `MaxHandle`（Windows）
4. `CheckAutoRestart()`：达到 `AutoRestart` 设定分钟数并落入允许时段
5. `CheckWatchDog()`：检查并拉起被守护服务

### 内存回收策略

- 当配置 `FreeMemoryInterval` 时，定期执行主动内存整理
- 包含 GC 与平台相关的工作集释放逻辑

### 退出与兜底

- 进程退出事件会触发收尾逻辑，避免“业务运行中主进程异常退出”导致状态不一致
- `StopLoop()` 内会清理运行状态并尝试终止子进程对象

### 实战建议

- 生产环境先给出宽松阈值，再依据日志逐步收紧
- 自动重启窗口建议设置在低峰时段（配合 `RestartTimeRange`）
- 关键业务建议同时开启外部监控系统形成双保险

---
## 配置文件说明

`Setting.Current` 提供统一配置入口，典型配置项如下。

### 基础信息

| 字段 | 说明 |
|---|---|
| `ServiceName` | 系统服务名（唯一标识） |
| `DisplayName` | 服务显示名 |
| `Description` | 服务描述 |
| `UseAutorun` | Windows 是否使用登录自启动 |

### 运行与监控

| 字段 | 说明 |
|---|---|
| `WatchInterval` | 监控循环间隔（秒） |
| `FreeMemoryInterval` | 主动内存整理间隔（秒），0 表示关闭 |
| `MaxMemory` | 最大工作集内存（MB），超限自动重启 |
| `MaxThread` | 最大线程数，超限自动重启 |
| `MaxHandle` | 最大句柄数，超限自动重启 |

### 自动重启与看门狗

| 字段 | 说明 |
|---|---|
| `AutoRestart` | 定时重启间隔（分钟），0 表示关闭 |
| `RestartTimeRange` | 定时重启允许时间段，例如 `02:00-05:00` |
| `WatchDog` | 被守护服务列表（逗号/分号分隔） |
| `AfterStart` | 本服务启动后额外执行的命令 |

### 多实例部署建议

同一程序可通过“复制部署目录 + 修改配置中的 `ServiceName`/`DisplayName`”形成多个独立实例。

### 配置策略建议

- 先保证可用，再逐步开启严格阈值
- 对内存敏感业务建议开启 `FreeMemoryInterval`
- `WatchDog` 仅配置关键基础服务，避免链式干扰

---
## ASP.NET Core 与 Worker 集成

`NewLife.Extensions.Hosting.AgentService` 把 `ServiceBase` 生命周期与 `Microsoft.Extensions.Hosting` 融合，适配 Web/Worker 服务化部署。

### 关键组件

| 组件 | 职责 |
|---|---|
| `ServiceLifetime` | 继承 `ServiceBase` 并实现 `IHostLifetime` |
| `ServiceLifetimeHostBuilderExtensions` | 提供 `UseAgentService()` 扩展方法 |
| `ServiceLifetimeOptions` | 承载服务元数据配置 |

### 集成方式

在 HostBuilder 上调用 `UseAgentService()`，即可把 Agent 服务能力注入通用主机。

核心收益：

- 保留 ASP.NET Core/Worker 原有生命周期
- 增加服务安装、启停、重启、监控能力
- 保持跨平台一致的运维入口

### 生命周期桥接

- `WaitForStartAsync`：注册主机事件并异步启动 Agent 运行线程
- `StopAsync`：等待停止信号并完成资源收尾
- 通过 `IHostApplicationLifetime` 绑定 Started/Stopping/Stopped 事件

### 适用场景

1. ASP.NET Core API 以系统服务方式部署
2. Worker 长驻任务纳入统一治理
3. 希望“应用开发模型”与“运维服务模型”统一

### 实践建议（集成）

- 在开发环境优先使用 `-run` 调试业务逻辑
- 生产环境交由系统服务托管（WindowsService/Systemd）
- 结合 `AutoRestart` 与外部监控形成稳定运行闭环

---
## 常见问题与排障

### 1）安装或启动失败

- Windows 请使用管理员权限
- Linux/macOS 请确认具备写系统服务目录和执行管理命令权限

### 2）服务已安装但无法运行

- 检查 `ServiceName` 是否与系统注册项一致
- 查看系统服务日志（Windows 事件查看器 / Linux journalctl）
- 检查 `AfterStart` 命令是否异常退出

### 3）重启过于频繁

- 先观察是否触发 `MaxMemory`/`MaxThread`/`MaxHandle`
- 临时放宽阈值并结合业务日志定位根因
- 检查 `AutoRestart` 与 `RestartTimeRange` 是否配置过于激进

### 4）WatchDog 不生效

- 确认被守护目标服务已正确安装
- 校验 `WatchDog` 名称拼写与分隔符
- 在 Linux 上确认目标服务托管体系（systemd / sysv）

### 5）如何调试服务逻辑

- 使用 `-run` 在前台模拟运行
- 在 `StartWork/StopWork` 增加关键业务日志
- 先保证业务逻辑可独立运行，再切回系统服务模式

### 6）多实例如何部署

- 复制程序目录
- 分别配置不同 `ServiceName` 与 `DisplayName`
- 在各目录分别执行安装和启动

### 7）跨平台迁移注意事项

- 不同平台的权限模型与服务目录不同
- Linux 建议优先使用 systemd
- 桌面 Windows 若依赖交互会话可选 autorun 模式
