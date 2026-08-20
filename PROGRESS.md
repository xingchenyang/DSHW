# DSHW 项目进度总结

> 最后更新：2026-08-20

---

## 📁 项目基本信息

| 项目 | 内容 |
|------|------|
| **项目名称** | DSHW (DeepSeek Harness Workbench) |
| **类型** | WinUI 3 桌面应用（Windows 客户端） |
| **目标框架** | .NET 9.0 |
| **核心功能** | 为 DeepSeek Harness (DSH) 提供原生 Windows 桌面外壳 |
| **代码仓库** | `C:\PROJETS-PERSO\DSHW\` |
| **GitHub** | https://github.com/xingchenyang/DSHW |

---

## ✅ 已完成

### 环境配置
- [x] .NET 9 SDK 已安装
- [x] VS2022 17.14 已配置
- [x] Git 个人配置完成（`.gitconfig-perso`）
- [x] SSH 密钥配置完成（`POLE2024P008-PERSO`）
- [x] GitHub 仓库创建完成

### 项目结构
- [x] 解决方案 `DSHW.sln` 已创建
- [x] 项目 `DSHW.Desktop.csproj` 已创建（WinUI 3 打包项目）
- [x] 目录结构已建立（Core/、Managers/、Helpers/、Utils/、Assets/）
- [x] 多语言资源文件已添加（en-US、fr-FR、zh-CN）

### 代码文件（已创建并编译通过）
| 文件 | 状态 |
|------|------|
| App.xaml / App.xaml.cs | ✅ 编译通过 |
| MainWindow.xaml / MainWindow.xaml.cs | ✅ 编译通过 |
| Core/Runner.cs | ✅ 编译通过 |
| Managers/TrayIconManager.cs | ✅ 编译通过（纯 Win32 API） |
| Helpers/Win32Helper.cs | ✅ 编译通过 |
| Utils/LanguageManager.cs | ✅ 编译通过 |
| appsettings.json | ✅ 已创建 |
| app.manifest | ✅ 已创建 |
| README.md | ✅ 已创建 |
| .gitignore | ✅ 已创建 |

---

## 🔧 当前技术选型

| 组件 | 选择 | 说明 |
|------|------|------|
| **UI 框架** | WinUI 3 | 打包版本（Package） |
| **WebView2** | Microsoft.Web.WebView2 | 嵌入 DSH Web UI |
| **托盘图标** | 纯 Win32 API | 不使用 Windows Forms |
| **多语言** | JSON + LanguageManager | 三语言支持 |
| **构建工具** | Visual Studio 2022 | 编译打包一体化 |

---

## ⚠️ 当前问题 / 待解决

### 编译状态
✅ **编译通过**（无错误）

### 运行状态
⚠️ **无法启动/调试**

### 问题描述
- 选择 `DSHW.Desktop (Package)` 启动时，提示"项目不知道如何执行该配置文件"
- 选择 `unpackaged` 启动时，报错：
  > `Cette application n'a pas démarré, car la configuration de l'application est incorrecte.`

### 可能原因
1. **Windows App SDK Runtime 未安装**
2. **Windows SDK 10.0.26100.0 未在 VS Installer 中勾选**
3. **测试证书未生成或无效**
4. **项目配置中目标 SDK 版本与本地安装版本不匹配**

### 待验证
- [ ] 在 VS Installer 中确认 `Windows 10 SDK (10.0.26100.0)` 是否已安装
- [ ] 安装 Windows App SDK Runtime (1.6.250430001)
- [ ] 尝试"重定目标解决方案"到已有 SDK 版本（如 10.0.22621.0）
- [ ] 重新生成测试证书
- [ ] 验证打包版本能否正常启动

---

## 📝 下次继续时的工作

### 优先级 1：让应用跑起来（调试启动）
1. 检查/安装 Windows 10 SDK (10.0.26100.0)
2. 检查/安装 Windows App SDK Runtime
3. 选择 `DSHW.Desktop (Package)` 调试模式
4. 验证应用能正常启动，WebView2 加载 DSH Web UI

### 优先级 2：功能完善
1. 实现窗口关闭 → 隐藏到托盘（不退出）
2. 托盘图标右键菜单（显示/退出）
3. 窗口位置/大小持久化
4. 环境检测与友好错误提示

### 优先级 3：发布准备
1. 生成 MSIX 安装包
2. 测试 FDD / SCD 发布版本

---

## 🚀 如何快速继续

1. **打开项目**

C:\PROJETS-PERSO\DSHW\DSHW.sln

2. **检查 SDK 安装**

- VS Installer → 修改 → 单个组件 → 搜索 `10.0.26100`
- 确认已勾选

3. **选择启动配置**

- 工具栏下拉菜单 → 选择 `DSHW.Desktop (Package)`

4. **按 F5 启动调试**

---

## 📎 相关链接

- Windows App SDK 下载：https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
- .NET 9 下载：https://dotnet.microsoft.com/download/dotnet/9.0
- GitHub 仓库：https://github.com/xingchenyang/DSHW
- DSH 官方：https://github.com/deepseek-ai/deepseek-harness

---

## 💡 备注

- 项目已选择纯 WinUI 3 路线，不引入 Windows Forms / WPF 依赖
- 托盘图标使用纯 Win32 API 实现
- 下次继续时，首要目标是解决调试启动问题，让应用跑起来

### 2026-08-20：切换到原生 .resw 多语言

- [x] 删除 JSON 语言包方案（LanguageManager.cs + *.json）
- [x] 添加 WinUI 3 原生 .resw 资源文件（en-US、fr-FR、zh-CN）
- [x] 使用 x:Uid 在 XAML 中绑定资源
- [x] 使用 ResourceLoader 在 C# 代码中获取资源

---

## 🔄 2026-08-20：更新品牌命名

### 变更内容
根据 DeepSeek Harness 官方最新 RC.8 版本（`dsh-v0.1.0-rc.8`）的品牌规范，项目中的品牌名称已统一调整：

| 原名称 | 新名称 | 说明 |
|--------|--------|------|
| `DeepSeek Harness` | `DSH` | 使用官方推荐缩写 |
| `DeepSeek Harness Workbench` | `DSHW` | 项目名保持不变 |

### 调整范围
- [x] 项目名保持 `DSHW`
- [x] 应用内文案已更新
- [x] 资源文件已更新

### 受影响的文件
| 文件 | 变更 |
|------|------|
| `Strings/*/Resources.resw` | 更新 Tray.Tooltip 等文本 |
| `README.md` | 更新项目描述 |
| `app.manifest` | 更新程序集描述 |
| `DSHW.Desktop.csproj` | 更新 AssemblyTitle/Description |

### 当前品牌标识
- **项目全称**：DSHW (DSH Workbench for Windows)
- **品牌缩写**：DSH（遵循官方规范）
- **GitHub 仓库**：https://github.com/xingchenyang/DSHW

---

## 📋 下一步

1. [ ] 验证 `Tray.Tooltip` 在各语言中显示正确
2. [ ] 确认 README.md 中不再出现完整 "DeepSeek Harness" 字样
3. [ ] 后续所有文案统一使用 `DSH` 缩写