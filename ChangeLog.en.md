# NewLife.Agent Changelog

## v10.18.2026.1024 (2026-10-24)

### Localization
- **31-language runtime dictionaries**: Adopted NewLife.Core 12 localization, covering built-in commands, configuration descriptions and Hosting messages, with `Locales/*.yml` dictionaries for 31 languages.
- **Localized Web dashboard**: Dashboard sources switched to English, with 31-language UI, language switching API and RTL support; custom panels fall back to the main dictionary when their own is missing.
- **Unified language settings**: Language settings moved to `NewLife.Setting`, and request culture is resolved automatically by NewLife.Core; application titles and descriptions use English sources and are translated at display/registration time.
- **Localized configuration comments**: XML configuration comments are saved in the current language; prefixed log output helps locate the source module.

### Web Management Panel
- **WebBind Configuration**: New `WebBind` setting; listens on localhost by default and can expose to the LAN on demand.
- **Port Conflict Warning**: A single-line warning is emitted when the panel port is occupied, avoiding false alarms of a service crash.
- **Language Status API**: Backend exposes a unified dashboard language status; frontend shows the current language and adapts to RTL.

### NativeAOT & Cross-Platform
- **Full NativeAOT Adaptation**: Built-in commands use static factories, panel responses are DTO-based with AOT serialization metadata preserved, fixing lost commands and empty JSON objects under AOT; `net8.0+` permanently enables the `IsAotCompatible` analyzer with zero warnings as a build gate.
- **Publish Smoke Matrix**: CI covers 12 RIDs, with 8 native targets plus loongarch64 (QEMU) running real smoke tests and cross targets verified by publish.
- **Unit Test Matrix**: win-x64 / linux-x64 / linux-arm64 run natively, and linux-loongarch64 runs the full suite under QEMU user-mode emulation.
- **Instruction-Set Smoke**: Added riscv64 / s390x smoke project and CI to verify niche architectures and big-endian byte order.

### Stability & Fixes
- **[fix]** Fixed SysVinit path lookup throwing when init.d is missing.
- **[fix]** Fixed `UseAgentService()` throwing when called without arguments.
- **[fix]** Command discovery now skips dynamic assemblies and tolerates unloadable types.
- **[fix]** Made Systemd/Install unit tests platform-independent.

### Dependencies & Build
- **NewLife.Core 12**: Upgraded to 12.x (stable 12.0.2026.1024) with v12 compatibility adaptations.
- **Dependency Upgrades**: NewLife.XCode, NewLife.Stardust and others upgraded to the latest versions.
- **CI Publishing**: The publish workflow now pushes to both GitHub Packages and NuGet.org, and creates a GitHub Release on tag push using this changelog as release notes.

---

## v10.17.2026.0802 (2026-08-02)

### Health Monitoring
- **HealthMonitor Component**: New health check component encapsulating memory/thread/handle threshold checks and auto-restart time parsing, callable by ServiceBase and independently testable.
- **Disk IOPS Collection**: Cross-platform disk IOPS collection enhancing dashboard metrics.

### Web Management Panel
- **Auth Refactor & Brute-Force Protection**: Refactored authentication with token-based auth and brute-force protection.
- **Secure Password Change**: New secure password change API with frontend interaction.
- **Frontend Enhancements**: Logout and Enter-key interactions, plus a release-memory action button.
- **Extensibility**: Factory methods and resource replacement, optimized ApiController config metadata retrieval.
- **Log Reading**: Support for log files locked by writing processes.

### CLI & Scripts
- **CLI Parsing**: Refactored command-line parsing and resource monitoring.
- **Script Modularization**: Refactored Procd/RcInit scripts with module IDs M1~M8 and extracted BuildScript method.

### Testing & Quality
- **Unit Test Coverage**: Added comprehensive unit tests covering ServiceHelper, ServiceBase, CommandHandler, WebPanel models, AuthLevel, HealthMonitor and more.
- **XML Docs**: Unified and improved XML documentation comments.

### Bug Fixes
- **[fix]** Fixed Procd/RcInit argument trimming by using TrimArg instead of TrimEnd.
- **[fix]** Removed StartWork retry logic in StartLoop, executing once to avoid duplicate startup.
- **[fix]** Fixed all gaps found during audit.

---

## v10.16.2026.0702 (2026-07-02)

### Web Management Panel
- **Lightweight Web Management Panel**: Added built-in web dashboard for viewing service status and performing operations without server login.
- **Extension Panels & Theme Switching**: Support for third-party extension panels and multiple built-in themes.
- **Unified ApiController**: Refactored WebPanel routing with a unified ApiController for standardized API handling.
- **Authentication**: Built-in token-based authentication for securing management endpoints.

### Documentation
- **Requirements & Competitive Analysis**: Added requirements document and competitive analysis report.
- **Dependency Update**: Upgraded NewLife.Core to the latest version.

---

## v10.15.2026.0402 (2026-04-02)

### Windows Service Enhancement
- **Desktop Process Launching**: Windows services can now launch desktop application processes, enhancing user-mode process startup capability.
- **Service Reliability**: Automatic failure recovery settings during Windows service installation, improving StarAgent stability.

---

## v10.14.2026.0102 (2026-01-02)

### Framework Upgrade
- **.NET 10 Support**: Added net10.0 target framework.

---

## v10.13.2025.1001 (2025-10-01)

### Stability
- **Windows Service Stability**: Enhanced Windows service stability and optimized install/uninstall workflows.
- **Core Library Upgrade**: Upgraded NewLife.Core to latest version.

---

## v10.11.2025.0401 (2025-04-01)

### Features
- **PreShutdown Support**: Windows services support PreShutdown for graceful exit on system shutdown.
- **Namespace Refactor**: Unified global Utility extension namespace to NewLife.

---

## v10.10.2025.0101 (2025-01-01)

### Fixes & Improvements
- **[fix]** Fixed external args missing dll parameter.
- **[fix]** Handled empty args array edge case.
- **Install Refactor**: External ServiceModel for unified command-line parsing.
- **ServiceBase.ProcessCommand Restored**: Enables inherited install/uninstall command interception.

---

## v10.10.2024.1223 (2024-12-23)

### Fixes
- **[fix]** Fixed Systemd service installation error.
- **[fix]** Fixed Win10+NET9 foreground process creation from background service.
- **Unit Tests**: Added Install and Systemd unit test projects.

---

## v10.10.2024.1113 (2024-11-13)

### Framework Upgrade
- **.NET 9 Support**: Added net9.0 target framework.

---

(Earlier versions omitted; see Git commit history for full details)
