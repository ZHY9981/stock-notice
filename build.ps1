# Builds the Stock Notice desktop app with the C# compiler shipped in Windows.
# No .NET SDK or NuGet packages are required.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$outDir = Join-Path $root 'build'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw 'C# compiler (csc.exe) was not found in the .NET Framework directory.'
}

$frameworkDir = Split-Path -Parent $csc
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Stop-RunningInstance {
    param([string]$TargetPath)

    $running = @(Get-Process -Name 'StockNotice' -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) { return }

    Write-Host 'StockNotice is running; closing it so the executable can be replaced ...'
    foreach ($item in $running) {
        try {
            Stop-Process -Id $item.Id -Force -ErrorAction Stop
        }
        catch {
            Write-Warning ('Could not close process {0}: {1}' -f $item.Id, $_.Exception.Message)
        }
    }

    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        if (-not (Get-Process -Name 'StockNotice' -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 250
    }

    if (Test-Path -LiteralPath $TargetPath) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try {
                $stream = [System.IO.File]::Open($TargetPath, 'Open', 'ReadWrite', 'None')
                $stream.Close()
                break
            }
            catch {
                Start-Sleep -Milliseconds 250
            }
        }
    }
}

$referenceNames = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll',
    'System.Net.Http.dll'
)

$references = @()
foreach ($name in $referenceNames) {
    $references += ('/reference:{0}' -f (Join-Path $frameworkDir $name))
}

$sources = Get-ChildItem -Path (Join-Path $srcDir '*.cs') | Sort-Object Name | ForEach-Object {
    $_.FullName
}
if (-not $sources) {
    throw "No source files were found in $srcDir"
}

$output = Join-Path $outDir 'StockNotice.exe'
Stop-RunningInstance -TargetPath $output

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    '/main:StockNotice.Program',
    ('/out:{0}' -f $output)
) + $references + $sources

Write-Host 'Compiling StockNotice.exe ...'
& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host 'Build failed. If the error mentions CS0016 and a file in use, close' -ForegroundColor Yellow
    Write-Host 'StockNotice (including its tray icon) and run this script again.' -ForegroundColor Yellow
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host ('Build succeeded: {0}' -f $output)
