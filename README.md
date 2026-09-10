# DSHW v0.1.1

[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-004578)](https://learn.microsoft.com/en-us/windows/apps/winui/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6)](https://www.microsoft.com/windows)

> 一个基于 WinUI 3、.NET 9 和 WebView2 的 Windows 桌面 Workbench，用于更方便地使用 DeepSeek Harness（DSH）。

## 特性

- WinUI 3 原生 Windows 界面和内置 WebView2。
- 一键启动 DSH Web，优先使用全局已安装的 `dsh` 命令；可选使用 `npx` 自动升级。
- 检查 Node.js、npm 和 DSH 环境，并显示 DSH/DSHW 双版本信息。
- 本地服务健康检查和 3080 端口复用。
- DSH 版本检查、更新提示和应用内更新流程。
- 更新日志缓冲、滚动和底部智能跟随。
- 系统托盘、窗口图标和 About 窗口。
- 中文、英文和法文界面。
- 应用启动、语言和系统托盘配置。

## 文档

- [CHANGELOG.md](CHANGELOG.md)：版本与用户可见变化。
- [DEVELOPMENT.md](DEVELOPMENT.md)：架构、关键约束、验证与发布流程。
- [AGENTS.md](AGENTS.md)：自动化 Agent 和维护者必须遵守的仓库规则。

## 环境要求与结构

- Windows 10 1809+ 或 Windows 11。
- .NET 9 SDK。
- Node.js 和 npm。
- WebView2 Runtime。
- `@deepseek-ai/dsh`。

当前唯一保证兼容的 DSH 版本：`v0.1.2-rc.1`，不保证向下兼容。

```text
DSHW.Desktop/Core/              环境检测、DSH 启停、备份和更新
DSHW.Desktop/MainWindow.*       WebView2、状态、更新日志和重启
DSHW.Desktop/InstallGuideWindow.*
                                DSH 未安装时的引导
DSHW.Desktop/Managers/          系统托盘管理
DSHW.Desktop/Strings/           中文、英文和法文资源
DSHW.Desktop/Properties/        启动设置和发布配置
scripts/                        构建、发布、启动和安全清理脚本
.tools/                         Agent 生成的测试和诊断产物，不提交、不发布
```

## 快速开始

在 Windows 开发环境中运行：

```powershell
git clone https://github.com/xingchenyang/DSHW.git
Set-Location DSHW
dotnet restore
dotnet build -c Release
dotnet run
```

发布前先退出正在运行的 DSHW，避免发布文件被锁定。发布脚本位于 `scripts/`，当前保留 `lite` 和 `full` 两种发布形式。

## 配置

可以在应用设置中配置启动行为、语言、系统托盘和其他选项。默认情况下，DSHW 优先调用全局安装的 `dsh`；只有显式开启自动更新时才使用 `npx -y`。

DSHW 使用本地 `127.0.0.1:3080` 与 DSH Web 通信。若该端口已由外部 DSH 服务占用，DSHW 会复用该服务，不在退出时结束外部进程。

## 项目边界

DSHW 是一个个人兴趣项目，旨在提供更加便捷的 Windows 桌面使用体验。

本项目独立开发，与 DeepSeek 官方没有隶属、合作或背书关系。部分代码和实现方案使用 AI 辅助开发。

## 许可证

MIT License
