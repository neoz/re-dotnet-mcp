# Launches the MCP Inspector pointed at the re-dotnet server.
# Inspector docs: https://github.com/modelcontextprotocol/inspector
#
# Usage:
#   .\scripts\inspect.ps1                     # run via `dotnet run` (no prebuild needed)
#   .\scripts\inspect.ps1 -Published          # run the published binary in .\publish
#   .\scripts\inspect.ps1 -Args '--log-file','D:\tmp\re.log'
#
# Requires: Node.js (for npx) and the .NET 9 SDK (unless -Published with a
# self-contained build).
#
# Implementation note: npx cannot auto-resolve the bin of a scoped package
# when the bin name (`mcp-inspector`) does not match the unscoped package
# name (`inspector`). We pass `--package` + the explicit bin name and invoke
# `npx.cmd` directly to avoid PowerShell argument re-encoding through the
# default `npx` shim.

[CmdletBinding()]
param(
    [switch]$Published,
    [string]$PublishDir = "$PSScriptRoot\..\publish",
    [string[]]$Args = @()
)

$ErrorActionPreference = 'Stop'

# Resolve the .cmd shim explicitly. PowerShell's `npx` (no extension) lookup
# can pick the .ps1 shim, which has different arg-forwarding semantics.
$npxCmd = (Get-Command npx.cmd -ErrorAction SilentlyContinue)?.Source
if (-not $npxCmd) {
    $npxCmd = (Get-Command npx -ErrorAction SilentlyContinue)?.Source
}
if (-not $npxCmd) {
    throw "npx not found on PATH. Install Node.js: https://nodejs.org/"
}

$repoRoot = Resolve-Path "$PSScriptRoot\.."
$pkg      = '@modelcontextprotocol/inspector'
$bin      = 'mcp-inspector'

if ($Published) {
    $exe = Join-Path $PublishDir 're-dotnet.exe'
    if (-not (Test-Path $exe)) {
        $exe = Join-Path $PublishDir 're-dotnet'
    }
    if (-not (Test-Path $exe)) {
        throw "Published binary not found under '$PublishDir'. Run: dotnet publish src/ReDotnet.Server -c Release -o publish"
    }
    $exeFwd = $exe -replace '\\', '/'
    Write-Host "Inspector -> $exeFwd" -ForegroundColor Cyan
    & $npxCmd -y -p $pkg $bin $exeFwd @Args
}
else {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet not found on PATH. Install .NET 9 SDK or pass -Published with a self-contained build."
    }
    $project = Join-Path $repoRoot 'src\ReDotnet.Server'
    # The inspector spawns the server through cmd.exe, which eats unescaped
    # backslashes in argument values. dotnet accepts forward slashes natively.
    $projectFwd = $project -replace '\\', '/'
    Write-Host "Inspector -> dotnet run --project $projectFwd" -ForegroundColor Cyan
    # Build once up-front so the inspector handshake doesn't time out on the
    # first restore/compile.
    & dotnet build $project -c Debug --nologo -v quiet | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

    $dotnetArgs = @('run', '--project', $projectFwd, '--no-build', '--')
    if ($Args.Count -gt 0) { $dotnetArgs += $Args }
    & $npxCmd -y -p $pkg $bin dotnet @dotnetArgs
}
