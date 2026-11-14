# 构建 CuteSakikoModRandom 的「加载器 + 按游戏版本变体」包。
#
# 结构（与本体 / 海克斯同款）：
#   CuteSakikoModRandom.dll          ← 加载器（loader/CuteSakikoModRandom.Loader.csproj）
#   CuteSakikoModRandom.json
#   CuteSakikoModRandom.pck          ← 共用一份
#   csk-random-variants.manifest     ← 变体清单（sha256）
#   compat/<版本>/CuteSakikoModRandom.dll
#
# 用法：
#   pwsh -File tools\build-bundle.ps1 -GameVersions '0.107.1,0.111.0,0.113.0' -Deploy
#
# 依赖：
#   · anti\Sts2Recover-<版本>\game-refs\ 下的 4 个程序集；
#   · 本体 compat\<版本>\CuteSakikoMod.dll（已部署）；
#   · AutoAnthony：0.113 用 repos 构建的 0.3.165；0.111/0.107 用 AA-0.3.147 的 0.111 构建。
param(
    [string[]]$GameVersions = @('0.113.0'),
    [string]$OutDir = 'E:\SLJT2\.build-check\random-bundle',
    [switch]$Deploy,
    [string]$ModsDir = 'E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods',
    [string]$AutoAnthony113 = 'E:\SLJT2\repos\AutoAnthony\ChaosMode\bin\Release\net9.0',
    [string]$AutoAnthony111 = 'E:\SLJT2\.build-check\AA-0.3.147\ChaosMode\bin\Release\net9.0'
)

$ErrorActionPreference = 'Stop'
$GameVersions = @($GameVersions | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$root = Split-Path -Parent $PSScriptRoot
$bundle = $OutDir
$compatRoot = Join-Path $bundle 'compat'

function Resolve-RitsuLib([string]$Version) {
    if ([version]$Version -ge [version]'0.113.0') {
        return @{
            Id = 'STS2.RitsuLib'
            Version = '0.6.11'
            Runtime = 'E:\SLJT2\Rider\RiderProjects\CuteSakikoMod\packages\sts2.ritsulib\0.6.11\lib\net9.0\STS2-RitsuLib.Runtime.dll'
        }
    }

    $compatVersion = if ([version]$Version -ge [version]'0.111.0') { '0.111.0' } else { '0.107.1' }
    $pkgName = "sts2.ritsulib.compat.$compatVersion"
    $runtime = "E:\SLJT2\Rider\RiderProjects\CuteSakikoMod\packages\$pkgName\0.6.11\lib\net9.0\STS2-RitsuLib.Runtime.dll"
    if (-not (Test-Path $runtime)) {
        $runtime = Join-Path $env:USERPROFILE ".nuget\packages\$pkgName\0.6.11\lib\net9.0\STS2-RitsuLib.Runtime.dll"
    }
    return @{
        Id = "STS2.RitsuLib.Compat.$compatVersion"
        Version = '0.6.11'
        Runtime = $runtime
    }
}

function Resolve-RefsDir([string]$Version) {
    $short = $Version -replace '\.0$', ''
    $pattern = '^Sts2Recover-' + [regex]::Escape($short)
    foreach ($dir in Get-ChildItem 'E:\SLJT2\anti' -Directory | Where-Object { $_.Name -match $pattern }) {
        $refs = Join-Path $dir.FullName 'game-refs'
        if (Test-Path (Join-Path $refs 'sts2.dll')) { return $refs }
    }
    throw "找不到 $Version 的 game-refs（anti\Sts2Recover-*）"
}

function Resolve-MainModDll([string]$Version) {
    $path = Join-Path (Join-Path $ModsDir 'CuteSakikoMod') "compat\$Version\CuteSakikoMod.dll"
    if (Test-Path $path) { return $path }
    $alt = "E:\SLJT2\.build-check\mainmod-$Version\CuteSakikoMod.dll"
    if (Test-Path $alt) { return $alt }
    throw "找不到 $Version 的本体变体 DLL：$path"
}

function Resolve-AutoAnthony([string]$Version) {
    $dir = if ([version]$Version -ge [version]'0.113.0') { $AutoAnthony113 } else { $AutoAnthony111 }
    if (-not (Test-Path (Join-Path $dir 'AutoAnthony.dll'))) {
        throw "找不到 AutoAnthony 引用：$dir\AutoAnthony.dll"
    }
    return $dir
}

New-Item -ItemType Directory -Force -Path $compatRoot | Out-Null

foreach ($version in $GameVersions) {
    $refs = Resolve-RefsDir $version
    $rl = Resolve-RitsuLib $version
    if (-not (Test-Path $rl.Runtime)) { throw "缺少 RitsuLib Runtime：$($rl.Runtime)" }
    $mainDll = Resolve-MainModDll $version
    $aaDir = Resolve-AutoAnthony $version
    Write-Host "[bundle] $version 本体=$mainDll AA=$aaDir" -ForegroundColor DarkGray

    $target = Join-Path $compatRoot $version
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Write-Host "[bundle] 构建 $version 变体 ..." -ForegroundColor Cyan
    & dotnet build (Join-Path $root 'CuteSakikoModRandom.csproj') -c Release -v m `
        -o $target `
        -p:AssemblyName=CuteSakikoModRandom `
        -p:Sts2GameVersion=$version `
        -p:Sts2DataDir=$refs `
        -p:AutoAnthonyDir=$aaDir `
        -p:RitsuLibPackageId=$($rl.Id) `
        -p:RitsuLibPackageVersion=$($rl.Version) `
        -p:RitsuLibRuntimeDll=$($rl.Runtime) `
        -p:CuteSakikoModDll=$mainDll `
        -p:SkipPck=true -p:SkipDeploy=true | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "变体 $version 构建失败" }

    Set-Content -Path (Join-Path $target 'compat-target.txt') -Value $version -NoNewline
    Get-ChildItem $target -Filter '*.deps.json' | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $target -Filter '*.pdb' | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $target -Filter '*.json' | Where-Object { $_.Name -ne 'compat-target.txt' } | Remove-Item -Force -ErrorAction SilentlyContinue
}

# 加载器
Write-Host '[bundle] 构建加载器 ...' -ForegroundColor Cyan
$loaderOut = Join-Path $OutDir 'loader'
& dotnet build (Join-Path $root 'loader\CuteSakikoModRandom.Loader.csproj') -c Release -v m -o $loaderOut | Out-Host
if ($LASTEXITCODE -ne 0) { throw '加载器构建失败' }
Copy-Item (Join-Path $loaderOut 'CuteSakikoModRandom.Loader.dll') (Join-Path $bundle 'CuteSakikoModRandom.dll') -Force

Copy-Item (Join-Path $root 'CuteSakikoModRandom.json') (Join-Path $bundle 'CuteSakikoModRandom.json') -Force

$pckSource = Join-Path (Join-Path $ModsDir 'CuteSakikoModRandom') 'CuteSakikoModRandom.pck'
if (Test-Path $pckSource) {
    Copy-Item $pckSource (Join-Path $bundle 'CuteSakikoModRandom.pck') -Force
} else {
    Write-Warning "没有找到现有 pck：$pckSource（先用正常构建导出一次 pck）"
}

$variants = @()
foreach ($target in Get-ChildItem $compatRoot -Directory | Sort-Object Name) {
    $dll = Join-Path $target.FullName 'CuteSakikoModRandom.dll'
    if (-not (Test-Path $dll)) { continue }
    $variants += [ordered]@{
        compatTarget = $target.Name
        assembly = 'CuteSakikoModRandom'
        sha256 = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifest = [ordered]@{ schema = 1; variants = $variants }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $bundle 'csk-random-variants.manifest') -Encoding UTF8

Write-Host "[bundle] 清单：$($variants.Count) 个变体" -ForegroundColor Green
$variants | ForEach-Object { Write-Host ("  {0}  {1}" -f $_.compatTarget, $_.sha256) }

if ($Deploy) {
    $dest = Join-Path $ModsDir 'CuteSakikoModRandom'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $bundle 'CuteSakikoModRandom.dll') $dest -Force
    Copy-Item (Join-Path $bundle 'CuteSakikoModRandom.json') $dest -Force
    if (Test-Path (Join-Path $bundle 'CuteSakikoModRandom.pck')) {
        Copy-Item (Join-Path $bundle 'CuteSakikoModRandom.pck') $dest -Force
    }
    Copy-Item (Join-Path $bundle 'csk-random-variants.manifest') $dest -Force
    Copy-Item (Join-Path $bundle 'compat') $dest -Recurse -Force
    Write-Host "[bundle] 已部署到 $dest" -ForegroundColor Green
}
