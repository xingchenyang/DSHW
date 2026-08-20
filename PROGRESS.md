# DSHW — 项目进度

> 最后更新：2026-08-20 · 版本 0.1.0

## 📁 项目信息

| 项 | 值 |
|----|----|
| 名称 | DSHW (DSH Workbench for Windows) |
| 类型 | WinUI 3 桌面应用（Unpackaged，`WindowsPackageType=None`） |
| 框架 | .NET 9.0 · net9.0-windows10.0.26100.0（最低 10.0.17763.0） |
| Windows App SDK | 2.4.0（Runtime 2.4.0 已安装） |
| 核心功能 | 启动 DSH（`npx @deepseek-ai/dsh web`）并用 WebView2 内嵌其 Web UI（http://127.0.0.1:3080） |
| 仓库 | `C:\PROJETS-PERSO\DSHW\` · https://github.com/xingchenyang/DSHW |

## ✅ 当前状态（2026-08-20 实机验证）

- [x] 编译通过（0 错误 0 警告）
- [x] 应用可启动：窗口 + 托盘图标 + 多语言状态栏正常
- [x] 多语言 .resw（en-US / fr-FR / zh-CN）在 Unpackaged 下正常加载
- [x] WebView2 加载 DSH Web UI 待联网环境最终确认

## 🛠 已实现

- WinUI 3 窗口（自定义深色标题栏 + 状态栏 + WebView2 主体，应用主题固定 Dark）
- `Core/Runner`：检测 Node.js、启动/监控 DSH 进程（`npx -y` 免交互安装，输出重定向 `dsh.log` 便于排查）
- `Managers/TrayIconManager`：纯 Win32 API 托盘图标（双击恢复窗口）
- `Helpers/ResourceHelper`：Unpackaged 下读取 .resw 资源（见下方要点）
- 品牌统一为 DSH
- 应用图标三层配置（见下方要点）
- 版本信息 0.1.0（exe 属性可见：FileVersion 0.1.0.0 / Product DSHW / Copyright）

## 🔧 技术要点（Unpackaged WinUI 3 的坑）

1. **app.manifest 只保留已注册参数**：`dpiAware` + `dpiAwareness` + `longPathAware`；`autoElevate`、`disableWindowFiltering`、`highResolutionScrollingAware` 等会导致 SxS 激活失败（"configuration incorrecte"）。
2. **app.manifest 不要加 `<?xml ...?>` 声明**：MSBuild 的清单嵌入/合并流程会产出损坏的内嵌资源，SxS 加载器报 "line 1 XML syntax error"（曾致启动失败）。
3. **MRT Core 资源读取**：默认 `ResourceLoader()` 在 Unpackaged 下解析不到资源；需 `ResourceManager().MainResourceMap.GetValue("Resources/键名.转斜杠")`，封装在 `ResourceHelper`。
4. **`resources.pri`**：构建后复制为 `resources.pri`（csproj 的 `CopyPriAsResourcesPri` 目标）。
5. **Win32 P/Invoke**：`Shell_NotifyIcon` 在 `shell32.dll`（不是 user32）；WndProc 委托必须存字段防 GC。
6. **`Window.Title` 不支持 x:Uid**（Window 非 DependencyObject），标题在 XAML 硬编码。
7. **应用图标三层**：exe 图标 = csproj `<ApplicationIcon>`（编译嵌入）；窗口图标 = `AppWindow.SetIcon()`（任务栏/Alt-Tab）；托盘图标 = `ExtractIconEx` 从 exe 提取（替代系统默认图标）。
8. **自定义标题栏**：`ExtendsContentIntoTitleBar=true` 时系统最小化/最大化/关闭按钮悬浮在内容右上，不要再画自定义关闭按钮；内容需预留右侧空间（本项目 180px）。**按钮颜色要显式设置** `AppWindow.TitleBar.ButtonForegroundColor` 等（否则跟随系统浅色主题，聚焦时深色字看不见）；`PreferredHeightOption=Tall` 让按钮匹配 48px 栏高（Win11）。
9. **npx 免交互**：无 stdin 的进程里 `npx` 首次安装会交互询问导致失败，必须 `npx -y`；DSH 输出重定向 `%LOCALAPPDATA%\DSHW\dsh.log`（单文件 exe 的 BaseDirectory 是临时解压目录，不可用）。
10. **状态语义**：WebView 成功加载 DSH UI 后锁定"就绪"，不再被 Runner 的进程退出事件降级（进程退出只影响"未成功加载"时的状态）；这样端口被占（DSH 启动失败但页面可访问）时状态栏不会误报"已停止"。

## 📋 待办

1. 托盘右键菜单（显示/隐藏/退出）— `WndProc` 中 `WM_RBUTTONUP` 分支待实现
2. 窗口关闭 → 隐藏到托盘（不退出）— 当前 `OnWindowClosed` 直接退出
3. 窗口位置/大小持久化
4. 环境检测与友好错误提示（Node.js 缺失等资源已就绪；可加 DSH 服务健康检查）
5. 发布验证（见下方"发布路线"）

## 📦 发布路线（2026-08-20 更新：拆分包优化后）

**拆分包**：csproj 引用 `Microsoft.WindowsAppSDK.Foundation/WinUI/Runtime/InteractiveExperiences/DWrite`（版本对齐 Runtime 2.4.0 组件期望），不再用 `Microsoft.WindowsAppSDK` 元包 → 去掉 AI/ML/Search/Widgets 库，**所有版本体积约减半**。

| 方案 | 产物 | 体积 | 目标机要求 | 状态 |
|------|------|------|-----------|------|
| **完全版** `win-x64.pubxml` | 单个 exe | ~235MB | 无（全自包含） | ✅ 实测运行 |
| **WinAppSDK 自包含单文件** `win-x64-lite-winappsdk.pubxml` | 单个 exe | ~98MB | 仅 .NET 9 Runtime | ✅ 用户实测（165MB 版）；98MB 版待复测 |
| **Lite 文件夹** `win-x64-lite.pubxml` | 47 个文件 | ~38.6MB | .NET 9 + WinAppSDK Runtime | ✅ 已实测运行 |
| ~~FDD 单文件~~ `win-x64-lite-single.pubxml` | — | — | — | ❌ 已删除（官方不推荐组合，实测无法启动） |

```powershell
dotnet publish -c Release -p:Platform=x64                                  # 完全版
dotnet publish -c Release -p:Platform=x64 -p:PublishProfile=win-x64-lite-winappsdk  # 98MB 单文件
dotnet publish -c Release -p:Platform=x64 -p:PublishProfile=win-x64-lite   # Lite 文件夹
```

**注意**：
- `dotnet publish` 文件夹版需 csproj 的 `CopyPriToPublish` 目标复制 `.pri`（WinUI 启动必需，否则 0xC000027B 崩溃）
- 单文件 exe 启动时解压到 `%TEMP%\.net\`（沙箱/受限环境会失败）；`Startup.cs` 为单文件设置 `MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY`
- **硬前提**：目标机必须有 **Node.js + npm**（DSHW 启动 DSH 走 `npx -y @deepseek-ai/dsh web`）
- **正式分发**（面向普通用户）时再做 **MSIX**：需签名证书（测试证书触发 SmartScreen 警告）、开始菜单/卸载/自动更新

## 🧹 清理记录（2026-08-20）

- 删除：`scripts/`（5 个空文件）、`resources/`（下载的 DeepSeek logo SVG）、`Package.appxmanifest`（Unpackaged 下未使用，MSIX 时由 VS 重建）
- 保留：`Assets/*.png`（MSIX 打包占位资源）
- 待清理：`obj/`、`bin/` 中的旧目标框架残留（net8.0、net9.0-19041），可 `dotnet clean` 或手动删除

## 📎 链接

- Windows App SDK：https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
- DSH 官方：https://github.com/deepseek-ai/deepseek-harness · https://deepseek.com/harness/en/
