@echo off
title Calling Card Studio
cd /d "%~dp0"
if exist "Calling Card Studio.exe" (
    start "" "Calling Card Studio.exe"
) else if exist "Publish\Calling Card Studio.exe" (
    start "" "Publish\Calling Card Studio.exe"
) else (
    dotnet run
)
