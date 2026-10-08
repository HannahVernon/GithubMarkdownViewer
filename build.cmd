@echo off
rem Builds GitHub Markdown Viewer and creates the Windows installer.
rem
rem Usage:
rem   build.cmd        Publish win-x64 and build the Windows installer
rem   build.cmd all    Publish win-x64, linux-x64 and osx-x64, then package what this machine can
rem
rem Requires the .NET SDK. Inno Setup 6 is optional (a portable ZIP is built without it).
rem Output goes to installer\output. The version comes from the csproj.
setlocal

set "BUILD_ARGS=-Runtime win-x64"
if /i "%~1"=="all" set "BUILD_ARGS="

where pwsh >nul 2>nul && set "PS_EXE=pwsh" || set "PS_EXE=powershell"

"%PS_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-all.ps1" %BUILD_ARGS%
exit /b %ERRORLEVEL%
