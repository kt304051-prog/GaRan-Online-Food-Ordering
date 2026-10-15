@echo off
setlocal
set "APP_DIR=%~dp0GaRan"
set "RUN_DIR=%APP_DIR%\bin\TeamRun-Final"

echo Dang build ban chay rieng de khong va cham voi Visual Studio...
dotnet build "%APP_DIR%\GaRan.csproj" --no-restore -o "%RUN_DIR%" /p:UseAppHost=false
if errorlevel 1 (
  echo Build that bai. Hay dong cua so ung dung cu neu dang chay, sau do thu lai.
  pause
  exit /b 1
)

powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -LocalPort 5181 -State Listen -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 (
  echo Cong khach hang 5181 dang chay san; giu nguyen tien trinh do.
) else (
  start "GaRan - Khach hang (5181)" powershell.exe -NoExit -ExecutionPolicy Bypass -Command "Set-Location -LiteralPath '%APP_DIR%'; $env:ASPNETCORE_ENVIRONMENT='Development'; $env:Portal__Mode='Customer'; dotnet '%RUN_DIR%\GaRan.dll' --urls http://0.0.0.0:5181"
  timeout /t 8 /nobreak >nul
)

powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -LocalPort 5182 -State Listen -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 (
  echo Cong quan tri 5182 dang chay san; giu nguyen tien trinh do.
) else (
  start "GaRan - Quan tri (5182)" powershell.exe -NoExit -ExecutionPolicy Bypass -Command "Set-Location -LiteralPath '%APP_DIR%'; $env:ASPNETCORE_ENVIRONMENT='Development'; $env:Portal__Mode='Internal'; dotnet '%RUN_DIR%\GaRan.dll' --urls http://0.0.0.0:5182"
)

echo Da mo hai cong. Khach hang: http://localhost:5181  Quan tri: http://localhost:5182
echo Khong dong hai cua so PowerShell khi dang su dung.
pause
