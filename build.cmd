@echo off
setlocal
pushd "%~dp0"
dotnet build EPLab.sln -c Release
if errorlevel 1 goto :finish
dotnet run --project tests\EPLab.Compiler.Tests -c Release --no-build
:finish
set "eplab_exit=%errorlevel%"
echo.
pause
popd
exit /b %eplab_exit%
