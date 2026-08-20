<#
.SYNOPSIS
  一键发布 DSHW（完全版 / 单文件版 / Lite 文件夹），输出到浅层目录，无需打开 VS。

.DESCRIPTION
  输出目录：DSHW.Desktop\release\<variant>\（full / single / lite）
  用法：
    .\scripts\publish.ps1                 # 发布全部三个版本
    .\scripts\publish.ps1 -Target full    # 只发布完全版（SCD 单文件，免装运行时）
    .\scripts\publish.ps1 -Target single  # 单文件版（WinAppSDK 自包含，需 .NET 9）
    .\scripts\publish.ps1 -Target lite    # Lite 文件夹（需 .NET 9 + WinAppSDK Runtime）
#>
param(
    [ValidateSet("all", "full", "single", "lite")]
    [string]$Target = "all"
)

$ErrorActionPreference = "Stop"

$root     = Split-Path -Parent $PSScriptRoot          # 仓库根（scripts 的上一级）
$project  = Join-Path $root "DSHW.Desktop\DSHW.Desktop.csproj"
$outBase  = Join-Path $root "DSHW.Desktop\release"

if (-not (Test-Path $project)) { throw "找不到项目文件: $project" }

function Publish-Variant {
    param(
        [string]$Name,
        [string]$Profile,
        [string]$Dir
    )
    Write-Host "`n=== 发布: $Name ==="

    # 目标目录清理（对齐 VS 发布的 DeleteExistingFiles）
    if (Test-Path $Dir) {
        # 若正在运行的 DSHW 锁定了输出文件，先提示
        $running = Get-Process -Name "DSHW.Desktop" -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($Dir, [System.StringComparison]::OrdinalIgnoreCase) }
        if ($running) {
            Write-Warning "检测到 DSHW 正在运行（$($running.Path)），输出文件可能被锁定。"
            $ans = Read-Host "建议先退出该应用。终止发布？(Y/n)"
            if ($ans -notmatch '^[Nn]') { throw "已终止：请先退出正在运行的 DSHW" }
        }
        Remove-Item $Dir -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $Dir) { throw "无法清理目标目录（文件被占用）: $Dir —— 请关闭正在运行的 DSHW 后重试" }
    }
    New-Item -ItemType Directory -Path $Dir -Force | Out-Null

    # 输出路径由 pubxml 的 PublishDir 决定（release\<variant>\），这里不覆盖
    dotnet publish $project -c Release -p:Platform=x64 -p:PublishProfile=$Profile
    if ($LASTEXITCODE -ne 0) { throw "发布 [$Name] 失败 (exit=$LASTEXITCODE)" }

    # 清理运行残留（如 WebView2 用户数据目录，正常应已挪到 %LOCALAPPDATA%）
    Get-ChildItem $Dir -Directory -Filter "*.WebView2" -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    $files   = Get-ChildItem $Dir -File -ErrorAction SilentlyContinue
    $totalMB = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 1)
    $exe     = $files | Where-Object { $_.Name -eq "DSHW.Desktop.exe" } | Select-Object -First 1
    $exeMB   = if ($exe) { [math]::Round($exe.Length / 1MB, 1) } else { 0 }
    Write-Host "完成: $($files.Count) 个文件, 总计 $totalMB MB, exe $exeMB MB"
    Write-Host "位置: $Dir"
}

switch ($Target) {
    "full"   { Publish-Variant "完全版 (SCD 单文件, 目标机免装任何运行时)" "full" (Join-Path $outBase "full") }
    "single" { Publish-Variant "单文件版 (WinAppSDK 自包含, 需 .NET 9 Runtime)" "single" (Join-Path $outBase "single") }
    "lite"   { Publish-Variant "Lite 文件夹 (FDD, 需 .NET 9 + WinAppSDK Runtime)" "lite" (Join-Path $outBase "lite") }
    default  {
        Publish-Variant "完全版 (SCD 单文件, 目标机免装任何运行时)" "full" (Join-Path $outBase "full")
        Publish-Variant "单文件版 (WinAppSDK 自包含, 需 .NET 9 Runtime)" "single" (Join-Path $outBase "single")
        Publish-Variant "Lite 文件夹 (FDD, 需 .NET 9 + WinAppSDK Runtime)" "lite" (Join-Path $outBase "lite")
    }
}

Write-Host "`n✅ 全部完成，输出目录: $outBase"
