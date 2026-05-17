#!/usr/bin/env pwsh
# Build the CrackMe sample (kept out of ReDotnet.sln; build manually with this script).
#
# Output: samples/CrackMe.Console/bin/<Configuration>/net9.0/CrackMe.Console.{dll,exe}
#         and CrackMe.Plugin.dll alongside it.
#
# Usage:
#   ./build.ps1                # Debug build
#   ./build.ps1 -Configuration Release
#   ./build.ps1 -Run           # build then run the resulting exe

param(
    [string]$Configuration = "Debug",
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

Push-Location $here
try {
    dotnet build "CrackMe.Console\CrackMe.Console.csproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "build failed" }

    $exe = Join-Path $here "CrackMe.Console\bin\$Configuration\net9.0\CrackMe.Console.dll"
    Write-Host ""
    Write-Host "built: $exe" -ForegroundColor Green
    Write-Host "expected flag: FLAG{r3_d0tn3t_cr4ckm3_pwn3d_2026}" -ForegroundColor DarkGray

    if ($Run) {
        & dotnet $exe
    }
}
finally {
    Pop-Location
}
