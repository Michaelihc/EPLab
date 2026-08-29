param(
    [string]$ManagedDirectory = ""
)

$ErrorActionPreference = "Stop"
$exampleRoot = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$eplabRoot = (Resolve-Path (Join-Path $exampleRoot "..\..")).Path
$Configuration = "Release"

function Assert-StrictChildPath {
    param([string]$Parent, [string]$Child)

    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\')
    $childPath = [System.IO.Path]::GetFullPath($Child).TrimEnd('\')
    $prefix = $parentPath + [System.IO.Path]::DirectorySeparatorChar
    if (-not $childPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe path outside expected parent: $childPath"
    }
}

function Assert-NoReparsePoints {
    param([string]$Root)

    $rootItem = Get-Item -LiteralPath $Root -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to operate on a reparse point: $Root"
    }

    $pending = New-Object 'System.Collections.Generic.Stack[System.IO.DirectoryInfo]'
    $pending.Push([System.IO.DirectoryInfo]$rootItem)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($item in $directory.GetFileSystemInfos()) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing to operate through a reparse point: $($item.FullName)"
            }
            if ($item -is [System.IO.DirectoryInfo]) {
                $pending.Push($item)
            }
        }
    }
}

function Assert-ExactPackage {
    param([string]$Root, [object[]]$Entries)

    Assert-NoReparsePoints $Root
    $rootPath = [System.IO.Path]::GetFullPath($Root)
    $expectedDirectories = @(
        [System.IO.Path]::GetFullPath((Join-Path $rootPath "plugins")),
        [System.IO.Path]::GetFullPath((Join-Path $rootPath "dependencies"))
    )
    $actualDirectories = @(Get-ChildItem -LiteralPath $rootPath -Force -Recurse -Directory)
    if ($actualDirectories.Count -ne $expectedDirectories.Count) {
        throw "Package has an unexpected directory count: $($actualDirectories.Count)"
    }
    foreach ($directory in $actualDirectories) {
        if ($expectedDirectories -notcontains [System.IO.Path]::GetFullPath($directory.FullName)) {
            throw "Package has an unexpected directory: $($directory.FullName)"
        }
    }

    $expectedFiles = @($Entries | ForEach-Object {
        [System.IO.Path]::GetFullPath((Join-Path $rootPath $_.RelativePath))
    })
    $actualFiles = @(Get-ChildItem -LiteralPath $rootPath -Force -Recurse -File)
    if ($actualFiles.Count -ne $expectedFiles.Count) {
        throw "Package has an unexpected file count: $($actualFiles.Count)"
    }
    foreach ($file in $actualFiles) {
        if ($expectedFiles -notcontains [System.IO.Path]::GetFullPath($file.FullName)) {
            throw "Package has an unexpected file: $($file.FullName)"
        }
    }

    foreach ($entry in $Entries) {
        $destination = [System.IO.Path]::GetFullPath((Join-Path $rootPath $entry.RelativePath))
        if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
            throw "Package is missing: $($entry.RelativePath)"
        }
        $sourceHash = (Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "Packaged file does not match its build output: $($entry.RelativePath)"
        }
        if ($entry.AssemblyName) {
            $actualName = [System.Reflection.AssemblyName]::GetAssemblyName($destination).Name
            if ($actualName -ne $entry.AssemblyName) {
                throw "Unexpected assembly identity '$actualName' in $($entry.RelativePath)"
            }
        }
    }
}

if ($ManagedDirectory) {
    $env:SCP_SL_MANAGED = (Resolve-Path $ManagedDirectory).Path
}

$bridgeProject = Join-Path $exampleRoot "Bridge\ScriptedWarhead.EPLab.Bridge.csproj"
$projectFile = Join-Path $exampleRoot "project.eplabproj"
$compilerProject = Join-Path $eplabRoot "src\EPLab.Cli\EPLab.Cli.csproj"

Write-Host "1/3 Building the narrow SCP:SL bridge..."
dotnet build $bridgeProject --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Bridge build failed." }

Write-Host "2/3 Compiling Easy-style source -> C# -> LabAPI DLL..."
$compilerArguments = @("run", "--project", $compilerProject, "--configuration", "Release", "--", "build", $projectFile, "--cn")
if ($env:SCP_SL_MANAGED) {
    $compilerArguments += @("--managed", $env:SCP_SL_MANAGED)
}
dotnet @compilerArguments
if ($LASTEXITCODE -ne 0) { throw "EPLab build failed." }

Write-Host "3/3 Making a two-folder deployment package..."
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $exampleRoot "bin\$Configuration"))
$binRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $releaseRoot))
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot "package"))
$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot (".package-stage-" + [System.Guid]::NewGuid().ToString("N"))))
$backupRoot = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot (".package-backup-" + [System.Guid]::NewGuid().ToString("N"))))
Assert-StrictChildPath $exampleRoot $binRoot
Assert-StrictChildPath $binRoot $releaseRoot
Assert-StrictChildPath $releaseRoot $packageRoot
Assert-StrictChildPath $releaseRoot $stageRoot
Assert-StrictChildPath $releaseRoot $backupRoot
Assert-NoReparsePoints $binRoot

$pluginOutput = Join-Path $exampleRoot "bin\$Configuration"
$bridgeOutput = Join-Path $exampleRoot "Bridge\bin\$Configuration\net48"
$packageEntries = @(
    [pscustomobject]@{
        Source = Join-Path $pluginOutput "ScriptedWarheadEPLab.dll"
        RelativePath = "plugins\ScriptedWarheadEPLab.dll"
        AssemblyName = "ScriptedWarheadEPLab"
    },
    [pscustomobject]@{
        Source = Join-Path $pluginOutput "ScriptedWarheadEPLab.pdb"
        RelativePath = "plugins\ScriptedWarheadEPLab.pdb"
        AssemblyName = $null
    },
    [pscustomobject]@{
        Source = Join-Path $bridgeOutput "ScriptedWarhead.EPLab.Bridge.dll"
        RelativePath = "dependencies\ScriptedWarhead.EPLab.Bridge.dll"
        AssemblyName = "ScriptedWarhead.EPLab.Bridge"
    },
    [pscustomobject]@{
        Source = Join-Path $bridgeOutput "ScriptedWarhead.EPLab.Bridge.pdb"
        RelativePath = "dependencies\ScriptedWarhead.EPLab.Bridge.pdb"
        AssemblyName = $null
    }
)

foreach ($entry in $packageEntries) {
    if (-not (Test-Path -LiteralPath $entry.Source -PathType Leaf)) {
        throw "Required build output is missing: $($entry.Source)"
    }
}

New-Item -ItemType Directory -Path (Join-Path $stageRoot "plugins"), (Join-Path $stageRoot "dependencies") | Out-Null
try {
    foreach ($entry in $packageEntries) {
        $destination = Join-Path $stageRoot $entry.RelativePath
        Copy-Item -LiteralPath $entry.Source -Destination $destination
    }
    Assert-ExactPackage $stageRoot $packageEntries

    $hadPreviousPackage = Test-Path -LiteralPath $packageRoot
    if ($hadPreviousPackage) {
        if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
            throw "The package target exists but is not a directory: $packageRoot"
        }
        Assert-NoReparsePoints $packageRoot
        Move-Item -LiteralPath $packageRoot -Destination $backupRoot
    }

    try {
        Move-Item -LiteralPath $stageRoot -Destination $packageRoot
        Assert-ExactPackage $packageRoot $packageEntries
        if ($hadPreviousPackage) {
            Assert-NoReparsePoints $backupRoot
            Remove-Item -LiteralPath $backupRoot -Recurse -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $packageRoot) {
            Assert-NoReparsePoints $packageRoot
            Remove-Item -LiteralPath $packageRoot -Recurse -Force
        }
        if (Test-Path -LiteralPath $backupRoot) {
            Move-Item -LiteralPath $backupRoot -Destination $packageRoot
        }
        throw
    }
}
finally {
    if (Test-Path -LiteralPath $stageRoot) {
        Assert-NoReparsePoints $stageRoot
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }
}

Write-Host "Done: $packageRoot"
