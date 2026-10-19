<!-- generated from README.md by sync-readme-package.ps1; do not edit -->

# NewLife.Agent - Cross-Platform Service Daemon & Ops Framework

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.agent?logo=github)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Agent?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Agent?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Agent?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Agent?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](https://github.com/NewLifeX/NewLife.Agent/blob/master/Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Agent?logo=github)](https://github.com/NewLifeX/NewLife.Agent/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.zh-CN.md) | [繁體中文](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.zh-TW.md) | [Español](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.es.md) | [Deutsch](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.de.md) | [日本語](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.ja.md) | [Français](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.fr.md) | [Português](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.pt.md) | [Русский](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.ru.md) | [한국어](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.ko.md) | [العربية](https://github.com/NewLifeX/NewLife.Agent/blob/master/README.ar.md)
<!-- lang:end -->

Tutorial: https://newlifex.com/core/agent

---
## Overview

`NewLife.Agent` is a service-oriented framework for long-running applications. It enables console/Web/Worker programs to be quickly hosted as operating system services, and provides unified install, start/stop, monitoring, restart, and troubleshooting capabilities.

Supported platforms:

- Windows (Service / startup at login)
- Linux (Systemd / Procd / RcInit, etc.)
- macOS (LaunchAgent)

Supported frameworks:

- `NewLife.Agent`: `net40;net45;net461;netstandard2.0;netstandard2.1;net6.0;net7.0;net8.0;net9.0;net10.0`
- `NewLife.Extensions.Hosting.AgentService`: `netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0`

---

## Highlights

- **Integrated Development and Operations**: Inherit `ServiceBase` to gain full service governance capabilities.
- **Built-in Web Dashboard**: A lightweight management panel for viewing status and performing operations in a browser, with extension panels and theme switching, built-in token authentication and brute-force protection; it listens on localhost by default (`WebBind` can expose it to the LAN).
- **Cross-Platform Auto-Adaptation**: Automatically selects the most suitable host implementation per operating system.
- **NativeAOT Native Publishing Support**: Built-in commands and the Web dashboard are fully adapted for trimming/AOT; `net8.0+` builds produce zero `IsAotCompatible` warnings, and CI covers 12 RIDs: 9 targets run real smoke tests (8 native targets + linux-loongarch64 under QEMU user-mode emulation), while cross targets (linux-arm/linux-musl-arm/linux-musl-arm64) get publish verification.
- **Multi-Architecture Unit Tests**: win-x64 / linux-x64 / linux-arm64 run natively, and linux-loongarch64 (Loongson) runs the full test suite under QEMU user-mode emulation; triggered by push/PR and supports manual runs.
- **Built-in Command System**: Supports `-install/-start/-stop/-restart/-status/-run`.
- **Health Monitoring & Self-Healing**: The HealthMonitor component provides memory/thread/handle thresholds, scheduled restarts, and WatchDog health checks.
- **Multi-Instance Friendly**: Configure different `ServiceName` values to deploy multiple instances of one program.
- **Host Integration Extension Package**: Bring ASP.NET Core/Worker applications under unified service lifecycle management.

---

## Getting Started

### 1) Minimal Service

Inherit `ServiceBase` and call `Main(args)` at the entry point:

- Override `StartWork(String reason)` to start your business logic
- Override `StopWork(String reason)` to stop and clean up resources

### 2) Command-Line Operations

Common commands:

- `-i`: Install the service
- `-install`: Install and start the service
- `-start`: Start the service
- `-stop`: Stop the service
- `-restart`: Restart the service
- `-status`: View service status
- `-u`: Uninstall the service
- `-uninstall`: Stop and uninstall the service
- `-run`: Run in console mode (for debugging)

> Install/uninstall/start/stop typically require Administrator (Windows) or root (Linux) privileges.

### 3) ASP.NET Core / Worker Integration

Reference `NewLife.Extensions.Hosting.AgentService` and call `UseAgentService()` on your `HostBuilder` to bridge the generic host lifecycle with Agent service governance.

---

## Documentation

- [NewLife.Agent User & Ops Manual (Chinese)](https://github.com/NewLifeX/NewLife.Agent/blob/master/Doc/Agent-使用与运维手册.md)
- [Requirements Document (Chinese)](https://github.com/NewLifeX/NewLife.Agent/blob/master/Doc/需求文档.md)
- [Feature List (Chinese)](https://github.com/NewLifeX/NewLife.Agent/blob/master/Doc/功能清单.md)
- [Architecture Design (Chinese)](https://github.com/NewLifeX/NewLife.Agent/blob/master/Doc/架构设计.md)
- [Competitive Analysis (Chinese)](https://github.com/NewLifeX/NewLife.Agent/blob/master/Doc/竞品分析.md)
- [Changelog (Chinese)](https://github.com/NewLifeX/NewLife.Agent/blob/master/ChangeLog.md)
- [Changelog (English)](https://github.com/NewLifeX/NewLife.Agent/blob/master/ChangeLog.en.md)

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
All projects target net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5 by default; the legacy version (2024.0801) supports net4.0/net2.0.

| Project | Year | Description |
|:-------:|:----:|-------------|
| **Core Components** | | Foundation for middleware and products |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Core library — logging, config, caching, networking, serialization, APM tracing |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Big data ORM — 10B+ rows per table, MySQL/SQLite/SQL Server/Oracle/PostgreSQL/DaMeng, auto-sharding, read/write splitting |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Network library — 22.66M tps per node, 4M concurrent TCP long connections per node |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Protocol communication library — CS application communication framework with HTTP/RPC support, high throughput, low overhead and easy access for IoT devices |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Rapid development platform — user permissions, SSO, OAuth server, verified at 10B+ row scale |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Service management — install apps as OS daemons, Windows Services, Linux Systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Zero scaffolding — project templates (Web, WebApi, Service) based on NewLife ecosystem |
| **Middleware** | | Integration with major middleware platforms |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Redis client — microsecond latency, million-level throughput, rich message queues, verified at 10B+ row scale |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Fully managed RocketMQ client — Apache RocketMQ & Alibaba Cloud MQ, verified at 1B+ scale |
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
| **FIoT Platform** | 2020 | IoT total solution — integrated software, hardware and big-data analytics for construction, environmental protection and agriculture, verified in a single-machine 100K+ point project |
| **UWB Indoor Positioning** | 2020 | Centimeter-level (10~20cm) high-precision indoor positioning — integrated hardware/software, interoperates with other systems, verified in large exhibition hall projects |

---

## NewLife Team

![XCode](https://newlifex.com/logo.png)

The NewLife team, founded in 2002, is a new-era IoT industry solution provider dedicated to software/hardware application consulting, system architecture planning, and development services.
The team's 80+ open-source projects have been widely used across industries, with cumulative NuGet downloads exceeding 4 million.
Flagship products developed by the team — NewLife.XCode big data middleware, AntJob distributed computing platform, Stardust distributed platform, NewLife.Redis cache queue component, and the FIoT IoT platform — have been successfully deployed in power, education, internet, telecommunications, transportation, logistics, industrial control, healthcare, and cultural heritage sectors, delivering a large number of advanced, reliable, secure, high-quality, and easily extensible products and system integration services to customers.

We will continuously improve our services to become a long-term trusted partner of our customers, and through continuous innovation and development, become an excellent domestic IoT service provider.

The NewLife team started in 2002; some open-source projects have a history of more than 20 years, and the source repository retains all modification records since 2010.
Website: https://newlifex.com
GitHub: https://github.com/newlifex
QQ Group: 1600800/1600838
WeChat Official Account:
![智能大石头](https://newlifex.com/stone.jpg)
