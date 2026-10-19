# NewLife.Agent - クロスプラットフォームサービスデーモンフレームワーク

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Agent?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Agent?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Agent?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Agent?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Agent/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Agent?logo=github)](https://github.com/NewLifeX/NewLife.Agent/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> この翻訳は中国語版より遅れている場合があります。

NewLife.Agent は .NET アプリを OS のデーモンとして登録し、Windows サービス・Linux systemd・macOS launchd を統一します。ヘルス監視、自動再起動、Web 管理画面も提供します。

## インストール

```bash
dotnet add package NewLife.Agent
```

## クイックスタート

```csharp
var service = new MyService();
service.Start(args);
```

## 主な機能

- クロスプラットフォーム：Windows / Linux / macOS / ARM64 / LoongArch64
- ログ、トレーシング、実行時診断を充実
- NewLife エコシステム（XCode/Cube/Remoting）とシームレスに連携

## ドキュメント

> 📖 詳細ドキュメント（中国語）： [リポジトリ内ドキュメント](Doc)

## ライセンス

MIT License © 2002-2026 NewLife チーム

[GitHub](https://github.com/NewLifeX/NewLife.Agent) | [Website](https://newlifex.com)
