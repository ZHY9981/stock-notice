# Compiles and runs the rule-engine checks with the C# compiler in Windows.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$srcDir = Join-Path $root 'src'
$toolsDir = Join-Path $root 'tools'
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

$references = @('System.dll', 'System.Core.dll', 'System.Net.Http.dll', 'System.Web.Extensions.dll') | ForEach-Object {
    '/reference:{0}' -f (Join-Path $frameworkDir $_)
}

$sources = @(
    (Join-Path $srcDir 'Models.cs'),
    (Join-Path $srcDir 'Models.StockCandidate.cs'),
    (Join-Path $srcDir 'TradingCalendar.cs'),
    (Join-Path $srcDir 'MarketClock.cs'),
    (Join-Path $srcDir 'AlertPolicy.cs'),
    (Join-Path $srcDir 'IMarketQuoteProvider.cs'),
    (Join-Path $srcDir 'MarketDataProvider.cs'),
    (Join-Path $srcDir 'AkShareQuoteParser.cs'),
    (Join-Path $srcDir 'RuleEngine.cs'),
    (Join-Path $toolsDir 'RuleEngineTests.cs')
)

$output = Join-Path $outDir 'RuleEngineTests.exe'

$arguments = @(
    '/nologo',
    '/target:exe',
    '/platform:anycpu',
    '/codepage:65001',
    ('/resource:{0},StockNotice.trading-calendar.json' -f (Join-Path $srcDir 'trading-calendar.json')),
    '/main:StockNotice.Tests.RuleEngineTests',
    ('/out:{0}' -f $output)
) + $references + $sources

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Test compilation failed with exit code $LASTEXITCODE"
}

& $output
exit $LASTEXITCODE
