# scripts/

PowerShell helpers for local development. Run from the repo root.

## `inspect.ps1` — launch the MCP Inspector against re-dotnet

Builds (if needed), then wires the server stdin/stdout into the
[MCP Inspector](https://github.com/modelcontextprotocol/inspector) UI on
http://localhost:6274.

```powershell
.\scripts\inspect.ps1                                 # via `dotnet run` (default)
.\scripts\inspect.ps1 -Published                      # via .\publish\<rid>\re-dotnet[.exe]
.\scripts\inspect.ps1 -PublishDir .\publish\win-x64   # point at a specific rid
.\scripts\inspect.ps1 -Args '--log-file','D:\tmp\re.log'   # extra server CLI flags
```

Requirements: **Node.js** (for `npx`) and **.NET 9 SDK** (unless `-Published`
with a self-contained build). The first run downloads
`@modelcontextprotocol/inspector` from npm; cached after that.

## `publish.ps1` — produce standalone binaries

Wraps `dotnet publish` for the common runtimes. Outputs land in
`publish\<rid>\re-dotnet[.exe]`.

```powershell
.\scripts\publish.ps1                          # win-x64 + linux-x64, framework-dependent
.\scripts\publish.ps1 -SelfContained           # both, single-file self-contained
.\scripts\publish.ps1 -Runtime win-x64         # one runtime only
.\scripts\publish.ps1 -OutDir D:\dist          # override base output dir
```

Framework-dependent output requires the .NET 9 runtime on the host running the
binary. Self-contained bundles the runtime (larger, no host dependency).

## Typical flow

```powershell
.\scripts\publish.ps1 -SelfContained -Runtime win-x64
.\scripts\inspect.ps1 -Published -PublishDir .\publish\win-x64
```
