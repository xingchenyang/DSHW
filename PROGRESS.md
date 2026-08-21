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

## 🧪 测试指引（进程清理安全）

`netstat -ano | findstr ":3080.*LISTENING"` 看占用。Harness 占→复用可安全测；空闲→DSHW 起自己的（退出后 netstat 应无监听）。测"DSH 占 3080"需先停 Harness（对话会断）。

## 📦 发布与脚本

- 发布：`scripts/publish-lite.bat`（或 `publish.ps1 -Target lite`）→ `DSHW.Desktop\release\lite\`。
- 体积：full 235.7MB / single 97.6MB / lite 38.6MB。
- Lite 需整个文件夹（resources.pri 必需）；`kill-dsh.bat` 清 3080 残留；`start-dsh.bat` 手动启动。

## 📋 待办

1. **下次做**：自动升级（0.1.1-rc 升级/降级）实机测试——含 `~/.dsh.bak` 备份、silly 流式安装、升级后降级回 0.1.0-rc.7。
2. 当前全局 dsh 为 **0.1.0-rc.7**（用户手动装回）；0.1.1-rc.2 改了 `.credentials.yaml` 格式，已有备份护栏。
3. 窗口位置/大小持久化。
4. 正式分发再做 MSIX（需签名证书）。

## 📎 链接

- DSH 官方：https://github.com/deepseek-ai/deepseek-harness
- DSH npm：https://www.npmjs.com/package/@deepseek-ai/dsh
