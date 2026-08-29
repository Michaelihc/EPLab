@echo off
setlocal
pushd "%~dp0"
dotnet run --project src\EPLab.Gui -c Release
set "eplab_exit=%errorlevel%"
popd
exit /b %eplab_exit%
