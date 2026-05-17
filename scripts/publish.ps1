# Publishes the re-dotnet server for win-x64 and linux-x64.
#
# Usage:
#   .\scripts\publish.ps1                          # win-x64 + linux-x64, framework-dependent
#   .\scripts\publish.ps1 -SelfContained           # both, self-contained single-file
#   .\scripts\publish.ps1 -Runtime win-x64         # one runtime only
#   .\scripts\publish.ps1 -OutDir D:\dist          # override base output dir (default: .\publish)
#
# Output layout:
#   <OutDir>\<runtime>\re-dotnet[.exe]

[CmdletBinding()]
param(
    [string[]]$Runtime = @('win-x64', 'linux-x64'),
    [switch]$SelfContained,
    [string]$Configuration = 'Release',
    [string]$OutDir = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet not found on PATH. Install the .NET 9 SDK: https://dotnet.microsoft.com/download"
}

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$project  = Join-Path $repoRoot 'src\ReDotnet.Server'

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}
$OutDir = Resolve-Path $OutDir

$results = @()
foreach ($rid in $Runtime) {
    $target = Join-Path $OutDir $rid
    Write-Host "Publishing $rid -> $target" -ForegroundColor Cyan

    $args = @(
        'publish', $project,
        '-c', $Configuration,
        '-r', $rid,
        '-o', $target,
        '--nologo'
    )
    if ($SelfContained) {
        $args += '--self-contained'
        $args += '-p:PublishSingleFile=true'
    } else {
        $args += '--no-self-contained'
    }

    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid (exit $LASTEXITCODE)" }

    $exe = if ($rid.StartsWith('win')) { 're-dotnet.exe' } else { 're-dotnet' }
    $exePath = Join-Path $target $exe
    if (-not (Test-Path $exePath)) {
        Write-Warning "expected binary '$exePath' was not produced — check output above."
    }
    $results += [pscustomobject]@{ Runtime = $rid; Path = $exePath }
}

Write-Host ""
Write-Host "Published binaries:" -ForegroundColor Green
$results | Format-Table -AutoSize
