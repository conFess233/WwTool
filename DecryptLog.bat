@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul

set "WWTOOL_ROOT=%~dp0"

if "%~1"=="" (
    set /p "WWTOOL_LOG_FILE=请输入要解密的 .log 文件路径："
) else (
    set "WWTOOL_LOG_FILE=%~1"
)

if not defined WWTOOL_LOG_FILE (
    echo 未指定日志文件。
    exit /b 1
)

set "WWTOOL_LOG_FILE=%WWTOOL_LOG_FILE:"=%"

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference = 'Stop';" ^
    "try {" ^
    "    $source = [System.IO.Path]::GetFullPath($env:WWTOOL_LOG_FILE);" ^
    "    if (-not [System.IO.File]::Exists($source)) { throw '指定的日志文件不存在：' + $source };" ^
    "    if ([System.IO.Path]::GetExtension($source) -ine '.log') { throw '仅支持解密 .log 文件：' + $source };" ^
    "    $outputDirectory = [System.IO.Path]::Combine($env:WWTOOL_ROOT, 'Decrypt');" ^
    "    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null;" ^
    "    $stream = [System.IO.File]::Open($source, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite);" ^
    "    try {" ^
    "        $memory = New-Object System.IO.MemoryStream;" ^
    "        try { $stream.CopyTo($memory); [byte[]]$data = $memory.ToArray() } finally { $memory.Dispose() };" ^
    "    } finally { $stream.Dispose() };" ^
    "    for ($i = 0; $i -lt $data.Length; $i++) {" ^
    "        [byte]$value = $data[$i];" ^
    "        if (($value -band 1) -ne 0) { $data[$i] = $value -bxor 0xA5 } else { $data[$i] = $value -bxor 0xEF };" ^
    "    };" ^
    "    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($source);" ^
    "    $target = [System.IO.Path]::Combine($outputDirectory, $baseName + '_decrypted.log');" ^
    "    if ([System.IO.File]::Exists($target)) {" ^
    "        $stamp = [DateTime]::Now.ToString('yyyyMMdd_HHmmssfff');" ^
    "        $target = [System.IO.Path]::Combine($outputDirectory, $baseName + '_decrypted_' + $stamp + '.log');" ^
    "    };" ^
    "    [System.IO.File]::WriteAllBytes($target, $data);" ^
    "    Write-Host ('解密完成：' + $target);" ^
    "} catch {" ^
    "    Write-Error $_.Exception.Message;" ^
    "    exit 1;" ^
    "}"

if errorlevel 1 (
    echo 解密失败。
    exit /b 1
)

exit /b 0
