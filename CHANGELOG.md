# Changelog

本文件记录面向使用者的功能变化。开发背景与技术决策见 `DEVELOPMENT.md`。

格式参考 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，版本遵循 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)。

## [0.1.2] - 2026-09-11

### Fixed

- 修复应用内升级日志没有可见滚动条、无法自动跟随到底部的问题；用户上翻后仍保持阅读位置。
- 降低大量 npm 日志刷新造成的 UI 卡顿，避免 UI 队列积压和整段日志反复复制。
- 修复升级成功后的重启按钮可能在版本对话框中被横向裁掉的问题。

## [0.1.1] - 2026-09-07

### Added

- 以 DeepSeek Harness `v0.1.2-rc.1` 为唯一保证的兼容版本，不保证向下兼容。
- 适配新版 DSH Web token 认证：自动读取 `dsh web --no-open` 输出中的完整 loopback 认证 URL，并交给 WebView2 加载。
- 应用内更新成功后增加“立即重启 DSHW”按钮，一键停止当前服务、释放 3080 并启动新实例。

### Changed

- 更新日志框增加纵向滚动条和智能跟随：位于底部时自动显示最新日志，用户上翻后保持阅读位置，滚回底部后恢复跟随。
- `dsh.log` 中的认证 token 自动脱敏，且认证 URL 仅接受 `127.0.0.1:3080`。

## [0.1.0] - 2026-09-01

### Added

- 初始化解决方案、WinUI 3 桌面项目、MIT License、README 和基础资源。
- 建立 Runner、MainWindow、托盘管理器、应用配置和中文、英文、法文本地化骨架。
- 完成 WebView2 内嵌 `127.0.0.1:3080` 的首个可运行 DSHW 版本。
- 增加 DSH 未安装引导、应用内 npm 安装、镜像选择和 Minimal/Verbose/Silly 日志级别。
- 增加更新前 `~/.dsh` 自动备份、最近五份保留策略和配置格式变化护栏。
- 增加版本/更新对话框、命令复制、重新检测和应用内更新流程。
- 扩展系统托盘菜单，支持显示、隐藏、关于和真正退出。
- 增加顶部运行状态、底部详细状态和 DSH/DSHW 双版本显示。
- 增加 About 窗口、WebView2 用户数据目录隔离及窗口/任务栏图标。
- 建立 full、lite 和当时的 single 发布脚本，以及手动启动和带安全确认的 3080 清理脚本。

### Changed

- 默认优先调用全局安装的 `dsh`，实现离线快速启动；只有配置显式开启自动更新时才使用 `npx -y`。
- 分离本机已安装版本和 npm registry 最新版本的检测语义，修复把远端版本误认为运行版本的问题。
- 更新 DSH 前检测 3080 监听并提示重启，不在更新过程中强制中断当前 WebView。
- `~/.dsh` 备份排除 `node_modules`、`blob_storage`、`indexeddb`、`cache` 和 `.cache`，避免备份体积膨胀。
- 3080 已有服务时改为复用并保持其生命周期；端口空闲时才启动 DSHW 自有服务。
- 更新日志改为后台 `StringBuilder` 缓冲、150ms 批量刷新，避免 silly 日志导致 UI 无响应。
- 版本对话框采用单实例原地刷新，并增加按钮防连点和可靠的行内反馈，避免 ContentDialog 重入崩溃。
- About 窗口打开前离线刷新已安装的 DSH 版本。
- 退出时清理自己的 cmd 进程树和首次观察到的监听 PID，并在结束前复查端口归属，避免误杀 Harness GUI。
- 转为 x64 Unpackaged 应用并建立初始单文件发布配置。
- 改用拆分的 Windows App SDK 包，显著降低发布体积。
- 固定 WebView2 用户数据目录，避免在发布目录生成缓存文件。

### Fixed

- 修复自定义标题栏、状态栏、DSH 启动和应用图标问题。
- 修复 Windows PowerShell 5.1 读取无 BOM 中文脚本产生乱码和语法错误的问题。
- 删除废弃的 single 发布变体，只保留 full 和 lite。
- 在构建和发布时复制 `resources.pri`，修复缺少 MRT 资源导致的启动崩溃。
