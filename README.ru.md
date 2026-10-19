# NewLife.Agent - Кроссплатформенный фреймворк служб-демонов

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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Перевод может отставать от китайской версии.

NewLife.Agent устанавливает приложения .NET как системные демоны, объединяя службы Windows, systemd Linux и launchd macOS, с мониторингом, автоперезапуском и веб-панелью.

## Установка

```bash
dotnet add package NewLife.Agent
```

## Быстрый старт

```csharp
var service = new MyService();
service.Start(args);
```

## Возможности

- Кроссплатформенность: Windows, Linux, macOS, ARM64/LoongArch64
- Полное журналирование, трассировка и диагностика выполнения
- Бесшовная интеграция с экосистемой NewLife (XCode/Cube/Remoting)

## Документация

> 📖 Полная документация (на китайском): [документы репозитория](Doc)

## Лицензия

MIT License © 2002-2026 Команда NewLife

[GitHub](https://github.com/NewLifeX/NewLife.Agent) | [Website](https://newlifex.com)
