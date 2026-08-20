<#
.SYNOPSIS
  手动启动 DSH web 服务（带控制台窗口，实时显示输出，Ctrl+C 停止）。

.DESCRIPTION
  与 DSHW 内部逻辑一致：npx -y 自动确认安装，@deepseek-ai/dsh 官方包。
  用法： .\scripts\start-dsh.ps1    （或双击 start-dsh.bat）
#>
$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "启动 DSH web: http://127.0.0.1:3080  （Ctrl+C 停止）" -ForegroundColor Cyan
Write-Host ""
npx -y @deepseek-ai/dsh web
Write-Host ""
Write-Host "DSH 已停止（退出码 $LASTEXITCODE）"
