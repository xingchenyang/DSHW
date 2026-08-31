# DSHW — 项目进度

> 最后更新：2026-08-22 · 版本 0.1.0 · 兼作"对话恢复手册"（中断后交给新对话即可接手）
> 测试用构建：`release/lite`（重发布即可见最新改动；发布前需先退出正在运行的 DSHW，否则 exe 被锁定）

## 📁 项目信息

| 项 | 值 |
|----|----|
| 名称 | DSHW (DSH Workbench for Windows) |
| 类型 | WinUI 3 桌面应用 · Unpackaged（`WindowsPackageType=None`） |
| 框架 | .NET 9.0 · net9.0-windows10.0.26100.0 · WinAppSDK 2.4.0 拆分包 |
| 核心功能 | 启动 DSH，WebView2 内嵌 Web UI（127.0.0.1:3080）。默认 `dsh web`；appsettings `DeepSeek:Update:AutoUpdate` 为 true 才走 `npx -y` |
| 仓库 | `C:\PROJETS-PERSO\DSHW\` · https://github.com/xingchenyang/DSHW |

## ⚠️ 环境事实（必读）

**3080 常被 Harness GUI 占用**。DSHW 探测 3080：已有服务→复用（退出不杀）；空闲→起自己的 DSH（退出杀 cmd 树 + 首次记录监听 PID，杀前复查防误杀）。

## 🛠 已实现

- **Runner**：Node 检测、端口复用、dsh-first 启动、已装/未装门控、日志 `%LOCALAPPDATA%\DSHW\dsh.log`、退出整树+按端口清理。
- **DshDetector**：installed(`npm.cmd ls -g`) 与 latest(`npm.cmd view`) 分开语义（修掉把 registry 最新版当运行版本的 bug）；正则抓版本保留 `-`。
- **Installer/AppConfig**：App 内 `npm.cmd install -g` 流式日志；读 appsettings；三档日志 Minimal/Verbose/Silly。
- **DshBackup/Updater**：更新前备份整个 `~/.dsh`→`~/.dsh.bak-<ts>`（留 5 份）；`Updater`=备份→silly 流式安装→重检。护栏：0.1.1-rc 改动 `.credentials.yaml` 格式，先备份。
- **InstallGuideWindow**：未装引导（命令+复制+镜像+日志级+流式日志+备份提示+装完重检）。
- **更新/版本对话框**：单实例+原地刷新（并发安全，连点不崩）；三按钮 800ms 防连点；复制/重检提示=按钮右侧小 TextBlock 停留 3s；重检结果明确话术不弄丢提示（TickUpToDateInPlace）。
- **TrayIconManager**、About 打开前离线刷新版本、三语言、双状态栏。

## 🔧 关键坑（Unpackaged WinUI 3）

1. PRT 资源用 `ResourceManager.MainResourceMap`（默认 ResourceLoader 读不到）。
2. `resources.pri` 需复制（构建 + publish），缺了 WinUI 启动崩 0xC000027B。
3. 发布目录删除：`DefaultItemExcludes` 排除 `release\`，否则被打进单文件膨胀。
4. PS `Restricted` 挡 `.ps1` shim：走 `cmd.exe /c <cmd>.cmd`。
5. **ContentDialog 重入崩溃**（0xc000027b）：别 `Hide()`+`ShowAsync()` 重建；单实例 + 原地 `Content` 重建；打开前别 await 联网刷新（卡 UI）。
6. **瞬显提示**：ToolTip `IsOpen` 定位不可靠、TeachingTip 会延迟闪退。用按钮行 StackPanel 右侧插 TextBlock，3s 后移除；重检后勿整树重建（会清掉提示）。
7. **日志流 O(n²) 饿死 UI（实机死机）**：逐行 `TextBox.Text +=` 在 silly 海量日志下每行全量重排 → UI 忙循环 `Responding=False`。**修法：StringBuilder 缓冲（后台线程 append）+150ms 定时合并刷新一次**（MainWindow 安装日志、InstallGuideWindow.LogLine 均用此方案）。
8. **升级时若 3080 有 dsh 在跑**：`npm install -g` 覆盖被正在运行的 node 加载的全局包 / `.dsh` 写入 → 冲突。Updater 已加 3080 监听探测 + log 警示（升级完需重启 DSHW 载入新版；不自动停服务以免打断 WebView2）。
9. **备份勿拷 node_modules**：`~/.dsh` 下 profile 的 `node_modules` 是可重建构建产物（dsh 依赖 sharp/node-pty 等），全量复制会把备份从 80MB 撑到 400+MB。`DshBackup.CopyDirectory` 现递归剪枝 `node_modules`/`blob_storage`/`indexeddb`/`cache`/`.cache`，只保用户数据+配置。
10. **scripts/*.ps1 必须带 UTF-8 BOM**：`*.bat` 用 `powershell`（Windows PowerShell 5.1）`-File` 调用，5.1 对**无 BOM 的 UTF-8 `.ps1` 按 ANSI/GBK 读** → 中文变乱码 `ÚÇÇÕç║...` → 引号被拆断 → ParserError。**修法：含中文的 `.ps1`（publish/kill-dsh/start-dsh）以 UTF-8 BOM 保存**。另外，**`.bat` 的 `echo` 中文可能乱码**（cmd 按系统代码页直接显示字节），故 **`.bat` 的回显/注释用英文 ASCII**（rebuild-lite 等已改）。校验：parse-check。

## 🧪 测试指引（进程清理安全）

`netstat -ano | findstr ":3080.*LISTENING"` 看占用。Harness 占→复用可安全测；空闲→DSHW 起自己的（退出后 netstat 应无监听）。测"DSH 占 3080"需先停 Harness（对话会断）。

## 📦 发布与脚本

- **快速重建**：`scripts/rebuild-lite.bat` 一条命令 = 退出 DSHW → 重发布 lite → 重启。
- 发布：`publish-lite.bat` / `publish-full.bat`（`publish.ps1 -Target lite|full`）→ `DSHW.Desktop\release\<variant>\`；`publish-all.bat` = full+lite。
- **`single` 已废弃**：`publish-single.bat`、`single.pubxml` 均已删；`publish.ps1` 只留 full/lite。
- **full = 完整自包含单文件**（SelfContained .NET + WinAppSDK + WebView2，目标机只需 Win11），约 236MB / 单 exe。实测文件夹模式仅 218MB / 460 文件（省 ~7% 但散文件、启动久），故保持单文件换取便携。
- **lite = 精简**（FDD），~39MB，需装 .NET9 + WinAppSDK Runtime + WebView2。
- Lite 需整个文件夹（resources.pri 必需）；`kill-dsh.bat` 清 3080 残留；`start-dsh.bat` 手动启动。

## 📋 待办

1. **下次做**：自动升级**复测**（已修日志流死机 bug + 升级时 3080 在跑警示 + `dsh web --no-open` 不再弹 Edge）。当前全局 dsh 已升到 **0.1.1-rc.2**；可测升级→降级回 0.1.0-rc.7，验证 `.dsh.bak` 备份/恢复。
2. 窗口位置/大小持久化。
3. 正式分发再做 MSIX（需签名证书）。

## 📎 链接

- DSH 官方：https://github.com/deepseek-ai/deepseek-harness
- DSH npm：https://www.npmjs.com/package/@deepseek-ai/dsh
