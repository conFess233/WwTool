# 本地打包脚本：生成 Windows x64 发布包到 publish/（已被 .gitignore 忽略）。
#
#   powershell -ExecutionPolicy Bypass -File publish.ps1
#
# 生成 6 个 zip（解压后直接是 WwTool.exe、Local 与 portable 标记文件）：
#   WwTool-<版本>-win-x64-single.zip               单文件，不带运行时（需安装 .NET 10 桌面运行时）
#   WwTool-<版本>-win-x64-single-runtime.zip       单文件，带运行时
#   WwTool-<版本>-win-x64-multi.zip                多文件，不带运行时
#   WwTool-<版本>-win-x64-multi-runtime.zip        多文件，带运行时
#   WwTool-<版本>-win-x64-single-lite.zip          单文件，不带运行时，不带 Local/Icons
#   WwTool-<版本>-win-x64-multi-lite.zip           多文件，不带运行时，不带 Local/Icons
# Lite 版缺少的图标在用到时从仓库下载（与完整版缺图时的行为相同）。
# 另生成 SHA256SUMS.txt。
param(
    # 只打部分包时用，例如 -Only single,single-lite
    [string[]]$Only
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = $PSScriptRoot
$project = Join-Path $root 'src\UI\WwTool.UI.csproj'
$icons = Join-Path $root 'WwTool\Local\Icons'
$out = Join-Path $root 'publish'
$work = Join-Path $out 'work'

[xml]$props = Get-Content (Join-Path $root 'src\Directory.Build.props') -Encoding UTF8
$version = ([string]$props.Project.PropertyGroup.Version).Trim()
if (-not $version) { throw '在 src/Directory.Build.props 中找不到 Version' }

# 名称 → 发布方式与是否带图标。
$packages = [ordered]@{
    'single'         = @{ Build = 'single';         Icons = $true }
    'single-runtime' = @{ Build = 'single-runtime'; Icons = $true }
    'multi'          = @{ Build = 'multi';          Icons = $true }
    'multi-runtime'  = @{ Build = 'multi-runtime';  Icons = $true }
    'single-lite'    = @{ Build = 'single';         Icons = $false }
    'multi-lite'     = @{ Build = 'multi';          Icons = $false }
}
if ($Only) {
    $Only = @($Only -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($name in $Only) { if (-not $packages.Contains($name)) { throw "未知的包：$name（可选：$($packages.Keys -join ', ')）" } }
    foreach ($name in @($packages.Keys)) { if ($Only -notcontains $name) { $packages.Remove($name) } }
}

$builds = @{
    'single'         = @('--self-contained', 'false', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true')
    'single-runtime' = @('--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true')
    'multi'          = @('--self-contained', 'false')
    'multi-runtime'  = @('--self-contained', 'true')
}

if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null

# 1. 发布：每种方式只编译一次，Lite 复用不带运行时的结果。
$needed = @($packages.Values | ForEach-Object { $_.Build } | Select-Object -Unique)
foreach ($build in $needed) {
    $dir = Join-Path $work "build-$build"
    Write-Host "== 发布 $build" -ForegroundColor Cyan
    $publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '-o', $dir, '-nologo', '-v', 'q',
        '-p:DebugType=none', "-p:InformationalVersion=v$version") + $builds[$build]
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败：$build" }
}

# 2. 组装、检查、压缩。
$zips = @()
foreach ($name in $packages.Keys) {
    $spec = $packages[$name]
    $stage = Join-Path $work "pkg-$name"
    Copy-Item (Join-Path $work "build-$($spec.Build)") $stage -Recurse
    if ($spec.Icons) { Copy-Item $icons (Join-Path $stage 'Local\Icons') -Recurse }

    # 检查：必须有的文件，不该有的文件。
    $problems = @()
    foreach ($required in 'WwTool.exe', 'portable', 'Local\Data\Characters.json', 'Local\Language\zh-Hans.json') {
        if (-not (Test-Path (Join-Path $stage $required))) { $problems += "缺少 $required" }
    }
    $files = @(Get-ChildItem $stage -Recurse -File)
    $relative = $files | ForEach-Object { $_.FullName.Substring($stage.Length + 1) }
    foreach ($path in $relative) {
        if ($path -like '*.pdb') { $problems += "多余的调试符号 $path" }
        if ($path -like 'Config\*' -or $path -like 'Local\Data\*.db*' -or $path -like 'Local\Cache\*' -or $path -like 'Local\Logs\*') { $problems += "包含用户数据 $path" }
    }
    $hasIcons = Test-Path (Join-Path $stage 'Local\Icons')
    if ($spec.Icons -and -not $hasIcons) { $problems += '缺少 Local\Icons' }
    if (-not $spec.Icons -and $hasIcons) { $problems += 'Lite 包不应包含 Local\Icons' }
    if ($spec.Build -like 'single*') {
        $loose = @($relative | Where-Object { $_ -notlike 'Local\*' -and $_ -ne 'WwTool.exe' -and $_ -ne 'portable' })
        if ($loose.Count) { $problems += "单文件包根目录有多余文件：$($loose -join ', ')" }
    }
    if ($problems.Count) { throw "$name 检查未通过：`n  $($problems -join "`n  ")" }

    $zip = Join-Path $out "WwTool-$version-win-x64-$name.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $zips += Get-Item $zip
    '{0,-46} {1,8:N1} MB  {2,4} 个文件' -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB), $files.Count | Write-Host
}

# 3. 校验和。
$sums = $zips | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
[System.IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'), [string[]]$sums)

Remove-Item $work -Recurse -Force
Write-Host "完成：$out" -ForegroundColor Green
