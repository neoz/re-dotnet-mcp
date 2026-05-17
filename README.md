# re-dotnet

MCP server for inspecting and patching .NET assemblies. Built on AsmResolver, exposed
over stdio so any MCP client (Claude Code, Claude Desktop, Cline, Continue, ...) can
drive a .NET reverse-engineering session.

This is the M0-M3 GA cut of the design in [docs/PRD.md](docs/PRD.md) and
[docs/PLAN.md](docs/PLAN.md).

## Build

Requires the .NET 9 SDK.

```
dotnet build
dotnet test
```

## Run

```
dotnet run --project src/ReDotnet.Server -- --help
```

Or the built binary:

```
src/ReDotnet.Server/bin/Debug/net9.0/re-dotnet --help
```

## Three-tool quickstart

Once the server is wired into your MCP client (see snippets below), a typical flow is:

1. `open_assembly` with the file path to load a `.dll`/`.exe` into a workspace.
2. `list_methods` (or `list_types`, `list_pinvokes`, `find_dangerous_apis`) to navigate.
3. `disassemble_method` to read IL for a method of interest.

For patching: `patch_il` accepts an ilasm subset; `save_assembly` writes a patched
output with the `strong_name_strategy` of your choice.

## CLI flags

| Flag | Purpose |
|---|---|
| `--search-paths PATHS` | Probe paths for AssemblyRef resolution (`;` or `:` separated). Env var: `REDOTNET_SEARCH_PATHS`. |
| `--log-file PATH` | Write JSON-lines log to a file. Stdio is reserved for MCP. |
| `--rules PATH` | YAML file with `dangerous_apis` rule overrides. Env var: `REDOTNET_RULES`. |
| `--version`, `-V` | Print version and exit. |
| `--help`, `-h` | Print help and exit. |

## MCP client config snippets

### Claude Desktop / Claude Code (config JSON)

```json
{
  "mcpServers": {
    "re-dotnet": {
      "command": "dotnet",
      "args": [
        "run", "--project",
        "D:/working/dotnet/re-dotnet-mcp/src/ReDotnet.Server"
      ]
    }
  }
}
```

### Cline / Continue

Same shape — point `command` + `args` at the built `re-dotnet` executable.

## Tool surface

24 tools are pinned and advertised by default; the rest are reachable via the
`search_tools` / `get_schema` / `call` / `batch` / `execute` meta-tools.

See `get_schema` for the live signature of each tool, or the
[PRD section 6 catalog](docs/PRD.md) for the design specification.

## License

MIT. See [LICENSE](LICENSE).
