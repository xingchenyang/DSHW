<#
.SYNOPSIS
  彻底杀死占用 127.0.0.1:3080 的 DSH 残留进程（含其子进程树）。

.DESCRIPTION
  用途：清理 bug 时代残留 / 手动 cmd 启动的 DSH。
  DSHW 出于安全设计，只杀自己启动的 DSH（复用现有服务时不杀），
  所以这类"外部残留"需要本脚本手动清理。

  ⚠️ 安全警告：如果 3080 当前是 DeepSeek Harness GUI（开发对话运行所在），
     请勿确认！脚本会先显示目标进程并要求确认。

  用法：
    .\scripts\kill-dsh.ps1          # 显示目标并询问确认
    .\scripts\kill-dsh.ps1 -Force   # 跳过确认（慎用）
#>
param(
    [switch]$Force,
    [int]$Port = 3080
)

$ErrorActionPreference = "Stop"

function Get-ListenerPid {
    param([int]$Port)
    $lines = netstat -ano | Select-String ":$Port\s" | Select-String "LISTENING"
    foreach ($line in $lines) {
        $parts = ($line.ToString() -split '\s+') | Where-Object { $_ }
        if ($parts.Count -ge 5 -and $parts[-1] -match '^\d+$') { return [int]$parts[-1] }
    }
    return 0
}

$listenerPid = Get-ListenerPid -Port $Port
if ($listenerPid -eq 0) {
    Write-Host "OK: 127.0.0.1:$Port 没有监听进程（无需清理）"
    exit 0
}

$proc = Get-Process -Id $listenerPid -ErrorAction SilentlyContinue
Write-Host "`n注意: 127.0.0.1:$Port 被 PID $listenerPid 占用："
if ($proc) {
    Write-Host "  进程: $($proc.ProcessName) | 启动时间: $($proc.StartTime)"
}
Write-Host "`n警告: 如果这是 DeepSeek Harness GUI（开发对话运行所在），请按 N 取消！"

if (-not $Force) {
    $ans = Read-Host "确认杀死 PID $listenerPid 及其子进程树? (Y/N)"
    if ($ans -notmatch '^[Yy]') { Write-Host "已取消"; exit 1 }
}

taskkill /PID $listenerPid /T /F
Start-Sleep -Milliseconds 800

if ((Get-ListenerPid -Port $Port) -eq 0) {
    Write-Host "OK: 127.0.0.1:$Port 已释放"
} else {
    Write-Host "警告: 仍在监听？请以管理员权限重试。"
}
