# NewLife.Agent - 크로스 플랫폼 서비스 데몬 프레임워크

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

> 이 번역은 [English README](README.md)보다 늦을 수 있으며, 영어 버전이 기준입니다.

NewLife.Agent는 .NET 애플리케이션을 OS 데몬으로 설치하며 Windows 서비스, Linux systemd, macOS launchd를 통합합니다. 상태 모니터링, 자동 재시작, 웹 패널을 제공합니다.

## 설치

```bash
dotnet add package NewLife.Agent
```

## 빠른 시작

```csharp
var service = new MyService();
service.Start(args);
```

## 주요 기능

- 크로스 플랫폼: Windows, Linux, macOS, ARM64/LoongArch64
- 로깅, 추적, 런타임 진단 지원
- NewLife 생태계(XCode/Cube/Remoting)와 원활한 통합

## 문서

> 📖 전체 문서(중국어): [저장소 문서](Doc)

## 라이선스

MIT License © 2002-2026 NewLife 팀

[GitHub](https://github.com/NewLifeX/NewLife.Agent) | [Website](https://newlifex.com)
