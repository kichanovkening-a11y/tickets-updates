@echo off
cd /d "%~dp0"
dotnet publish src\Tickets.App\Tickets.App.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.32 -o release\Tickets
if errorlevel 1 exit /b 1
copy README.txt release\Tickets\README.txt
