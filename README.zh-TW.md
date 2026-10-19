# NewLife.Agent - 跨平台服務守護與維運框架

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Agent?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Agent?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Agent?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Agent?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Agent?logo=github)](https://github.com/NewLifeX/NewLife.Agent/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> 本翻譯可能落後，以簡體中文版為準。

NewLife.Agent 把 .NET 應用程式安裝為作業系統守護行程，統一 Windows 服務、Linux Systemd 與 macOS Launchd，並提供健康監控、自動重啟和 Web 管理面板。

## 安裝

```bash
dotnet add package NewLife.Agent
```

## 快速開始

```csharp
var service = new MyService();
service.Start(args);
```

## 核心特性

- 跨平台支援 Windows、Linux、macOS 與 ARM64/LoongArch64
- 日誌、鏈路追蹤與執行診斷完善
- 與 NewLife 生態（XCode/Cube/Remoting）無縫整合

## 文件

> 📖 完整文件（中文）見 [儲存庫文件](Doc)

## 授權

MIT License © 2002-2026 新生命開發團隊

[GitHub](https://github.com/NewLifeX/NewLife.Agent) | [Website](https://newlifex.com)
