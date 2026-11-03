# Creates the local code-signing certificate used by tools\sign-exe.ps1 and
# trusts its public key for the current user, so builds stop tripping
# SmartScreen. Run once per machine; safe to run again.

$ErrorActionPreference = 'Stop'

$subject = 'CN=ZHY Stock Notice (local)'
$existing = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
    Select-Object -First 1

if ($existing) {
    Write-Host ('Using existing certificate ' + $existing.Thumbprint)
    $cert = $existing
}
else {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -KeyExportPolicy Exportable `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host ('Created certificate ' + $cert.Thumbprint)
}

$cerPath = Join-Path $env:TEMP 'stocknotice-signing.cer'
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
certutil -user -addstore -f Root $cerPath | Out-Null
Write-Host 'Public key trusted for the current user.'
Write-Host 'Done. Run build.ps1 to produce a signed executable.'
