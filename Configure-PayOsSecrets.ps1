$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'GaRan\GaRan.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    throw "Không tìm thấy project GaRan: $project"
}

function Set-SecretFromPrompt([string]$key, [string]$prompt) {
    $secureValue = Read-Host $prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        $value = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "Giá trị $key không được để trống."
        }
        & dotnet user-secrets set $key $value --project $project | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Không lưu được $key vào User Secrets."
        }
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        $value = $null
        $secureValue.Dispose()
    }
}

Set-SecretFromPrompt 'PayOS:ClientId' 'PayOS Client ID'
Set-SecretFromPrompt 'PayOS:ApiKey' 'PayOS API Key'
Set-SecretFromPrompt 'PayOS:ChecksumKey' 'PayOS Checksum Key'

Write-Host ''
Write-Host 'Đã lưu 3 khóa payOS trong User Secrets của Windows user hiện tại; giá trị không được in ra.'
Write-Host 'Khởi động lại website ở Development để nạp cấu hình.'
Write-Host 'Trong bảng điều khiển payOS, đặt Webhook URL thành: https://<ten-mien-cong-khai>/webhooks/payos'
Write-Host 'Website phải có HTTPS công khai để payOS gửi xác nhận giao dịch. Không dùng localhost làm Webhook URL.'
