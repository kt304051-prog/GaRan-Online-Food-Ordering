$ErrorActionPreference = 'Stop'

$userSecretsId = '93548473-be06-487d-b499-679db001d3f1'
$secretsDirectory = Join-Path $env:APPDATA "Microsoft\UserSecrets\$userSecretsId"
$secretsPath = Join-Path $secretsDirectory 'secrets.json'
$hadSePaySecret = $false

function Read-HiddenValue([string] $Prompt) {
    $secureValue = Read-Host -Prompt $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        $secureValue.Dispose()
    }
}

$smtpHost = Read-Host 'SMTP host (example: smtp.gmail.com)'
if ([string]::IsNullOrWhiteSpace($smtpHost)) { throw 'SMTP host is required.' }

$portInput = Read-Host 'SMTP port [587]'
$smtpPort = 587
if (-not [string]::IsNullOrWhiteSpace($portInput) -and
    (-not [int]::TryParse($portInput, [ref]$smtpPort) -or $smtpPort -lt 1 -or $smtpPort -gt 65535)) {
    throw 'SMTP port must be a number from 1 through 65535.'
}

$smtpUsername = Read-Host 'SMTP username (usually the sender email)'
if ([string]::IsNullOrWhiteSpace($smtpUsername)) { throw 'SMTP username is required.' }
$smtpPassword = Read-HiddenValue 'SMTP password or app password (input is hidden)'
if ([string]::IsNullOrWhiteSpace($smtpPassword)) { throw 'SMTP password is required.' }

$fromEmail = Read-Host 'From email [leave blank to use Admin store email]'
$fromName = Read-Host 'From name [leave blank to use Admin store name]'
$sePaySecret = Read-HiddenValue 'SePay webhook secret from your SePay webhook settings (blank leaves auto confirmation disabled)'

New-Item -ItemType Directory -Path $secretsDirectory -Force | Out-Null
$secrets = @{}
if (Test-Path -LiteralPath $secretsPath) {
    $existing = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
    foreach ($property in $existing.PSObject.Properties) {
        $secrets[$property.Name] = $property.Value
    }
    $hadSePaySecret = -not [string]::IsNullOrWhiteSpace([string]$secrets['SePay:WebhookSecret'])
}

$secrets['Smtp:Host'] = $smtpHost.Trim()
$secrets['Smtp:Port'] = $smtpPort.ToString()
$secrets['Smtp:Username'] = $smtpUsername.Trim()
$secrets['Smtp:Password'] = $smtpPassword
$secrets['Smtp:EnableSsl'] = 'true'
if (-not [string]::IsNullOrWhiteSpace($fromEmail)) { $secrets['Smtp:FromEmail'] = $fromEmail.Trim() }
if (-not [string]::IsNullOrWhiteSpace($fromName)) { $secrets['Smtp:FromName'] = $fromName.Trim() }
if (-not [string]::IsNullOrWhiteSpace($sePaySecret)) { $secrets['SePay:WebhookSecret'] = $sePaySecret }

$json = $secrets | ConvertTo-Json -Depth 10
Set-Content -LiteralPath $secretsPath -Value $json -Encoding UTF8

Write-Output 'SMTP settings saved in the current Windows user secrets store, outside the project folder.'
if ([string]::IsNullOrWhiteSpace($sePaySecret) -and -not $hadSePaySecret) {
    Write-Output 'No SePay secret was entered; automatic payment confirmation remains disabled.'
}
Write-Output 'Restart the GaRan app to load the updated settings. Secrets were not printed.'
