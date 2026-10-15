#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$serviceName = 'MSSQLSERVER'
$tcpPath = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQLServer\SuperSocketNetLib\Tcp'
$loopbackPath = Join-Path $tcpPath 'IP10'
$backupPath = Join-Path $env:TEMP ("GaRan-Sql-Tcp-Before-{0}.reg" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

if (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
    throw "SQL Server service '$serviceName' was not found."
}
if (-not (Test-Path $loopbackPath)) {
    throw "SQL Server loopback configuration was not found: $loopbackPath"
}

& reg.exe export 'HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQLServer\SuperSocketNetLib\Tcp' $backupPath /y | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not back up the current TCP settings. No settings were changed.'
}

$tcpKeys = Get-ChildItem -Path $tcpPath
foreach ($key in $tcpKeys) {
    if ($key.PSChildName -match '^IP\d+$' -and $key.PSChildName -ne 'IP10') {
        Set-ItemProperty -Path $key.PSPath -Name Enabled -Value 0
    }
}

Set-ItemProperty -Path $tcpPath -Name ListenOnAllIPs -Value 0
Set-ItemProperty -Path $tcpPath -Name Enabled -Value 1
Set-ItemProperty -Path $loopbackPath -Name Enabled -Value 1
Set-ItemProperty -Path $loopbackPath -Name TcpPort -Value '1433'
Set-ItemProperty -Path $loopbackPath -Name TcpDynamicPorts -Value ''

Restart-Service -Name $serviceName -Force
$service = Get-Service -Name $serviceName
$service.WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
Start-Sleep -Seconds 2

$listeners = @(Get-NetTCPConnection -State Listen -LocalPort 1433 -ErrorAction SilentlyContinue)
if ($listeners.Count -ne 1 -or $listeners[0].LocalAddress -ne '127.0.0.1') {
    $addresses = ($listeners | ForEach-Object LocalAddress) -join ', '
    throw "SQL Server is not listening only on 127.0.0.1:1433. Listening addresses: $addresses. Backup: $backupPath"
}

Write-Output 'SQL Server is listening locally at 127.0.0.1:1433. TCP is not exposed to the LAN.'
Write-Output "Previous settings were backed up to: $backupPath"

