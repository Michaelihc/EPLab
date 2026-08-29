$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

dotnet run --project .\src\EPLab.Gui -c Release
exit $LASTEXITCODE
