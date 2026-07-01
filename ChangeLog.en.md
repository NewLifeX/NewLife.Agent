# NewLife.Agent Changelog

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
