param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Run,
    [switch]$SelfTest,
    [switch]$Publish,
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\DeepSeekStatus\DeepSeekStatus.csproj'

if ($Publish) {
    $version = (Select-String -Path $project -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
    $out = Join-Path $PSScriptRoot "Build\DeepSeekStatus-$Runtime"
    $zip = Join-Path $PSScriptRoot "Build\DeepSeekStatus-$version-$Runtime.zip"

    if (Test-Path $out) {
        Remove-Item -Recurse -Force $out
    }

    dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -o $out
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force

    Write-Host ""
    Write-Host "Published (no .NET runtime required):"
    Write-Host "  $out\DeepSeekStatus.exe"
    Write-Host "  $zip"
    exit 0
}

dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $PSScriptRoot "src\DeepSeekStatus\bin\$Configuration\net10.0-windows\DeepSeekStatus.exe"

if ($SelfTest) {
    & $exe --selftest
    exit $LASTEXITCODE
}

if ($Run) {
    Start-Process -FilePath $exe
    Write-Host "Launched $exe - look for the whale in the notification area."
}
