# Agent Instructions

修改本仓库前，完整阅读 `README.md`、`DEVELOPMENT.md` 和 `CHANGELOG.md`。

## 范围

- DSHW 是 WinUI 3、.NET 9 和 WebView2 构建的 Windows 桌面壳，最终应用版本为 `0.1.2`；项目已归档，不再维护。
- 当前唯一保证兼容的 DeepSeek Harness 版本为 `v0.1.2-rc.1`，不保证向下兼容；不要自行承诺其他版本兼容性，必须先验证对应 CLI、认证和 profile 行为。
- DSH 属于外部快速演进依赖；升级兼容性必须以实际 CLI 行为和启动输出验证，不要只依赖旧版本假设。
- 保持 `README.md` 面向使用者且简洁。用户可见变化写入 `CHANGELOG.md`，架构、技术背景和接手状态写入 `DEVELOPMENT.md`。

## 必须保持

### 服务与进程

- 3080 已被占用时只能复用服务，退出 DSHW 不得结束该外部进程。
- DSHW 自行启动服务时，退出必须清理自己的 cmd 进程树及启动期间首次观察到的监听 PID；杀进程前必须再次核对端口监听者，避免误杀 Harness GUI。

### 认证与日志

- 从 `dsh web` 输出提取认证 URL 时，只接受 `127.0.0.1:3080`；日志中的 token 必须脱敏。
- npm silly 日志不得逐行执行 `TextBox.Text +=`；继续使用后台缓冲和定时批量刷新，避免 UI 线程 O(n²) 重排。
- 更新日志仅在视图已处于底部时自动跟随；用户上翻后必须保持阅读位置，直到用户自行滚回底部。

### 更新与备份

- `AutoUpdate` 默认不得隐式启用；关闭时优先调用已全局安装的 `dsh`，开启时才使用 `npx -y`。
- 更新 DSH 前必须备份 `~/.dsh`，且备份不得复制 `node_modules`、`blob_storage`、`indexeddb`、`cache` 或 `.cache`。

### UI 与发布

- 不得用 `Hide()` 后重新 `ShowAsync()` 的方式重建同一个 `ContentDialog`；版本对话框保持单实例并原地更新。
- Unpackaged 发布物必须包含 `resources.pri`，且发布目录必须继续排除在 SDK 默认文件扫描之外。
- 含中文的 `scripts/*.ps1` 必须保存为 UTF-8 BOM；`.bat` 的输出和注释保持 ASCII，兼容 Windows PowerShell 5.1 与非 UTF-8 cmd 代码页。

### Agent 产物

- Agent 自动生成的所有测试文件、测试夹具、截图、报告及诊断/验证产物必须放入仓库 `.tools/`；不得散落在仓库根目录、源码目录或发布目录。`.tools/` 仅作 scratch space，不提交版本控制。

## 检查

提交前至少运行：

```powershell
dotnet build DSHW.Desktop\DSHW.Desktop.csproj --no-restore
git diff --check
```

涉及启动、升级、进程清理或发布时，还应按 `DEVELOPMENT.md` 的验证章节检查 3080 归属、token 登录、备份内容、日志交互和 lite/full 发布物。

## 文档

- 所有新增和修改都属于即将提交的版本；提交前先确定版本号和日期，再将用户可见变化直接写入对应版本章节。
- 架构、限制、关键决策、发布流程或待办变化时更新 `DEVELOPMENT.md`。
- 只有安装、配置或用户使用方式变化时才更新 `README.md`。
