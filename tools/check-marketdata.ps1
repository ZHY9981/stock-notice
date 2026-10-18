# Compiles and runs a live market-data check against the built desktop app.
# This tool needs network access and is intentionally not part of the test run.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$toolsDir = Join-Path $root 'tools'
$buildDir = Join-Path $root 'build'
$app = Join-Path $buildDir 'StockNotice.exe'

if (-not (Test-Path -LiteralPath $app)) {
    throw 'Build StockNotice.exe before running the live market-data check.'
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw 'C# compiler (csc.exe) was not found in the .NET Framework directory.'
}

$frameworkDir = Split-Path -Parent $csc
$output = Join-Path $buildDir 'MarketDataLiveCheck.exe'

$arguments = @(
    '/nologo',
    '/target:exe',
    '/platform:anycpu',
    '/codepage:65001',
    '/main:StockNotice.Tests.MarketDataLiveCheck',
    ('/out:{0}' -f $output),
    ('/reference:{0}' -f (Join-Path $frameworkDir 'System.dll')),
    ('/reference:{0}' -f (Join-Path $frameworkDir 'System.Core.dll')),
    ('/reference:{0}' -f $app),
    (Join-Path $toolsDir 'MarketDataLiveCheck.cs')
)

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Live-check compilation failed with exit code $LASTEXITCODE"
}

& $output @args
exit $LASTEXITCODE
