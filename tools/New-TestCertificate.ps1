<#
.SYNOPSIS
  Creates the self-signed test certificate that signs the open-ClaudeOS MSIX.

.DESCRIPTION
  The package's Publisher (in windows/src/ClaudeOS.Shell/Package.appxmanifest) must equal the
  certificate subject, so the subject is read from the manifest. Writes a .pfx (private key, keep it
  secret) and a .cer (public, what you install to trust builds signed by it).

  For a stable signature across builds (so you trust the .cer once), run this locally, then store the
  PFX and its password as repository secrets. Never commit either:

    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("claudeos-test.pfx"))
    gh secret set CLAUDEOS_TEST_PFX_BASE64 --body $b64
    gh secret set CLAUDEOS_TEST_PFX_PASSWORD

  Without those secrets, CI creates a throwaway certificate per run and publishes its .cer next to
  the package.

.PARAMETER OutDir
  Where to write claudeos-test.pfx and claudeos-test.cer.

.PARAMETER Password
  Password for the PFX. A random one is generated and printed if omitted.
#>
[CmdletBinding()]
param(
  [string] $OutDir = ".",
  [securestring] $Password
)

$ErrorActionPreference = "Stop"
$manifest = Join-Path $PSScriptRoot "..\windows\src\ClaudeOS.Shell\Package.appxmanifest"
$subject = ([xml](Get-Content -Raw $manifest)).Package.Identity.Publisher
if (-not $subject) { throw "Could not read Publisher from $manifest" }

$generated = $false
if (-not $Password) {
  $plain = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
  $Password = ConvertTo-SecureString $plain -AsPlainText -Force
  $generated = $true
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject $subject `
  -KeyUsage DigitalSignature `
  -FriendlyName "open-ClaudeOS test signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -NotAfter (Get-Date).AddYears(2) `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$pfx = Join-Path $OutDir "claudeos-test.pfx"
$cer = Join-Path $OutDir "claudeos-test.cer"
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $Password | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"

Write-Host "Subject:  $subject"
Write-Host "PFX:      $pfx   (secret: never commit)"
Write-Host "CER:      $cer   (install into Local Machine > Trusted People)"
if ($generated) {
  Write-Host "Password: $([Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)))"
}
