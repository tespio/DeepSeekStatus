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

    $holidays = @($data.days | Where-Object { $_.isOffDay } | Sort-Object date |
        ForEach-Object { [ordered]@{ date = $_.date; name = $_.name } })
    $workdays = @($data.days | Where-Object { -not $_.isOffDay } | Sort-Object date |
        ForEach-Object { [ordered]@{ date = $_.date; name = $_.name } })
    if ($holidays.Count -gt 0) {
        $yearMap["$year"] = [ordered]@{ holidays = $holidays; workdays = $workdays }
        Write-Host "${year}: $($holidays.Count) holidays, $($workdays.Count) make-up workdays"
    }
    else {
        Write-Warning "No holiday data for $year (skipped)"
    }
}

$payload = [ordered]@{
    source  = 'https://github.com/NateScarlet/holiday-cn'
    note    = 'Chinese public holidays and make-up workdays (State Council annual notices). Regenerate with Tools/update-holidays.ps1.'
    updated = (Get-Date -Format 'yyyy-MM-dd')
    years   = $yearMap
}

$target = Join-Path $PSScriptRoot '..\src\DeepSeekStatus\Assets\china-holidays.json'
$json = $payload | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($target, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $target"
