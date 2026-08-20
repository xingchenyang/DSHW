# DSHW

DSH Workbench for Windows

[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2B-0078D6)](https://www.microsoft.com/windows)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-004578)](https://learn.microsoft.com/en-us/windows/apps/winui/)

> 一个基于 WinUI 3 和 WebView2 的 Windows 桌面 Workbench，用于更方便地使用 DeepSeek Harness。

## ✨ 特性

- 🖥️ WinUI 3 原生 Windows 界面
- 🌐 内置 WebView2
- 🚀 一键启动 DSH Web
- 🔍 Node.js / npm / dsh 环境检查
- 🔄 dsh 版本检查与更新提示
- ❤️ 本地服务健康检查
- 📌 系统托盘支持
- 🌍 多语言支持
- ⚙️ 应用与启动配置

## 📦 系统要求

- Windows 10 1809+ / Windows 11
- Node.js / npm
- WebView2 Runtime
- `@deepseek-ai/dsh`

## 🚀 快速开始

```bash
git clone https://github.com/xingchenyang/DSHW.git
cd DSHW
dotnet restore
dotnet build -c Release
dotnet run

---

## 🔧 配置

可以在应用设置中配置启动行为、语言、系统托盘和其他选项。

---

## 📖 关于本项目

DSHW 是一个个人兴趣项目，旨在提供更加便捷的 Windows 桌面使用体验。

本项目独立开发，与 DeepSeek 官方没有隶属、合作或背书关系。

部分代码和实现方案使用 AI 辅助开发。

---

## 📄 许可证

MIT License