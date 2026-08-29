$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

dotnet build .\EPLab.sln -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project .\tests\EPLab.Compiler.Tests -c Release --no-build
exit $LASTEXITCODE
