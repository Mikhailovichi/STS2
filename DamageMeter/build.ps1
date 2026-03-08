[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$SkipBackup
)

$ErrorActionPreference = "Stop"

function Get-BuildToolchain {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $sdkRoot = Join-Path (Split-Path -Parent $dotnet) "sdk"
    $sdk = Get-ChildItem -Path $sdkRoot -Directory |
        ForEach-Object {
            $match = [regex]::Match($_.Name, '^\d+(\.\d+)+')
            if ($match.Success) {
                [pscustomobject]@{
                    Version = [version]$match.Value
                    CscPath = Join-Path $_.FullName "Roslyn\\bincore\\csc.dll"
                }
            }
        } |
        Where-Object { Test-Path $_.CscPath } |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if (-not $sdk) {
        throw "Could not find a Roslyn compiler under $sdkRoot."
    }

    return [pscustomobject]@{
        DotnetPath = $dotnet
        CscPath = $sdk.CscPath
        Version = $sdk.Version
    }
}

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$moddingRoot = Split-Path -Parent $sourceRoot
$gameRoot = Split-Path -Parent (Split-Path -Parent $sourceRoot)
$modsRoot = Join-Path $gameRoot "mods"
$managedDir = Join-Path $gameRoot "data_sts2_windows_x86_64"
$buildDir = Join-Path $sourceRoot "build"
$responseFile = Join-Path $buildDir "build.rsp"
$outputDll = Join-Path $buildDir "DamageMeter.dll"
$installDll = Join-Path $modsRoot "DamageMeter\\DamageMeter.dll"
$manifestPath = Join-Path $sourceRoot "mod_manifest.json"
$projectPath = Join-Path $sourceRoot "project.godot"
$godotPath = Join-Path $moddingRoot ".tools\\godot451\\Godot_v4.5.1-stable_mono_win64\\Godot_v4.5.1-stable_mono_win64.exe"
$installModDir = Split-Path -Parent $installDll
$installPck = Join-Path $installModDir "DamageMeter.pck"

if (-not (Test-Path $managedDir)) {
    throw "Managed DLL directory not found: $managedDir"
}

New-Item -ItemType Directory -Path $buildDir -Force | Out-Null

$toolchain = Get-BuildToolchain
$references = Get-ChildItem -Path $managedDir -Filter *.dll |
    Where-Object {
        try {
            [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null
            $true
        }
        catch [System.BadImageFormatException] {
            $false
        }
    } |
    Sort-Object Name
$resources = @(
    Get-ChildItem -Path $sourceRoot -Filter "DamageMeter.localization*.json"
    Get-ChildItem -Path $sourceRoot -Filter "DamageMeter.icons.*.png"
) | Sort-Object Name
$sources = @(
    Get-ChildItem -Path (Join-Path $sourceRoot "DamageMeter") -Recurse -Filter *.cs
    Get-ChildItem -Path (Join-Path $sourceRoot "GodotPlugins") -Recurse -Filter *.cs
    Get-ChildItem -Path (Join-Path $sourceRoot "Properties") -Recurse -Filter *.cs
) | Sort-Object FullName

if ($sources.Count -eq 0) {
    throw "No C# sources found under $sourceRoot."
}

$responseLines = @(
    "/langversion:latest"
    "/target:library"
    "/filealign:512"
    "/optimize+"
    "/deterministic+"
    "/unsafe+"
    "/nullable:enable"
    "/out:`"$outputDll`""
)

$responseLines += $references | ForEach-Object { "/reference:`"$($_.FullName)`"" }
$responseLines += $resources | ForEach-Object { "/resource:`"$($_.FullName)`",$($_.Name)" }
$responseLines += $sources | ForEach-Object { "`"$($_.FullName)`"" }

$responseLines | Set-Content -Path $responseFile -Encoding UTF8

Write-Host "Building DamageMeter with SDK $($toolchain.Version)..."
& $toolchain.DotnetPath $toolchain.CscPath "@$responseFile"
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Built DLL: $outputDll"

if ($Install) {
    if (-not (Test-Path $installDll)) {
        throw "Installed mod DLL not found: $installDll"
    }

    $runningGame = Get-Process -Name "SlayTheSpire2" -ErrorAction SilentlyContinue
    if ($runningGame) {
        $pidList = ($runningGame | Select-Object -ExpandProperty Id) -join ", "
        throw "Slay the Spire 2 is currently running (PID: $pidList). Close the game, then rerun build.ps1 -Install."
    }

    if (-not $SkipBackup) {
        $backupDir = Join-Path $installModDir "backups"
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupDll = Join-Path $backupDir "DamageMeter.$timestamp.dll"
        $backupPck = Join-Path $backupDir "DamageMeter.$timestamp.pck"
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Copy-Item -Path $installDll -Destination $backupDll -Force
        if (Test-Path $installPck) {
            Copy-Item -Path $installPck -Destination $backupPck -Force
            Write-Host "Backed up current PCK to: $backupPck"
        }
        Write-Host "Backed up current DLL to: $backupDll"
    }

    try {
        Copy-Item -Path $outputDll -Destination $installDll -Force
        if (Test-Path $manifestPath) {
            Copy-Item -Path $manifestPath -Destination (Join-Path $installModDir "mod_manifest.json") -Force
        }
    }
    catch [System.IO.IOException] {
        throw "Could not install because $installDll is locked. Close the game, then rerun build.ps1 -Install."
    }

    Write-Host "Installed DLL to: $installDll"

    if ((Test-Path $projectPath) -and (Test-Path $manifestPath) -and (Test-Path $godotPath)) {
        Write-Host "Exporting DamageMeter.pck with Godot..."
        Push-Location $sourceRoot
        try {
            & $godotPath --headless --export-pack "Windows Desktop" $installPck
            if ($LASTEXITCODE -ne 0) {
                throw "Godot export failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
        Write-Host "Installed PCK to: $installPck"
    }
    else {
        Write-Host "Skipping PCK export because project.godot, mod_manifest.json, or Godot is missing."
    }
}
