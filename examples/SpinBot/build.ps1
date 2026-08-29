[CmdletBinding()]
param(
    [string] $ManagedDirectory,
    [string] $GlobalDependenciesDirectory,
    [string] $HarmonyPath
)

$ErrorActionPreference = 'Stop'
$exampleDirectory = [System.IO.Path]::GetFullPath($PSScriptRoot)
$eplabRoot = [System.IO.Path]::GetFullPath((Join-Path $exampleDirectory '..\..'))
$cliProject = Join-Path $eplabRoot 'src\EPLab.Cli\EPLab.Cli.csproj'
$projectFile = Join-Path $exampleDirectory 'project.eplabproj'
$bridgeProject = Join-Path $exampleDirectory 'bridge\SpinBot.EPLab.Bridge.csproj'
$releaseDirectory = [System.IO.Path]::GetFullPath((Join-Path $exampleDirectory 'bin\Release'))
$packageDirectory = [System.IO.Path]::GetFullPath((Join-Path $releaseDirectory 'package'))

function Assert-ReleaseChild {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $LeafPattern
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $parent = [System.IO.Path]::GetFullPath((Split-Path -Parent $fullPath))
    $leaf = Split-Path -Leaf $fullPath
    if (-not [string]::Equals($parent, $releaseDirectory, [System.StringComparison]::OrdinalIgnoreCase) -or
        $leaf -notlike $LeafPattern) {
        throw "Refusing a package filesystem operation outside the exact release directory: $fullPath"
    }
    return $fullPath
}

function Get-ContainedRelativePath {
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $Path
    )

    $trimCharacters = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $rootPrefix = [System.IO.Path]::GetFullPath($Root).TrimEnd($trimCharacters) +
        [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to report a package file outside the package directory: $fullPath"
    }

    return $fullPath.Substring($rootPrefix.Length).Replace(
        [System.IO.Path]::AltDirectorySeparatorChar,
        [System.IO.Path]::DirectorySeparatorChar)
}

function Remove-ValidatedTree {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $LeafPattern
    )

    $validated = Assert-ReleaseChild -Path $Path -LeafPattern $LeafPattern
    if (-not (Test-Path -LiteralPath $validated)) { return }
    $item = Get-Item -LiteralPath $validated -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to recurse into a non-directory or redirected package path: $validated"
    }
    $resolved = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $validated).Path)
    if (-not [string]::Equals($resolved, $validated, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a redirected package path: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

if (-not [string]::Equals(
    $packageDirectory,
    [System.IO.Path]::GetFullPath((Join-Path $releaseDirectory 'package')),
    [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The package path failed its exact-target safety check.'
}
if (-not (Test-Path -LiteralPath $cliProject -PathType Leaf)) {
    throw "The EPLab CLI project was not found relative to this example: $cliProject"
}

$nativeDirectory = Join-Path $exampleDirectory 'bridge\ReferenceNative'
$privateNativeFiles = @(
    'AutoAimFireService.cs',
    'AutoFireRateMath.cs',
    'CorrectOnFirePatch.cs',
    'DummyRevolverPatches.cs',
    'DummySpinService.cs',
    'SpinRateMath.cs',
    'VisualRotationPatch.cs'
)
$missingPrivateNativeFiles = @($privateNativeFiles | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $nativeDirectory $_) -PathType Leaf)
})
if ($missingPrivateNativeFiles.Count -ne 0) {
    throw ('Cement''s private SpinBot native-edge sources are intentionally not included in the public repository. ' +
        'This full validation build requires the seven private files under bridge\ReferenceNative. Missing: ' +
        ($missingPrivateNativeFiles -join ', '))
}

if ([string]::IsNullOrWhiteSpace($ManagedDirectory)) {
    $ManagedDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed'
}
$ManagedDirectory = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($ManagedDirectory))

if ([string]::IsNullOrWhiteSpace($GlobalDependenciesDirectory)) {
    $GlobalDependenciesDirectory = Join-Path $env:APPDATA 'SCP Secret Laboratory\LabAPI\dependencies\global'
}
$GlobalDependenciesDirectory = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($GlobalDependenciesDirectory))

if ([string]::IsNullOrWhiteSpace($HarmonyPath)) {
    $candidates = @(
        $env:EPLAB_HARMONY_DLL,
        (Join-Path $GlobalDependenciesDirectory '0Harmony.dll')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $HarmonyPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($HarmonyPath) -or -not (Test-Path -LiteralPath $HarmonyPath -PathType Leaf)) {
    throw '0Harmony.dll was not found. Pass -HarmonyPath or set EPLAB_HARMONY_DLL.'
}
$HarmonyPath = [System.IO.Path]::GetFullPath($HarmonyPath)
if (-not (Test-Path -LiteralPath (Join-Path $ManagedDirectory 'LabApi.dll') -PathType Leaf)) {
    throw "LabApi.dll was not found under: $ManagedDirectory"
}
if (-not (Test-Path -LiteralPath (Join-Path $GlobalDependenciesDirectory 'ServerKeybinds.dll') -PathType Leaf)) {
    throw "ServerKeybinds.dll was not found under: $GlobalDependenciesDirectory"
}

$nativeHashes = [ordered]@{
    'AutoAimFireService.cs' = '1AD20A0840787B8205ABEA7D37E16F0E79C361C4CC259273BFA7581CB1EA9576'
    'AutoFireRateMath.cs' = '419C881065A75960C96BA14EF15B9D195AD0B617253FB291904407D6F1CF8E49'
    'CorrectOnFirePatch.cs' = 'E68F38FF9B9FCC47D5350931938D1F9A08714EA38655D48E994D06BF6E4843D7'
    'DummyRevolverPatches.cs' = '2F9EE91CD9FC2B55C84AB032CEC0178494D0AF744A395A889EE96D55EA9261BB'
    'DummySpinService.cs' = '893FC38EBA810C4E5D1E7ACC001C4407991372E3CEC71A8DB3097D7F6EDDABC3'
    'SpinRateMath.cs' = 'E3A4CBE288B2531660E53C3B5460A1BD896E1F1BC78DD27C38C112109C0490C6'
    'VisualRotationPatch.cs' = 'FD1F7D20C3FFD609D9A5304A0B564AEEF5580BA0B777C3B25AB0EFD371ECAEFC'
}

Write-Host '1/5 Verifying the locally supplied private native-edge inputs...'
foreach ($entry in $nativeHashes.GetEnumerator()) {
    $sourcePath = Join-Path $nativeDirectory $entry.Key
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Pinned native source is missing: $sourcePath"
    }
    $actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    if (-not [string]::Equals($actualHash, $entry.Value, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Pinned native source hash changed without a provenance update: $($entry.Key)"
    }
}

Write-Host '2/5 Building the narrow native bridge...'
dotnet build $bridgeProject -c Release --nologo "-p:SCP_SL_MANAGED=$ManagedDirectory" "-p:HarmonyPath=$HarmonyPath"
if ($LASTEXITCODE -ne 0) { throw 'Native bridge build failed.' }

Write-Host '3/5 Compiling Easy-style source -> generated C# -> net48 LabAPI DLL...'
dotnet run --project $cliProject -c Release -- build $projectFile --managed $ManagedDirectory --dependencies $GlobalDependenciesDirectory --cn
if ($LASTEXITCODE -ne 0) { throw 'EPLab compilation failed.' }

$stageLeaf = 'package.staging.' + [Guid]::NewGuid().ToString('N')
$stageDirectory = Assert-ReleaseChild -Path (Join-Path $releaseDirectory $stageLeaf) -LeafPattern 'package.staging.*'
$stagePluginDirectory = Join-Path $stageDirectory 'plugins'
$stageGlobalDirectory = Join-Path $stageDirectory 'dependencies\global'

Write-Host '4/5 Staging the structured package...'
try {
    New-Item -ItemType Directory -Force -Path $stagePluginDirectory | Out-Null
    New-Item -ItemType Directory -Force -Path $stageGlobalDirectory | Out-Null
    $stagePlugin = Join-Path $stagePluginDirectory 'EPLabSpinBot.dll'
    $stageBridge = Join-Path $stageGlobalDirectory 'SpinBot.EPLab.Bridge.dll'
    Copy-Item -LiteralPath (Join-Path $exampleDirectory 'bin\Release\EPLabSpinBot.dll') -Destination $stagePlugin -Force
    Copy-Item -LiteralPath (Join-Path $exampleDirectory 'bridge\bin\Release\SpinBot.EPLab.Bridge.dll') -Destination $stageBridge -Force

    Write-Host '5/5 Testing Easy math and dependency-resolved generated plugin/command types...'
    $testProject = Join-Path $exampleDirectory 'tests\SpinBot.MathHarness.csproj'
    $harmonyDirectory = Split-Path -Parent $HarmonyPath
    dotnet run --project $testProject -c Release -- $stagePlugin $stageGlobalDirectory $ManagedDirectory $GlobalDependenciesDirectory $harmonyDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Staged Easy-side and reflection validation failed.'
    }
} catch {
    if (Test-Path -LiteralPath $stageDirectory) {
        Remove-ValidatedTree -Path $stageDirectory -LeafPattern 'package.staging.*'
    }
    throw
}

$backupDirectory = $null
try {
    if (Test-Path -LiteralPath $packageDirectory) {
        $existingPackage = Get-Item -LiteralPath $packageDirectory -Force
        if (-not $existingPackage.PSIsContainer -or
            ($existingPackage.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to replace a non-directory or redirected package target: $packageDirectory"
        }
        $backupLeaf = 'package.previous.' + [Guid]::NewGuid().ToString('N')
        $backupDirectory = Assert-ReleaseChild -Path (Join-Path $releaseDirectory $backupLeaf) -LeafPattern 'package.previous.*'
        Move-Item -LiteralPath $packageDirectory -Destination $backupDirectory
    }
    Move-Item -LiteralPath $stageDirectory -Destination $packageDirectory
} catch {
    if (-not (Test-Path -LiteralPath $packageDirectory) -and
        $null -ne $backupDirectory -and
        (Test-Path -LiteralPath $backupDirectory)) {
        Move-Item -LiteralPath $backupDirectory -Destination $packageDirectory
    }
    if (Test-Path -LiteralPath $stageDirectory) {
        Remove-ValidatedTree -Path $stageDirectory -LeafPattern 'package.staging.*'
    }
    throw
}

if ($null -ne $backupDirectory -and (Test-Path -LiteralPath $backupDirectory)) {
    Remove-ValidatedTree -Path $backupDirectory -LeafPattern 'package.previous.*'
}

Write-Host "Done. Validated package published to: $packageDirectory"
Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relativePath = Get-ContainedRelativePath -Root $packageDirectory -Path $_.FullName
    $sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    Write-Host ("  {0} ({1} bytes, SHA-256 {2})" -f $relativePath, $_.Length, $sha256)
}
Write-Host 'Not redistributed: install 0Harmony.dll and ServerKeybinds.dll separately in LabAPI dependencies/global.'
