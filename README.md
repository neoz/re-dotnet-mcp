# re-dotnet

Stdio MCP server for .NET assembly inspection and IL patching, built on AsmResolver.

## Prerequisites

- **.NET 9 SDK** — https://dotnet.microsoft.com/download/dotnet/9.0

Verify:

```
dotnet --version    # expect 9.x
```

## Build

```
dotnet build
```

## Test

```
dotnet test
```

## Run

```
dotnet run --project src/ReDotnet.Server
```

The server speaks MCP over stdio; wire it into any MCP client. Example
Claude Desktop config:

```json
{
  "mcpServers": {
    "re-dotnet": {
      "command": "dotnet",
      "args": ["run", "--project", "<absolute-path>/src/ReDotnet.Server"]
    }
  }
}
```

CLI flags: `dotnet run --project src/ReDotnet.Server -- --help`.

## Release build (standalone binary)

Publish once, then run the resulting `re-dotnet` (or `re-dotnet.exe`) directly
— no `dotnet` invocation needed at runtime.

**Framework-dependent** (smaller; requires .NET 9 runtime on the host):

```
dotnet publish src/ReDotnet.Server -c Release -o publish
```

**Self-contained** (single bundle, no runtime required on the host):

```
# Windows x64
dotnet publish src/ReDotnet.Server -c Release -r win-x64 --self-contained -o publish

# Linux x64
dotnet publish src/ReDotnet.Server -c Release -r linux-x64 --self-contained -o publish

# macOS arm64
dotnet publish src/ReDotnet.Server -c Release -r osx-arm64 --self-contained -o publish
```

Run the published binary:

```
# Windows
publish\re-dotnet.exe --help

# Linux / macOS
./publish/re-dotnet --help
```

Wire the binary into an MCP client by replacing the `dotnet run` command:

```json
{
  "mcpServers": {
    "re-dotnet": {
      "command": "<absolute-path>/publish/re-dotnet.exe"
    }
  }
}
```

## Tools

`*` marks the 27 tools advertised by default; the rest are reachable via
`call` / `search_tools`. At runtime `get_schema <tool_name>` returns each
tool's parameter list.

### Lifecycle

| Tool | Description |
|---|---|
| `open_assembly` * | Load a .NET assembly (.dll/.exe/.netmodule) into a workspace. |
| `close_assembly` * | Evict the workspace for assembly_id. Optionally flush the sidecar first. |
| `list_assemblies` * | List currently open workspaces. |
| `get_assembly_info` * | Get metadata-level information for an open assembly. |

### Module & metadata

| Tool | Description |
|---|---|
| `get_module_info` | Module-level metadata: name, MVID, runtime/metadata versions, entry point token. |
| `list_streams` | List the .NET metadata streams (#~, #-, #Strings, #US, #GUID, #Blob, #Pdb) with sizes. |
| `list_sections` | List the PE sections of the assembly with RVA/size/characteristics. |
| `get_metadata_table_counts` | Get row counts for each metadata table. |
| `resolve_token` | Resolve an identifier (hex token, FQN, or @0xRVA) to a canonical member handle. |

### Types

| Tool | Description |
|---|---|
| `list_types` * | List type definitions with filter_regex/namespace/kind/visibility filters. |
| `get_type` * | Get detailed info for a type: base, interfaces, members, nested types, custom-attribute count. |
| `get_type_layout` | Explicit/sequential/auto layout for a type with field offsets. |
| `list_namespaces` | List distinct namespaces with their type counts. |

### Methods

| Tool | Description |
|---|---|
| `list_methods` * | List methods with filter_regex/type/modifier filters. |
| `get_method` * | Method detail: signature, attributes, PInvoke info, code size, locals, EHs. |
| `disassemble_method` * | IL listing for a method with offsets, opcodes, resolved operands. |
| `get_method_locals` | Local variable signatures for a method. |
| `get_method_exception_handlers` | try/catch/filter/finally regions for a method. |

### Members (fields / properties / events)

| Tool | Description |
|---|---|
| `list_fields` | List fields; optionally restrict to a single declaring type. |
| `get_field` | A field's signature, modifiers, and (if literal) constant value. |
| `get_field_rva_data` | Read the raw bytes of a field's mapped RVA initializer (e.g. Roslyn `<PrivateImplementationDetails>` byte-array literals). Returns hex preview + base64; capped at max_bytes. |
| `list_properties` | List properties; optionally restrict to a single declaring type. |
| `get_property` | A property's type and getter/setter method tokens. |
| `list_events` | List events; optionally restrict to a single declaring type. |
| `get_event` | An event's handler type and add/remove method tokens. |

### References & xrefs

| Tool | Description |
|---|---|
| `list_assembly_refs` * | AssemblyReferences with version, culture, public-key-token. |
| `list_module_refs` | ModuleReferences (typically populated by P/Invokes). |
| `list_type_refs` | External TypeReferences. |
| `list_member_refs` | External MemberReferences. |
| `get_xrefs_to` * | Find IL sites that reference the given type/method/field/string. |
| `get_xrefs_from` * | Every external token referenced inside one method's IL. |
| `get_call_graph` * | Callee tree rooted at the given method, up to depth 3. |

### Imports / exports

| Tool | Description |
|---|---|
| `list_pinvokes` * | All DllImport methods with target DLL, entry point, charset, calling convention. |
| `list_unmanaged_exports` | Methods exported via [DllExport]-style VTable fixups. |
| `list_type_forwarders` | TypeForwardedTo entries (exported types re-routed to another assembly). |

### Strings & search

| Tool | Description |
|---|---|
| `list_user_strings` * | Entries in the #US heap. |
| `list_ldstr_strings` | Strings referenced by ldstr IL operands, with the methods that reference each. |
| `find_code_by_string` * | Methods whose IL contains ldstr matching a regex. |
| `search_il` * | Regex over IL mnemonic + resolved operand text (per instruction). |
| `search_bytes` | Byte-pattern search inside one or all PE sections. |
| `find_dangerous_apis` * | Scan IL for calls matching dangerous-API rules (deserialization/process/reflection/crypto/network/file). |

### Resources & attributes

| Tool | Description |
|---|---|
| `list_resources` | Manifest resources (embedded or linked) with sizes. |
| `read_resource` | Read a manifest resource, capped at max_bytes; returns hex preview + auto-detected encoding. |
| `list_custom_attributes` | Custom attributes by attribute-type substring and/or owner token. |

### Sidecar reads

| Tool | Description |
|---|---|
| `get_comment` | The sidecar comment for a token, if any. |
| `list_bookmarks` | All bookmarks from the sidecar. |
| `get_color` | The sidecar color tag for a token, if any. |

### Mutations (sidecar)

| Tool | Description |
|---|---|
| `set_comment` | Set or clear a sidecar comment for a token. |
| `set_method_il_comment` | Set or clear an IL-offset-scoped comment inside a method. |
| `set_bookmark` | Add a bookmark targeting a token. |
| `delete_bookmark` | Remove a bookmark by slot. |
| `set_color` | Set or clear a sidecar color tag for a token (rrggbb). |

### Mutations (assembly)

| Tool | Description |
|---|---|
| `rename_type` * | Rename a type. Updates the TypeDef row; cross-module refs are unaffected. |
| `rename_method` * | Rename a method. |
| `rename_field` | Rename a field. |
| `rename_parameter` | Rename a method parameter. |
| `rename_local` | Rename a local variable (sidecar; PortablePdb writeback v1.1+). |
| `patch_il` * | Replace IL at offset with assembled new_il (byte-size-preserving; pad with nops). |
| `nop_il_range` | Replace IL instructions in [start, end) with nops preserving length. |
| `replace_method_body` | Replace the entire IL body of a method with assembled il_text. |
| `undo` | Pop and revert the most recent mutation for this assembly. |
| `redo` | Re-apply the most recently undone mutation. |

### Save

| Tool | Description |
|---|---|
| `save_assembly` * | Write the assembly to disk. Default output: `<stem>.patched.<ext>`. |
| `save_assembly_as` | Alias of save_assembly with an explicit required path. |
| `save_sidecar_only` | Flush the sidecar JSON without writing the assembly. |

### Meta

| Tool | Description |
|---|---|
| `search_tools` * | Search the full catalog, including tools hidden from the pinned set. |
| `get_schema` * | Full schema (name, description, parameters) for a tool by name. |
| `call` * | Invoke any tool by name with arguments as a JSON object string. |
| `batch` * | Invoke several tools in sequence; input is `[{tool_name, arguments_json}, ...]`. |
| `execute` * | Like `batch` but one `tool_name <json-args>` per line. |

Design and full tool spec: [`docs/PRD.md`](docs/PRD.md), [`docs/PLAN.md`](docs/PLAN.md).
License: [MIT](LICENSE).
