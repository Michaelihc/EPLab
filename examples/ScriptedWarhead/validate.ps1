$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')

$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $root "bin\Release\package"))
$rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar
if (-not $packageRoot.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe validation package path: $packageRoot"
}
$staleFile = Join-Path $packageRoot "plugins\ScriptedWarheadEPLab-old.dll"
foreach ($pathComponent in @(
    (Join-Path $root "bin"),
    (Join-Path $root "bin\Release"),
    $packageRoot,
    (Split-Path -Parent $staleFile)
)) {
    if (Test-Path -LiteralPath $pathComponent) {
        $componentItem = Get-Item -LiteralPath $pathComponent -Force
        if (($componentItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to seed a stale file through a reparse point: $pathComponent"
        }
    }
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $staleFile) | Out-Null
Set-Content -LiteralPath $staleFile -Encoding ASCII -Value "stale validation sentinel"
if (-not (Test-Path -LiteralPath $staleFile -PathType Leaf)) {
    throw "Could not seed the stale-package regression sentinel."
}

& (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) { throw "Build validation failed." }
if (Test-Path -LiteralPath $staleFile) {
    throw "Exact package replacement failed: stale file survived."
}

$sourcePath = (Get-ChildItem -LiteralPath (Join-Path $root "src") -File | Select-Object -First 1).FullName
$source = Get-Content -Raw -Encoding UTF8 -LiteralPath $sourcePath
$required = @(
    "Schedule",
    "DMS at 19:00",
    "IsDeadmanSwitchDetonation",
    "actual native audio playback",
    "ApplyOmegaFacility",
    "DetonateAndKill",
    ".RA"
)

foreach ($marker in $required) {
    if (-not $source.Contains($marker)) {
        throw "Missing ScriptedWarhead behavior marker: $marker"
    }
}

$plugin = Join-Path $packageRoot "plugins\ScriptedWarheadEPLab.dll"
$bridge = Join-Path $packageRoot "dependencies\ScriptedWarhead.EPLab.Bridge.dll"
[void][System.Reflection.AssemblyName]::GetAssemblyName($plugin)
[void][System.Reflection.AssemblyName]::GetAssemblyName($bridge)

$expectedPackageFiles = @(
    [System.IO.Path]::GetFullPath($plugin),
    [System.IO.Path]::GetFullPath((Join-Path $packageRoot "plugins\ScriptedWarheadEPLab.pdb")),
    [System.IO.Path]::GetFullPath($bridge),
    [System.IO.Path]::GetFullPath((Join-Path $packageRoot "dependencies\ScriptedWarhead.EPLab.Bridge.pdb"))
)
$actualPackageFiles = @(Get-ChildItem -LiteralPath $packageRoot -Force -Recurse -File)
if ($actualPackageFiles.Count -ne $expectedPackageFiles.Count) {
    throw "Exact package regression failed: unexpected file count $($actualPackageFiles.Count)."
}
foreach ($file in $actualPackageFiles) {
    if ($expectedPackageFiles -notcontains [System.IO.Path]::GetFullPath($file.FullName)) {
        throw "Exact package regression failed: unexpected file $($file.FullName)."
    }
}

$generated = Join-Path $root "obj\eplab\Release\EPLab.All.Generated.cs.txt"
if (-not (Select-String -LiteralPath $generated -SimpleMatch "#line " -Quiet)) {
    throw "Generated C# lost its source map."
}

Write-Host "ScriptedWarhead EPLab validation passed."
