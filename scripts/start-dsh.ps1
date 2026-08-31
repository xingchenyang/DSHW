<#
.SYNOPSIS
  手动启动 DSH web 服务（带控制台窗口，实时显示输出，Ctrl+C 停止）。

.DESCRIPTION
  与 DSHW 内部逻辑一致：优先用全局已装的 dsh 命令（离线、快）。
  ⚠️ 经 cmd /c 调用，避免 PowerShell 执行策略（Restricted）拦截 npm.ps1 / npx.ps1 / dsh.ps1 shim。
  需要 npx -y 自动升级时： .\scripts\start-dsh.ps1 -UseNpx
  用法： .\scripts\start-dsh.ps1    （或双击 start-dsh.bat）
.PARAMETER UseNpx
  若提供，改用 npx -y 启动（联网自动升级，可能较慢/卡）。
#>
[CmdletBinding()]
param(
    [switch]$UseNpx
)
$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

Write-Host "启动 DSH web: http://127.0.0.1:3080  （Ctrl+C 停止）" -ForegroundColor Cyan
Write-Host ""

if ($UseNpx) {
    cmd /c "npx -y @deepseek-ai/dsh web"
}
else {
    cmd /c "dsh web"
}

Write-Host ""
Write-Host "DSH 已停止（退出码 $LASTEXITCODE）"
Pop-Location
