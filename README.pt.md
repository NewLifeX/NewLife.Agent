# NewLife.Agent - Framework de daemon de serviço multiplataforma

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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Esta tradução pode estar desatualizada em relação à versão em chinês.

NewLife.Agent instala aplicações .NET como daemons do sistema, unificando serviços do Windows, systemd do Linux e launchd do macOS, com monitoramento, reinício automático e painel web.

## Instalação

```bash
dotnet add package NewLife.Agent
```

## Início rápido

```csharp
var service = new MyService();
service.Start(args);
```

## Recursos

- Multiplataforma: Windows, Linux, macOS e ARM64/LoongArch64
- Registro, rastreamento e diagnóstico em tempo de execução completos
- Integração perfeita com o ecossistema NewLife (XCode/Cube/Remoting)

## Documentação

> 📖 Documentação completa (em chinês): [documentos do repositório](Doc)

## Licença

MIT License © 2002-2026 Equipe NewLife

[GitHub](https://github.com/NewLifeX/NewLife.Agent) | [Website](https://newlifex.com)
