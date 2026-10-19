# NewLife.Agent - إطار خدمات خفية متعدد المنصات

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Agent?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Agent?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Agent?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Agent?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Agent?logo=github)](https://github.com/NewLifeX/NewLife.Agent/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> قد تتأخر هذه الترجمة عن النسخة الصينية.

يثبّت NewLife.Agent تطبيقات .NET كخدمات نظام ويوحّد خدمات Windows وsystemd في Linux وlaunchd في macOS، مع مراقبة الحالة وإعادة التشغيل التلقائي ولوحة ويب.

## التثبيت

```bash
dotnet add package NewLife.Agent
```

## البدء السريع

```csharp
var service = new MyService();
service.Start(args);
```

## الميزات الرئيسية

- متعدد المنصات: Windows وLinux وmacOS وARM64/LoongArch64
- تسجيل وتتبع وتشخيص شامل أثناء التشغيل
- تكامل سلس مع منظومة NewLife (XCode/Cube/Remoting)

## التوثيق

> 📖 التوثيق الكامل (بالصينية): [مستندات المستودع](Doc)

## الترخيص

MIT License © 2002-2026 فريق NewLife

[GitHub](https://github.com/NewLifeX/NewLife.Agent) | [Website](https://newlifex.com)
