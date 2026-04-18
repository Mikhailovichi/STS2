param()

$ErrorActionPreference = "Stop"

$modRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$servicePath = Join-Path $modRoot "Services\RelicRpsFightService.cs"
$livePresentationPath = Join-Path $modRoot "Services\RelicRpsLiveFightPresentationService.cs"
$uiCompatPath = Join-Path $modRoot "Services\RelicSharedUiCompatService.cs"
$overlayPath = Join-Path $modRoot "UI\RelicRpsChoiceOverlay.cs"
$skipPatchPath = Join-Path $modRoot "Patches\SkipButtonPatch.cs"
$collectionPatchPath = Join-Path $modRoot "Patches\TreasureRoomRelicCollectionPatch.cs"

if (-not (Test-Path $servicePath)) {
    throw "RelicRpsFightService.cs not found at $servicePath"
}

if (-not (Test-Path $livePresentationPath)) {
    throw "RelicRpsLiveFightPresentationService.cs not found at $livePresentationPath"
}

if (-not (Test-Path $uiCompatPath)) {
    throw "RelicSharedUiCompatService.cs not found at $uiCompatPath"
}

if (-not (Test-Path $overlayPath)) {
    throw "RelicRpsChoiceOverlay.cs not found at $overlayPath"
}

if (-not (Test-Path $skipPatchPath)) {
    throw "SkipButtonPatch.cs not found at $skipPatchPath"
}

if (-not (Test-Path $collectionPatchPath)) {
    throw "TreasureRoomRelicCollectionPatch.cs not found at $collectionPatchPath"
}

$serviceCode = Get-Content -Raw $servicePath
$livePresentationCode = Get-Content -Raw $livePresentationPath
$uiCompatCode = Get-Content -Raw $uiCompatPath
$overlayCode = Get-Content -Raw $overlayPath
$skipPatchCode = Get-Content -Raw $skipPatchPath
$collectionPatchCode = Get-Content -Raw $collectionPatchPath

$failures = New-Object System.Collections.Generic.List[string]
$passes = New-Object System.Collections.Generic.List[string]

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if ($Condition) {
        $passes.Add($Message)
    }
    else {
        $failures.Add($Message)
    }
}

function Get-LosingMove {
    param(
        [int]$Move1,
        [int]$Move2
    )

    if ((($Move1 + 1) % 3) -eq $Move2) {
        return $Move1
    }

    return $Move2
}

function Resolve-FightScenario {
    param(
        [int[]]$Players,
        [object[]]$Rounds
    )

    $active = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($player in $Players) {
        [void]$active.Add($player)
    }

    $roundIndex = 0
    foreach ($round in $Rounds) {
        if ($active.Count -le 1) {
            break
        }

        $moves = @{}
        foreach ($player in $Players) {
            if ($active.Contains($player)) {
                if (-not $round.ContainsKey($player)) {
                    throw "Missing move for active player $player in round $($roundIndex + 1)"
                }

                $moves[$player] = [int]$round[$player]
            }
        }

        $distinct = $moves.Values | Sort-Object -Unique
        if ($distinct.Count -eq 2) {
            $losing = Get-LosingMove $distinct[0] $distinct[1]
            foreach ($player in @($moves.Keys)) {
                if ($moves[$player] -eq $losing) {
                    [void]$active.Remove([int]$player)
                }
            }
        }

        $roundIndex++
    }

    if ($active.Count -ne 1) {
        throw "Fight did not resolve to exactly one winner. Remaining: $($active.Count)"
    }

    return $active | Select-Object -First 1
}

function Get-LayoutPositions {
    param(
        [int]$VisibleHolderCount
    )

    $sampleWidth = 184.0
    $sampleHeight = 250.0
    $gapX = [Math]::Max($sampleWidth * 0.12, 24.0)
    $gapY = [Math]::Max($sampleHeight * 0.12, 26.0)
    $columns = [Math]::Min(4, $VisibleHolderCount)
    $rows = [int][Math]::Ceiling($VisibleHolderCount / [double]$columns)
    $positions = @()

    $anchorCenterX = 0.0
    $anchorCenterY = 0.0
    $totalHeight = $rows * $sampleHeight + ($rows - 1) * $gapY
    $firstRowY = $anchorCenterY - $totalHeight * 0.5 + $sampleHeight * 0.5

    for ($row = 0; $row -lt $rows; $row++) {
        $rowStartIndex = $row * $columns
        $rowItemCount = [Math]::Min($columns, $VisibleHolderCount - $rowStartIndex)
        $rowWidth = $rowItemCount * $sampleWidth + ($rowItemCount - 1) * $gapX
        $firstColumnX = $anchorCenterX - $rowWidth * 0.5 + $sampleWidth * 0.5

        for ($column = 0; $column -lt $rowItemCount; $column++) {
            $x = $firstColumnX + $column * ($sampleWidth + $gapX) - $sampleWidth * 0.5
            $y = $firstRowY + $row * ($sampleHeight + $gapY) - $sampleHeight * 0.5
            $positions += [pscustomobject]@{
                Index = $rowStartIndex + $column
                X = [Math]::Round($x, 4)
                Y = [Math]::Round($y, 4)
            }
        }
    }

    return $positions
}

Assert-True (-not ($serviceCode -match "sessionId")) "Removed local sessionId-only move path."
Assert-True (-not ($serviceCode -match "RelicRpsMoveMessage")) "Removed custom relic RPS network message path."
Assert-True (-not ($serviceCode -match "GameActionPlayerChoiceContext")) "No GameActionPlayerChoiceContext dependency in RPS flow."
Assert-True (-not ($serviceCode -match "SignalPlayerChoiceBegun")) "No default PlayerChoice Begin/End UI hook in RPS flow."
Assert-True ($serviceCode -match "PlayerChoiceSynchronizer") "RPS flow uses PlayerChoiceSynchronizer."
Assert-True ($serviceCode -match "TryMoveFromChoiceResult") "RPS flow validates remote move payload."
Assert-True ($serviceCode -match "TryPresentRoundAsync") "RPS flow triggers live round presentation after each move reveal."
Assert-True ($serviceCode -match "HasActivePrompt") "RPS service exposes active prompt state."
Assert-True ($serviceCode -match "IsTreasureSessionActive") "RPS service exposes shared relic session state."
Assert-True ($livePresentationCode -match "RegisterCollection") "Live fight presentation service tracks active treasure room UI."
Assert-True ($livePresentationCode -match "ShouldHandleRelicsAwarded") "Live fight presentation service can suppress duplicate end-of-fight playback."
Assert-True ($livePresentationCode -match "AnimateRelicAwardsAsync") "Live fight presentation service provides custom relic award animation flow."
Assert-True ($uiCompatCode -match "EnsureHolderCapacity") "Shared relic UI holder expansion exists."
Assert-True ($uiCompatCode -match "ReflowHolderLayout") "Shared relic UI 5+ holder reflow exists."
Assert-True ($overlayCode -match "_UnhandledInput") "Overlay handles keyboard input."
Assert-True ($overlayCode -match "TryMapShortcutToMove") "Overlay supports direct shortcut move submit."
Assert-True ($overlayCode -match "CycleFocus") "Overlay supports directional focus switching."
Assert-True ($skipPatchCode -match "OnSkipButtonReleased") "Skip release interception patch exists."
Assert-True ($skipPatchCode -match "NChoiceSelectionSkipButton") "Skip press interception patch exists."
Assert-True ($skipPatchCode -match "HideAndDisableSkipButton") "Skip button is force-hidden/disabled in choose relic screen."
Assert-True ($skipPatchCode -match "_Ready") "Skip button hide patch runs after NChooseARelicSelection._Ready."
Assert-True ($skipPatchCode -match "AfterOverlayShown") "Skip button hide patch runs after overlay shown."
Assert-True ($skipPatchCode -match "IsTreasureSessionActive") "Skip suppression is gated by shared relic session state."
Assert-True ($collectionPatchCode -match "RegisterCollection") "Treasure room collection patch registers live presentation UI."
Assert-True ($collectionPatchCode -match "OnRelicsAwarded") "Treasure room collection patch intercepts relic award playback when needed."

$positions = Get-LayoutPositions -VisibleHolderCount 8
Assert-True ($positions.Count -eq 8) "8-player layout generates 8 holder slots."

$uniquePositions = $positions | ForEach-Object { "$($_.X),$($_.Y)" } | Sort-Object -Unique
Assert-True ($uniquePositions.Count -eq 8) "8-player holder slots are unique."

$fightA = Resolve-FightScenario -Players @(1, 2, 3, 4) -Rounds @(
    @{ 1 = 0; 2 = 1; 3 = 1; 4 = 0 },
    @{ 2 = 2; 3 = 1 }
)
Assert-True ($fightA -eq 2) "Fight scenario A resolves to expected winner."

$fightB = Resolve-FightScenario -Players @(5, 6, 7, 8) -Rounds @(
    @{ 5 = 0; 6 = 0; 7 = 1; 8 = 2 },
    @{ 5 = 0; 6 = 1; 7 = 1; 8 = 1 },
    @{ 6 = 2; 7 = 0; 8 = 0 },
    @{ 7 = 1; 8 = 0 }
)
Assert-True ($fightB -eq 7) "Fight scenario B resolves to expected winner."

Assert-True (($fightA -ne $fightB)) "Two relic fights can resolve independently."

Write-Host "=== RelicRpsChoice Regression Summary ==="
foreach ($pass in $passes) {
    Write-Host "[PASS] $pass"
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "[FAIL] $failure"
    }

    throw "Regression script found $($failures.Count) failure(s)."
}

Write-Host "All regression checks passed."
