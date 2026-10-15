$ErrorActionPreference = 'Stop'

$userSecretsId = '93548473-be06-487d-b499-679db001d3f1'
$secretsDirectory = Join-Path $env:APPDATA "Microsoft\UserSecrets\$userSecretsId"
$secretsPath = Join-Path $secretsDirectory 'secrets.json'

if (-not (Test-Path -LiteralPath $secretsPath)) {
    throw 'SMTP settings were not found. Run Configure-EmailSecrets.ps1 first.'
}

$secureValue = Read-Host 'Paste the new 16-character Gmail app password (input hidden; spaces are removed)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
try {
    $appPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) -replace '\s', ''
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $secureValue.Dispose()
}

if ($appPassword.Length -ne 16) {
    throw "The Gmail app password must contain exactly 16 characters after spaces are removed; received $($appPassword.Length). Nothing was changed."
}

$secrets = @{}
$existing = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
foreach ($property in $existing.PSObject.Properties) {
    $secrets[$property.Name] = $property.Value
}
$secrets['Smtp:Password'] = $appPassword
Set-Content -LiteralPath $secretsPath -Value ($secrets | ConvertTo-Json -Depth 10) -Encoding UTF8

Write-Output 'Gmail app password updated in the current Windows user secrets store. The password was not printed.'
Write-Output 'Restart the GaRan app before trying email delivery again.'
