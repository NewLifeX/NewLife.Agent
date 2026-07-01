# NewLife.Agent - Cross-Platform Service Daemon & Ops Framework

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.agent?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.agent?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.agent?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.agent?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.agent?label=dev%20nuget&logo=nuget)
![Nuget Downloads](https://img.shields.io/nuget/dt/NewLife.Extensions.Hosting.AgentService?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/NewLife.Extensions.Hosting.AgentService?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/NewLife.Extensions.Hosting.AgentService?label=dev%20nuget&logo=nuget)

Tutorial: https://newlifex.com/core/agent

> [中文文档](./Readme.MD) | [更新日志](./ChangeLog.md) | [Changelog](./ChangeLog.en.md)

---

## Overview

`NewLife.Agent` is a service-oriented framework for long-running applications. It enables console/Web/Worker programs to be quickly hosted as operating system services, with unified install, start/stop, monitoring, restart, and troubleshooting capabilities.

### Supported Platforms

- **Windows** (Windows Service / Startup auto-run)
- **Linux** (Systemd / Procd / RcInit)
- **macOS** (LaunchAgent)

### Supported Frameworks

- `NewLife.Agent`: `net40;net45;net461;netstandard2.0;netstandard2.1;net6.0;net7.0;net8.0;net9.0;net10.0`
- `NewLife.Extensions.Hosting.AgentService`: `netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0`

---

## Highlights

- **DevOps in One**: Inherit `ServiceBase` to gain full service governance capabilities.
- **Built-in Web Dashboard**: Lightweight management panel accessible via browser for status viewing and operations, with extension panels and theme switching.
- **Cross-Platform Auto-Adaptation**: Automatically selects the best host implementation per operating system.
- **Built-in Command System**: Supports `-install/-start/-stop/-restart/-status/-run`.
- **Health Monitoring & Self-Healing**: Memory/thread/handle thresholds, scheduled restarts, WatchDog.
- **Multi-Instance Friendly**: Configure different `ServiceName` values for multi-instance deployment.
- **Host Integration Extension**: Use `NewLife.Extensions.Hosting.AgentService` to unify ASP.NET Core/Worker lifecycles with Agent service governance.

---

## Getting Started

### 1) Minimal Service

Inherit `ServiceBase` and call `Main(args)` at the entry point:

- Override `StartWork(String reason)` to start your business logic
- Override `StopWork(String reason)` to stop and clean up resources

### 2) Command-Line Operations

Common commands:

- `-install`: Install the service
- `-start`: Start the service
- `-stop`: Stop the service
- `-restart`: Restart the service
- `-status`: View service status
- `-uninstall`: Uninstall the service
- `-run`: Run in console mode (for debugging)

> Install/uninstall/start/stop typically require Administrator (Windows) or root (Linux) privileges.

### 3) ASP.NET Core / Worker Integration

Reference `NewLife.Extensions.Hosting.AgentService` and call `UseAgentService()` on your `HostBuilder` to bridge the generic host lifecycle with Agent service governance.

---

## Documentation

- [User & Ops Manual (Chinese)](./Doc/Agent-使用与运维手册.md)
- [Requirements Document (Chinese)](./Doc/需求文档.md)
- [Competitive Analysis (Chinese)](./Doc/竞品分析报告.md)
- [Changelog](./ChangeLog.en.md)

---

## Quick Install

```bash
dotnet new install NewLife.Templates
dotnet new service --name agent
cd agent
dotnet run
```

---

## NewLife Project Matrix

| Project | Year | Description |
|:-------:|:----:|-------------|
| **Core Components** | | Foundation for middleware and products |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Core library — logging, config, caching, networking, serialization, APM tracing |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Big data ORM — 10B+ rows per table, MySQL/SQLite/SQL Server/Oracle/PostgreSQL/DaMeng, auto-sharding, read/write splitting |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Network library — 22.66M tps throughput, 4M concurrent TCP connections per node |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | RPC framework — HTTP/RPC communication, high throughput, IoT device friendly |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Rapid development platform — user permissions, SSO, OAuth server, verified at 10B+ row scale |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Service management — install apps as OS daemons, Windows Services, Linux Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Zero scaffolding — project templates (Web, WebApi, Service) based on NewLife ecosystem |
| **Middleware** | | Integration with major middleware platforms |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Redis client — microsecond latency, million-level throughput, rich message queues |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | RocketMQ client — Apache RocketMQ & Alibaba Cloud MQ, verified at 1B+ scale |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | MQTT — MqttClient/MqttServer, Alibaba Cloud IoT support |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT standard library — protocol specifications for IoT domain |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | Modbus — ModbusTcp/ModbusRTU/ModbusASCII, ZeroIoT & IoTEdge support |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Siemens PLC protocol — IoT platform & IoTEdge support |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Map components — Baidu, AMap, Tencent, Tianditu |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Audio codec — PCM/ADPCMA/G711A/G722U/WAV/AAC |
| **Products** | | Production-ready platforms |
| [Stardust](https://github.com/NewLifeX/Stardust) | 2018 | Distributed service platform — node management, APM, config center, registry, release center |
| [AntJob](https://github.com/NewLifeX/AntJob) | 2019 | Distributed big data compute — real-time & offline, ant-colony sharding, verified at trillion-row scale |
| [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP) | 2021 | Enterprise ERP — product, customer, sales, supplier management |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 2006 | Developer toolkit — networking, serial, encryption, regex, Modbus, MQTT |
| [EasyIO](https://github.com/NewLifeX/EasyIO) | 2023 | Simple file storage — centralized file storage for distributed systems |
| [XProxy](https://github.com/NewLifeX/XProxy) | 2005 | Production reverse proxy — NAT proxy, HTTP proxy |
| [HttpMeter](https://github.com/NewLifeX/HttpMeter) | 2022 | HTTP stress testing tool |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 2015 | Git source code management |
| [SmartOS](https://github.com/NewLifeX/SmartOS) | 2014 | Embedded OS — fully independent, ARM Cortex-M |
| [SmartA2](https://github.com/NewLifeX/SmartA2) | 2019 | Embedded industrial computer — IoT edge gateway, .NET 8, industrial/agricultural/transportation/medical |
| **FIoT Platform** | 2020 | IoT total solution — construction, environmental, agriculture, verified at 100K+ nodes |
| **UWB Indoor Positioning** | 2020 | Centimeter-level (10~20cm) high-precision indoor positioning |

---

## NewLife Team

![XCode](https://newlifex.com/logo.png)

The NewLife team, founded in 2002, is a next-generation IoT solution provider dedicated to software/hardware application consulting, system architecture planning, and development services.
Our 80+ open-source projects are widely used across industries, with over 4 million total NuGet downloads.
Flagship products — NewLife.XCode big data ORM, AntJob distributed compute, Stardust distributed platform, NewLife.Redis caching, and the FIoT platform — have been successfully deployed in power, education, internet, telecommunications, transportation, logistics, industrial control, healthcare, and cultural heritage sectors.

Website: https://newlifex.com
GitHub: https://github.com/newlifex
QQ Group: 1600800 / 1600838
