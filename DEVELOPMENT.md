# Development Guide

技术背景和接手信息。使用说明见 `README.md`，用户可见变化见 `CHANGELOG.md`，维护规则见 `AGENTS.md`。

## 当前状态

- DSHW：`0.1.1`，WinUI 3 + .NET 9 + WebView2，Unpackaged x64 应用。
- 唯一保证兼容的 DSH：`v0.1.2-rc.1`，不保证向下兼容。
- 测试构建：`DSHW.Desktop/release/lite`；发布前先退出 DSHW，避免 exe 被锁定。
- 当前重点：Web token 认证、更新日志智能滚动、更新后一键重启。

## 代码地图

- `Core/Runner.cs`：环境检测、3080 复用、DSH 启停、认证 URL 和进程清理。
- `Core/DshDetector.cs`：区分已安装版本与 registry 最新版本。
- `Core/Installer.cs`、`DshBackup.cs`、`Updater.cs`：安装、备份和更新流程。
- `MainWindow.xaml.cs`：WebView2、版本对话框、更新日志和重启。
- `InstallGuideWindow.*`：未安装引导；`TrayIconManager.cs`：托盘菜单。
- `Strings/*/Resources.resw`：三语言资源；`scripts/`：构建与维护脚本。
- `.tools/`：Agent 生成的测试和诊断产物，不提交、不发布。

## 运行流程

1. 探测 `127.0.0.1:3080`；已有服务则复用，退出时不结束它。
2. 端口空闲时检查 Node 和全局 DSH；缺失则显示安装引导。
3. 默认执行 `dsh web --no-open`；仅 `DeepSeek:Update:AutoUpdate=true` 时使用 `npx -y`。
4. stdout/stderr 写入 `%LOCALAPPDATA%\DSHW\dsh.log`，token 脱敏。
5. Runner 只接受本机 3080 的认证 URL，WebView2 等待服务和 URL 后导航。

外部 DSH 已占用 3080 且 WebView2 没有有效 cookie 时，DSHW 无法取回其旧 token；应关闭外部服务，让 DSHW 自行启动。

## 关键约束

- 只清理 DSHW 自己启动的进程；记录首次监听 PID，结束前再次核对 3080 归属。
- 更新顺序：端口警告 → 备份 `~/.dsh` → npm 全局安装 → 版本重检。
- 备份保留最近五份，并排除依赖、浏览器存储和缓存目录。
- npm 日志每 150ms 批量刷新；仅当视图位于底部时跟随。
- 重启先 Dispose Runner、释放 3080，再通过 `Environment.ProcessPath` 启动新实例。
- ContentDialog 保持单实例并原地更新，避免重入。
- 构建与发布必须复制 `resources.pri`，并排除 `release/**`。
- 中文 `.ps1` 使用 UTF-8 BOM；`.bat` 输出和注释使用 ASCII。

## 发布

- `scripts/rebuild-lite.bat`：退出、发布 lite、重新启动。
- `scripts/publish-lite.bat`：精简文件夹版，依赖 .NET 9、WinAppSDK Runtime 和 WebView2。
- `scripts/publish-full.bat`：自包含单文件版；`publish-all.bat`：发布两种版本。
- `scripts/kill-dsh.bat`、`start-dsh.bat`：安全清理和手动启动。

`single` 变体已废弃，不要恢复。

## 验证

```powershell
dotnet build DSHW.Desktop\DSHW.Desktop.csproj --no-restore
git diff --check
```

实机覆盖自有/外部 3080 归属、token 登录与脱敏、更新备份与日志滚动、一键重启，以及 lite/full 的 `resources.pri` 和三语言资源。

## 下一步

1. 等 DSH 发布下一个 RC 后，用一次真实自动更新集中回归本次修复；不为重复测试主动降级或反复安装。验证 token 认证 URL、日志滚动条、底部智能跟随、上翻位置保持、更新完成提示及一键重启。
2. 持久化窗口位置和大小。
3. 正式分发时评估签名 MSIX。

## 参考

- DSH：https://github.com/deepseek-ai/deepseek-harness
- npm：https://www.npmjs.com/package/@deepseek-ai/dsh
