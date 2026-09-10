# Development Guide

本文档保存项目的技术背景、关键决策和接手信息。安装与配置见 `README.md`，用户可见变化见 `CHANGELOG.md`，维护约束见 `AGENTS.md`。

## 当前状态

- 当前版本：`0.1.1`。
- 技术栈：WinUI 3、.NET 9、WebView2；Unpackaged x64 应用。
- 目标环境：Windows 10 1809+ / Windows 11。
- 唯一保证兼容的 DSH：`v0.1.2-rc.1`，不保证向下兼容。
- 测试构建：`DSHW.Desktop/release/lite`；发布前先退出 DSHW，避免 exe 被锁定。
- 当前重点：Web token 认证、更新日志智能滚动、更新后一键重启。

## 代码地图

- `DSHW.Desktop/Core/Runner.cs`：环境检测、3080 复用、DSH 启停、认证 URL 和进程清理。
- `DSHW.Desktop/Core/DshDetector.cs`：区分已安装版本与 registry 最新版本。
- `DSHW.Desktop/Core/Installer.cs`、`DshBackup.cs`、`Updater.cs`：安装、备份和更新流程。
- `DSHW.Desktop/MainWindow.xaml.cs`：WebView2、版本对话框、更新日志和重启。
- `DSHW.Desktop/InstallGuideWindow.*`：未安装引导；`Managers/TrayIconManager.cs`：托盘菜单。
- `DSHW.Desktop/Strings/*/Resources.resw`：中文、英文和法文资源。
- `scripts/`：构建、发布、启动和维护脚本。
- `.tools/`：Agent 生成的测试和诊断产物，不提交、不发布。

## 运行流程

1. 探测 `127.0.0.1:3080`；已有服务则复用，退出时不结束它。
2. 端口空闲时检查 Node 和全局 DSH；缺失则显示安装引导。
3. 默认执行 `dsh web --no-open`；仅 `DeepSeek:Update:AutoUpdate=true` 时使用 `npx -y`。
4. stdout/stderr 写入 `%LOCALAPPDATA%\\DSHW\\dsh.log`，认证 token 脱敏。
5. Runner 只接受本机 3080 的认证 URL，WebView2 等待服务和 URL 后导航。

外部 DSH 已占用 3080 且 WebView2 没有有效 cookie 时，DSHW 无法取回其旧 token；应关闭外部服务，让 DSHW 自行启动。

## 关键决策与约束

### 服务与进程

- 3080 已被占用时只能复用服务，退出 DSHW 不得结束该外部进程。
- DSHW 自行启动服务时，退出必须清理自己的 cmd 进程树及启动期间首次观察到的监听 PID。
- 杀进程前必须再次核对端口监听者，避免误杀 Harness GUI。

### 认证与日志

- 从 `dsh web` 输出提取认证 URL 时，只接受 `127.0.0.1:3080`。
- 日志中的 token 必须脱敏。
- npm silly 日志使用后台缓冲和定时批量刷新，避免逐行更新 `TextBox.Text` 造成 UI 线程 O(n²) 重排。
- 更新日志仅在视图已处于底部时自动跟随；用户上翻后保持阅读位置，直到用户自行滚回底部。

### 更新与备份

- `AutoUpdate` 默认不得隐式启用；关闭时优先调用已全局安装的 `dsh`，开启时才使用 `npx -y`。
- 更新 DSH 前必须备份 `~/.dsh`，且备份不得复制 `node_modules`、`blob_storage`、`indexeddb`、`cache` 或 `.cache`。
- 更新顺序为：端口警告 → 备份 `~/.dsh` → npm 全局安装 → 版本重检。
- 备份保留最近五份，并排除依赖、浏览器存储和缓存目录。

### UI 与发布

- 不得用 `Hide()` 后重新 `ShowAsync()` 的方式重建同一个 `ContentDialog`；版本对话框保持单实例并原地更新。
- 重启先 Dispose Runner、释放 3080，再通过 `Environment.ProcessPath` 启动新实例。
- Unpackaged 发布物必须包含 `resources.pri`，且发布目录必须继续排除在 SDK 默认文件扫描之外。
- 中文 `scripts/*.ps1` 使用 UTF-8 BOM；`.bat` 的输出和注释使用 ASCII，兼容 Windows PowerShell 5.1 与非 UTF-8 cmd 代码页。
- `single` 变体已废弃，不要恢复。

## 已知限制

- 当前只保证兼容 DSH `v0.1.2-rc.1`；升级兼容性必须以实际 CLI 行为、认证和 profile 启动输出验证。
- 外部 DSH 占用 3080 且没有有效 cookie 时，DSHW 无法取得外部服务的旧 token。
- `lite` 发布依赖目标机器安装 .NET 9、WinAppSDK Runtime 和 WebView2；`full` 为自包含单文件发布。
- 实机验证需要覆盖自有/外部 3080 归属、token 登录与脱敏、更新备份与日志滚动、一键重启，以及 lite/full 的 `resources.pri` 和三语言资源。

## 构建与验证

```powershell
dotnet build DSHW.Desktop\\DSHW.Desktop.csproj --no-restore
git diff --check
```

发布脚本：

- `scripts/rebuild-lite.bat`：退出、发布 lite、重新启动。
- `scripts/publish-lite.bat`：精简文件夹版。
- `scripts/publish-full.bat`：自包含单文件版。
- `scripts/publish-all.bat`：发布 lite 和 full 两种版本。
- `scripts/kill-dsh.bat`、`start-dsh.bat`：安全清理和手动启动。

## 发布检查

1. 确定版本号和发布日期，更新 `CHANGELOG.md`、`DEVELOPMENT.md` 与 README 标题。
2. 运行构建和 `git diff --check`，确认发布目录包含 `resources.pri`。
3. 按实机流程检查 3080 归属、token 登录与脱敏、更新备份、日志滚动和一键重启。
4. 分别验证 lite/full 发布物、三语言资源和退出时的进程清理。
5. 确认 `.tools/` 之外没有 Agent 生成的测试、截图、报告或诊断产物。

## 维护流程

1. 修改前阅读 `README.md`、本文件、`CHANGELOG.md` 和 `AGENTS.md`。
2. 升级 DSH、.NET、WinUI 3 或 WebView2 时，以实际启动、认证和发布行为为准。
3. 用户可见功能、修复和兼容性变化同步更新 `CHANGELOG.md`；架构、限制和发布流程变化同步更新本文件。
4. 只有安装、配置或用户使用方式变化时才更新 `README.md`。

## 参考

- DSH：[deepseek-harness](https://github.com/deepseek-ai/deepseek-harness)
- npm：[@deepseek-ai/dsh](https://www.npmjs.com/package/@deepseek-ai/dsh)
