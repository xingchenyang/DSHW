# DSHW — 项目进度

> 最后更新：2026-08-20 · 版本 0.1.0 · 本文档兼作"对话恢复手册"（中断后交给新对话即可接手）

## 📁 项目信息

| 项 | 值 |
|----|----|
| 名称 | DSHW (DSH Workbench for Windows) |
| 类型 | WinUI 3 桌面应用 · Unpackaged（`WindowsPackageType=None`） |
| 框架 | .NET 9.0 · net9.0-windows10.0.26100.0 · WinAppSDK 2.4.0 拆分包 |
| 核心功能 | `npx -y @deepseek-ai/dsh web` 启动 DSH，WebView2 内嵌其 Web UI（127.0.0.1:3080） |
| 语言策略 | 默认 en-US；fr/zh 跟随系统；其他系统回退英语（`DefaultLanguage=en-US`） |
| 仓库 | `C:\PROJETS-PERSO\DSHW\` · https://github.com/xingchenyang/DSHW |

## ⚠️ 环境事实（必读）

**3080 可能被 DeepSeek Harness GUI 占用**（当前对话就跑在 Harness 里）。DSHW 启动时探测 3080：已有服务→**复用**（退出不杀，防误杀 Harness）；空闲→启动自己的 DSH（退出时杀 cmd 树 + 首次记录的监听 PID，杀前复查 PID 防抢占误杀）。

## ✅ 状态（实机验证）

- 编译 0 错误 0 警告；窗口/托盘/双状态栏/多语言/浅色主题正常
- 托盘右键菜单（显示/隐藏/关于/退出）、关闭最小化、AboutWindow（三语言）
- 三个发布版本实测通过：**full 235.7MB / single 97.6MB / lite 38.6MB**，无嵌套
- ✅ 进程清理实机验证通过（退出后 3080 释放）
- 待复测：DSH 版本显示（`npm view`）、复用不误杀

## 🛠 已实现

- `Core/Runner`：Node 检测、端口探测复用、启动/监控 DSH（`npx -y`、日志 `%LOCALAPPDATA%\DSHW\dsh.log`）、退出整树+按端口清理（实机验证：退出后 3080 释放）
- `TrayIconManager`：纯 Win32 托盘 + TrackPopupMenu 右键菜单
- 双状态栏（详细消息省略+悬停全文 + 双版本：壳程序集 / DSH `npm view`）、AboutWindow（480×340 居中主屏）、ResourceHelper、三层图标
- 主窗口默认尺寸 = 主显示器工作区 85%（自适应 1080p/4K），最小 1100×700

## 🔧 关键坑（Unpackaged WinUI 3）

1. **app.manifest**：只保留 `dpiAware/dpiAwareness/longPathAware`；不要加 `<?xml?>` 声明（SxS "line 1 XML syntax error"）
2. **MRT 资源**：默认 `ResourceLoader()` 读不到，用 `ResourceManager.MainResourceMap.GetValue("Resources/键.转/")`（见 ResourceHelper）
3. **`resources.pri`**：构建复制为 resources.pri；`dotnet publish` 文件夹版需 `CopyPriToPublish`（缺了 WinUI 启动崩溃 0xC000027B）
4. **Win32**：`Shell_NotifyIcon` 在 shell32.dll；WndProc 委托必须存字段防 GC
5. **标题栏**：不要自画关闭按钮；按钮颜色显式设 `AppWindow.TitleBar.*`；`PreferredHeightOption=Tall`
6. **npx**：无 stdin 进程必须 `npx -y`（首次安装询问会失败）
7. **进程清理**：npx 启动 dsh 后可能自退导致孤儿 → 按端口 netstat 找监听 PID 清理，不能只信 cmd
8. **单文件**：解压到 `%TEMP%\.net\`；`Startup.cs` 设 `MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY`
9. **发布目录排除**：`release\` 在项目内会被默认 None glob 打包进单文件 → 嵌套+膨胀。修复：`<DefaultItemExcludes>$(DefaultItemExcludes);release\**</DefaultItemExcludes>`（**必须是属性**）

## 🧪 测试指引（进程清理安全）

`netstat -ano | findstr ":3080.*LISTENING"` 先看占用者。
- Harness 在 3080 → DSHW 复用，退出不杀 → **可安全测**
- 3080 空闲 → DSHW 起自己的 DSH → 退出后 netstat 应无监听 → 安全测
- ⚠️ 测"DSH 占 3080"路径需先停 Harness → **对话会断** → 测完重启 Harness，新对话读本文档继续

## 📦 发布与脚本（scripts/）

```
publish-all.bat / publish.ps1 [-Target full|single|lite]   # 发布 → DSHW.Desktop\release\<variant>\
publish-full.bat / publish-single.bat / publish-lite.bat    # 单独发布某版本
kill-dsh.bat / kill-dsh.ps1 [-Force]                        # 清理 3080 残留（确认后杀，防误杀 Harness）
start-dsh.bat / start-dsh.ps1                               # 手动启动 DSH web（控制台，Ctrl+C 停）
```

| 方案 | pubxml | 产物 | 体积 | 目标机要求 |
|------|--------|------|------|-----------|
| 完全版 | `full.pubxml` | 单 exe | 235.7MB（含 ReadyToRun） | 无 |
| 单文件版 | `single.pubxml` | 单 exe | 97.6MB | 仅 .NET 9 |
| Lite 文件夹 | `lite.pubxml` | 47 文件 | 38.6MB | .NET 9 + WinAppSDK Runtime |

- 单文件产物 exe 可单独拷贝运行（pdb/pri 可选）；Lite 需整个文件夹（resources.pri 必需）
- 硬前提：目标机有 Node.js + npm；正式分发再做 MSIX（需签名证书）

## 📋 待办

1. 实机复测进程清理/复用/DSH 版本显示
2. 窗口位置/大小持久化
3. DSH 更新检测提示（`npm view` 已可对比）

## 📎 链接

- DSH 官方仓库：https://github.com/deepseek-ai/deepseek-harness · [rc.8 发布说明](https://github.com/deepseek-ai/deepseek-harness/releases/tag/dsh-v0.1.0-rc.8)
- DSH npm：https://www.npmjs.com/package/@deepseek-ai/dsh
- Windows App SDK：https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
