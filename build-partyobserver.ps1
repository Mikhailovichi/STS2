param(
    [switch]$Publish,
    [string]$Sts2Path,
    [string]$DotnetPath,
    [string]$GodotPath
)

$ErrorActionPreference = "Stop"

$workspace = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $workspace "PartyObserver\PartyObserver.csproj"

function Resolve-FullPath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $null
    }

    return [System.IO.Path]::GetFullPath($PathValue)
}

function Resolve-GameRoot {
    param([string]$StartPath)

    $current = Resolve-FullPath $StartPath
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path (Join-Path $current "data_sts2_windows_x86_64")) {
            return $current
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }

        $current = $parent
    }

    return $null
}

function Resolve-ExistingPath {
    param([string[]]$Candidates)

    foreach ($candidate in $Candidates) {
        $fullPath = Resolve-FullPath $candidate
        if (-not [string]::IsNullOrWhiteSpace($fullPath) -and (Test-Path $fullPath)) {
            return $fullPath
        }
    }

    return $null
}

$resolvedSts2Path = if ($Sts2Path) { Resolve-FullPath $Sts2Path } else { Resolve-GameRoot $workspace }
if ([string]::IsNullOrWhiteSpace($resolvedSts2Path) -or -not (Test-Path (Join-Path $resolvedSts2Path "data_sts2_windows_x86_64"))) {
    throw "Could not resolve the Slay the Spire 2 install directory. Pass -Sts2Path to the game root."
}

$dotnet = Resolve-ExistingPath @(
    $DotnetPath,
    (Join-Path $workspace ".tools\dotnet9_full\dotnet.exe"),
    (Join-Path $workspace ".tools\dotnet9\dotnet.exe"),
    (Join-Path $resolvedSts2Path "modding\.tools\dotnet9_full\dotnet.exe"),
    (Join-Path $resolvedSts2Path "modding\.tools\dotnet9\dotnet.exe")
)

if (-not $dotnet) {
    $systemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($systemDotnet) {
        $systemVersion = (& $systemDotnet.Source --version).Trim()
        if ([version]$systemVersion -ge [version]"9.0.0") {
            $dotnet = $systemDotnet.Source
        }
    }
}

if (-not $dotnet) {
    throw "Unable to find a .NET 9 SDK. Pass -DotnetPath, add .tools to the repo, or keep the repo near a game install with modding\\.tools\\dotnet9."
}

$godot = Resolve-ExistingPath @(
    $GodotPath,
    (Join-Path $workspace ".tools\godot451\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"),
    (Join-Path $resolvedSts2Path "modding\.tools\godot451\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe")
)

$command = if ($Publish) { "publish" } else { "build" }
$dotnetDir = Split-Path -Parent $dotnet

Write-Host "Using Slay the Spire 2 root: $resolvedSts2Path"
Write-Host "Using dotnet: $dotnet"
Write-Host "Running dotnet $command for PartyObserver"

$env:PATH = "$dotnetDir;$env:PATH"

$arguments = @($command, $project, "-p:Sts2Path=$resolvedSts2Path")
if ($Publish -and $godot) {
    Write-Host "Using Godot export tool: $godot"
    $arguments += "-p:GodotPath=$godot"
}

& $dotnet @arguments
