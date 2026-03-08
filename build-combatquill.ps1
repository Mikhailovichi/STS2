param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

$workspace = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $workspace ".tools\dotnet9\dotnet.exe"
$dotnetDir = Split-Path -Parent $dotnet
$project = Join-Path $workspace "CombatQuill\CombatQuill.csproj"
$godot = Join-Path $workspace ".tools\godot451\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"

if (-not (Test-Path $dotnet)) {
    throw "Local .NET 9 SDK not found at $dotnet"
}

if (-not (Test-Path $project)) {
    throw "CombatQuill project not found at $project"
}

$command = if ($Publish) { "publish" } else { "build" }

Write-Host "Using $dotnet"
Write-Host "Running dotnet $command for CombatQuill"

$env:PATH = "$dotnetDir;$env:PATH"

$arguments = @($command, $project)
if ($Publish -and (Test-Path $godot)) {
    Write-Host "Using local Godot export tool at $godot"
    $arguments += "-p:GodotPath=$godot"
}

& $dotnet @arguments
