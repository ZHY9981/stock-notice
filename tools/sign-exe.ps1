# Signs the built executable with the local code-signing certificate.
# The certificate lives in the current user's Personal store and its public
# part is trusted in the current user's Root store, so SmartScreen treats the
# program as a known publisher instead of an unknown app.

param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "File not found: $Path"
}

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -like '*CN=ZHY Stock Notice (local)*' -and $_.HasPrivateKey } |
    Select-Object -First 1

if (-not $cert) {
    Write-Warning 'Local signing certificate not found; the build stays unsigned.'
    Write-Warning 'Recreate it with tools\make-signing-cert.ps1 (see AGENTS.md).'
    exit 0
}

$signature = Set-AuthenticodeSignature -FilePath $Path -Certificate $cert -HashAlgorithm SHA256
if ($signature.Status -ne 'Valid') {
    Write-Warning ('Signing failed: ' + $signature.Status + '. The build stays unsigned.')
    exit 0
}

Write-Host ('Signed with ' + $cert.Subject + ' (' + $cert.Thumbprint + ')')
