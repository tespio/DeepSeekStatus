param(
    [int[]]$Years
)

$ErrorActionPreference = 'Stop'

if (-not $Years -or $Years.Count -eq 0) {
    $current = (Get-Date).Year
    $Years = @(($current - 1), $current, ($current + 1))
}

$yearMap = [ordered]@{}
foreach ($year in $Years) {
    $url = "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/$year.json"
    try {
        $data = Invoke-RestMethod -Uri $url -UseBasicParsing
    }
    catch {
        Write-Warning "No holiday data for $year (skipped)"
        continue
    }

    $offDays = @($data.days | Where-Object { $_.isOffDay } | ForEach-Object { $_.date } | Sort-Object)
    if ($offDays.Count -gt 0) {
        $yearMap["$year"] = $offDays
        Write-Host "$year: $($offDays.Count) off days"
    }
    else {
        Write-Warning "No holiday data for $year (skipped)"
    }
}

$payload = [ordered]@{
    source  = 'https://github.com/NateScarlet/holiday-cn'
    note    = 'Chinese public holidays (State Council annual notices). Only full days off are listed; adjusted weekend workdays are already off-peak. Regenerate with Tools/update-holidays.ps1.'
    updated = (Get-Date -Format 'yyyy-MM-dd')
    years   = $yearMap
}

$target = Join-Path $PSScriptRoot '..\src\DeepSeekStatus\Assets\china-holidays.json'
$json = $payload | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($target, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $target"
