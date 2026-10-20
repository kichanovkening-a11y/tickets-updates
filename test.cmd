@echo off
cd /d "%~dp0"
dotnet run --project tests\Tickets.Tests\Tickets.Tests.csproj -- "%CD%\data\seed.db"
