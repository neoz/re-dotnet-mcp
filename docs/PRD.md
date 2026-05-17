# PRD: re-dotnet — Reverse-Engineering MCP Server for .NET Assemblies

**Author:** computer.angel@gmail.com
**Date:** 2026-05-17
**Status:** Draft v1
**Repo:** `D:\working\dotnet\re-dotnet-mcp` (greenfield)

## 1. Problem Statement

LLM-assisted reverse engineering has working tooling for native binaries (re-mcp covers IDA + Ghidra with ~150 tools) but **no equivalent for managed .NET assemblies**. Analysts working on .NET malware, packed loaders, third-party SDKs, and CTF crackmes currently bounce between dnSpyEx, ILSpy, and dotPeek manually — none of which expose a programmable interface an LLM can drive. The cost of this gap: every .NET investigation becomes a human-in-the-loop click-fest, agentic workflows stop at the PE boundary, and the rich metadata model that makes .NET *easier* to reverse than native code goes unused by AI tools.

## 2. Goals

| # | Goal | Measure |
|---|------|---------|
| G1 | Ship a stdio MCP server that exposes .NET assembly inspection + IL patching to any MCP client | Working server + 10 reference tool calls in Claude Code / Cline by GA |
| G2 | Cover the four primary user workflows (malware triage, pentest recon, 3rd-party lib reversal, CTF) without forcing users to drop into dnSpy | >=90% of common dnSpy navigation actions have a tool equivalent |
| G3 | Maintain conceptual parity with re-mcp's tool naming where it applies, so agents trained on re-mcp transfer cleanly | Shared tool names for `list_*`, `get_*`, `search_*`, `rename_*`, `patch_*` families |
| G4 | Read-write round-trip: load `Foo.dll` -> rename/patch -> save `Foo.patched.dll` that loads in CLR | End-to-end test: patch a method body, save, execute, observe behavior change |
| G5 | Token cost per investigation lower than equivalent native RE workflow | Median session uses <50k input tokens for typical 1-MB assembly triage |

## 3. Non-Goals

| Non-Goal | Why excluded |
|---|---|
| **C# pseudocode decompilation** | Pure AsmResolver scope. IL-only in v1. ILSpy/dnSpy integration deferred to v2 (avoids tying release schedule to ICSharpCode.Decompiler API churn). |
| **Automated deobfuscation** (ConfuserEx, .NET Reactor, Eazfuscator) | Best-effort load only. de4dot integration is its own product. We surface what AsmResolver can load. |
| **Dynamic analysis / debugging / emulation** | No CLR hosting, no ICorDebug, no Harmony hooks. Static analysis only. |
| **Native code analysis inside mixed-mode assemblies (C++/CLI)** | We expose that native sections exist but do not disassemble them — that's re-mcp's job. |
| **Single-file bundle extraction** (`dotnet publish --self-contained`) | v2 candidate. v1 requires user to pre-extract with `dotnet-bundle-extractor`. |
| **Source-based analysis** | We read assemblies, not `.cs`/`.csproj`. |
| **Symbol server / NuGet auto-fetch for AssemblyRefs** | Out of scope; users provide all referenced assemblies themselves. |
| **GUI** | MCP server only. Any UI is the client's responsibility. |

## 4. User Stories

### Malware analyst (Maya)
- **M1.** As a malware analyst, I want to list all P/Invokes in a sample so I can see what unmanaged surface it touches without scrolling through dnSpy.
- **M2.** As a malware analyst, I want to grep across the `#US` user-strings heap and IL `ldstr` operands so I can find suspicious URLs/keys in one pass.
- **M3.** As a malware analyst, I want to dump embedded `Resources` (manifest + linked) including encrypted blobs, so I can hand them to a sandbox.
- **M4.** As a malware analyst, I want to find all callers of `Activator.CreateInstance` / `Assembly.Load` / `Type.GetType` so I can locate the reflection-based loader stage.
- **M5.** As a malware analyst, I want to detect whether the assembly is protected (heuristic on stream order, weird type names, missing method bodies) so I know to reach for de4dot before continuing.

### Security researcher / pentester (Sam)
- **S1.** As a pentester, I want to enumerate all `public` types/methods (the attack surface) of a service DLL so I can pick targets.
- **S2.** As a pentester, I want to find all uses of dangerous APIs (`BinaryFormatter.Deserialize`, `XmlSerializer`, `LosFormatter`, `Process.Start`, `Sql*Command`) so I can hunt deserialization and injection sinks.
- **S3.** As a pentester, I want to dump all string literals that look like secrets (regex over `#US` and `ldstr`) so I can grep for keys / connection strings.
- **S4.** As a pentester, I want to see strong-name + Authenticode signing state so I know whether I can resign after patching.

### Developer reversing a 3rd-party library (Dev)
- **D1.** As a dev, I want to list a library's public API surface grouped by namespace so I can understand it before writing wrappers.
- **D2.** As a dev, I want to see TargetFramework, dependencies (AssemblyRefs with versions), and any `TypeForwardedTo` so I can plan compatibility.
- **D3.** As a dev, I want to read IL for a single method so I can understand a specific behavior without licensing concerns of full decompilation.

### CTF / crackme player (CTF)
- **C1.** As a CTF player, I want to patch a single IL instruction (e.g., `brfalse` -> `brtrue`) and save the assembly so I can flip a check without writing a dnSpy script.
- **C2.** As a CTF player, I want to rename obfuscated method/type names persistently across a session so I can build a mental map.
- **C3.** As a CTF player, I want to compute method MD5/SHA hashes to identify which methods stayed identical across two binary versions.
- **C4.** As a CTF player solving a multi-stage crackme, I want to recover a staged flag *without executing the binary* by chaining read-only tools — enumerating stage types via `list_types`, reading each `Probe` body with `disassemble_method`, decoding compiler-emitted byte literals (`<PrivateImplementationDetails>` blobs) via `get_field_rva_data`, extracting embedded ciphertext with `read_resource`, and recovering assembly-level keys via `list_custom_attributes` scoped to the assembly token. The reference sample is `samples/CrackMe.Console` (7 stages spanning literal-prefix, single-byte-XOR, anti-debug, reflection dispatch, per-index transform, resource+attribute key, and cross-assembly plugin invocation).

## 5. Architecture & Conventions

### 5.1 Identifier model

Unlike re-mcp's flat 64-bit `address` space, .NET has multiple identifier kinds. **All tools accept any of these**, with disambiguation rules:

| Form | Example | Use |
|---|---|---|
| Metadata token (hex) | `"0x06000123"` | Canonical. Table byte = type (0x02 TypeDef, 0x06 MethodDef, 0x04 FieldDef, etc.) |
| Fully-qualified name | `"Acme.Loader::DecryptPayload(System.Byte[])"` | Human-friendly. Signature optional if unambiguous. |
| Token+module | `{"module":"Foo.dll","token":"0x06000123"}` | Required only in multi-module assemblies. |
| RVA (hex) | `"@0x2050"` | Last resort, native interop. |

Tools return both `token` and `name` in every response so the agent can pick its preferred handle.

### 5.2 Workspace model

Equivalent to re-mcp's "database": a loaded `ModuleDefinition` (or `AssemblyDefinition` for multi-module) plus a **sidecar annotation store** (`.redotnet/<assembly>.json`) holding user comments, color tags, bookmarks, and pending renames before save.

`assembly_id` = stem of file path, same convention as re-mcp's `database`.

### 5.3 Save semantics

- **In-memory mutations** (rename, retype, comment, IL patch) accumulate on the loaded module.
- `save_assembly` writes via `ModuleDefinition.Write()`. Strong names re-signed if private key supplied.
- `save_assembly_as` writes to a new path (default behavior: `Foo.dll` -> `Foo.patched.dll`, never overwrites).
- Every mutation tool returns `old_*` fields (matching re-mcp's "old values" convention).

### 5.4 Pagination, errors, batching

Identical to re-mcp: `offset`/`limit`/`total`/`has_more`; `BackendError` -> `isError=True` with structured detail; `search_tools` / `get_schema` / `call` / `batch` / `execute` meta-tools.

### 5.5 Transport

stdio first (per the prereq). Server is a single .NET 9 console app using `ModelContextProtocol` C# SDK. SSE/HTTP transports are v2.

## 6. Requirements

### 6.1 Must-Have (P0) — v1.0

**Lifecycle**
- [ ] `open_assembly(path, keep_open=True, assembly_id=None)` — load `.dll`/`.exe`/`.netmodule` via `ModuleDefinition.FromFile`.
- [ ] `close_assembly(id, save=False)` / `save_assembly(id, path=None)` / `list_assemblies()` / `get_assembly_info(id)` — file path, runtime, target framework, machine, characteristics, strong-name state, Authenticode state, PDB presence, stream layout.

**Module & metadata**
- [ ] `get_module_info` — module name, MVID, runtime version, metadata version, EntryPoint token.
- [ ] `list_streams` — `#~` / `#-` / `#Strings` / `#US` / `#GUID` / `#Blob` / `#Pdb` with sizes.
- [ ] `list_sections` — PE sections (`.text`, `.rsrc`, `.reloc`) with RVA/size/characteristics.
- [ ] `get_metadata_table_counts` — row counts per metadata table.
- [ ] `resolve_token(token)` — token -> kind + canonical handle.

**Types**
- [ ] `list_types(filter_regex=None, namespace=None, kind=None, visibility=None)` — class/struct/interface/enum/delegate; paginated.
- [ ] `get_type(token_or_name)` — base type, interfaces, layout, fields, methods, properties, events, nested types, custom attributes.
- [ ] `get_type_layout` — explicit/sequential layout, packing, size, field offsets.
- [ ] `list_namespaces` — distinct namespaces + counts.

**Methods**
- [ ] `list_methods(filter_regex=None, type=None, modifier=None)` — static/instance, virtual/abstract, public/etc.; paginated.
- [ ] `get_method(token_or_name)` — signature, attributes, impl flags, P/Invoke info, RVA, code size, max-stack, exception handlers count, local count.
- [ ] `disassemble_method(token_or_name)` — IL listing with offsets, opcodes, resolved operands.
- [ ] `get_method_locals` — local variables with types and `pinned` flag.
- [ ] `get_method_exception_handlers` — try/catch/filter/finally ranges.

**Fields, properties, events**
- [ ] `list_fields` / `get_field`
- [ ] `get_field_rva_data(token, max_bytes=4096)` — read the raw initializer bytes of a field whose RVA points into the PE image. Covers Roslyn-emitted byte-array literals (`<PrivateImplementationDetails>` blobs lowered from `new byte[] { ... }` via `RuntimeHelpers.InitializeArray`), interop tables, and string-init constants. Returns hex preview + base64; capped at `max_bytes`. (Required by C4.)
- [ ] `list_properties` / `get_property` (with getter/setter method tokens)
- [ ] `list_events` / `get_event`

**References & xrefs**
- [ ] `list_assembly_refs(filter_regex=None)` — AssemblyRefs with version, culture, public key token, hash.
- [ ] `list_module_refs` — for P/Invokes.
- [ ] `list_type_refs(filter_regex=None)` — external types referenced.
- [ ] `list_member_refs(filter_regex=None)` — external method/field refs.
- [ ] `get_xrefs_to(token, kind=None)` — find all methods whose IL references this type/method/field/string. Paginated. Mirrors re-mcp `get_xrefs_to`.
- [ ] `get_xrefs_from(method_token)` — every external token referenced inside one method's IL.
- [ ] `get_call_graph(method_token, depth=1)` — direct callers/callees; depth 1-3.

**Imports / P/Invoke / exports**
- [ ] `list_pinvokes(module_filter=None)` — every `DllImport` with target DLL, entry point, calling convention, charset. (Maps M1.)
- [ ] `list_unmanaged_exports` — `[DllExport]`-style + VTable fixups.
- [ ] `list_type_forwarders`.

**Strings & search**
- [ ] `list_user_strings(filter_regex=None, min_length=4)` — `#US` heap. Paginated.
- [ ] `list_ldstr_strings(filter_regex=None)` — strings referenced by `ldstr` IL opcodes, with method-token list per string (combines re-mcp `get_strings` + `find_code_by_string`).
- [ ] `find_code_by_string(regex)` — convenience: returns methods whose IL `ldstr`s match.
- [ ] `search_il(pattern)` — regex over IL mnemonics + resolved operand text (re-mcp `search_text` analogue).
- [ ] `search_bytes(hex_pattern, section=None)` — byte search in PE.
- [ ] `find_dangerous_apis(set=None)` — preset bundles: `deserialization`, `process`, `reflection`, `crypto`, `network`, `file`. (Serves S2 + M4.)

**Resources & attributes**
- [ ] `list_resources` — manifest resources (embedded/linked) with size/offset.
- [ ] `read_resource(name, max_bytes=4096)` — extract bytes + auto-detect (string/PNG/PE/`.resources`).
- [ ] `list_custom_attributes(target_token=None, type_filter=None)` — by attribute type or applied target. **Scoping:** with `target` omitted the listing covers TypeDef/MethodDef/FieldDef/ParamDef rows; **assembly-scope attributes (token `0x20000001`)** — including `[assembly: ...]` declarations on `AssemblyDefinition` — are only returned when `target="0x20000001"` is passed explicitly. Callers hunting for assembly-level metadata (target framework, informational version, custom marker attributes carrying decryption material) must scope the call.

**Renaming (read-write)**
- [ ] `rename_type(token, new_name, new_namespace=None)` — updates TypeDef + all TypeRefs in-module + all string-bound IL references where safe.
- [ ] `rename_method(token, new_name)`
- [ ] `rename_field(token, new_name)`
- [ ] `rename_parameter(method_token, index, new_name)`
- [ ] `rename_local(method_token, index, new_name)` — written to PortablePdb if present, else sidecar.

**IL patching (read-write)**
- [ ] `patch_il(method_token, offset, new_instructions)` — accepts text IL ("`ldarg.0; brtrue.s L1`"). Validates max-stack. Returns `old_il`.
- [ ] `nop_il_range(method_token, start_offset, end_offset)` — replace with `nop`s preserving length.
- [ ] `replace_method_body(method_token, il_text)` — full body swap with re-computed locals.
- [ ] `patch_bytes(rva, hex)` — raw PE byte patch (re-mcp parity).

**Comments / bookmarks / colors (sidecar)**
- [ ] `set_comment(token, text)` / `get_comment(token)` / `set_method_il_comment(method_token, offset, text)`
- [ ] `set_bookmark(token, description)` / `list_bookmarks()` / `delete_bookmark`
- [ ] `set_color(token, rrggbb)` / `get_color(token)`

**Meta-tools** (re-mcp parity)
- [ ] `search_tools` / `get_schema` / `call` / `batch` / `execute` — keep tool surface manageable; only ~25 pinned tools visible, rest hidden behind search.

**Undo / save**
- [ ] `undo` / `redo` — stack-based; covers renames, IL patches, comments.
- [ ] `save_assembly(id, path=None, strong_name_key=None, deterministic=True)`
- [ ] `save_sidecar_only(id)` — persist annotations without touching the `.dll`.

### 6.2 Nice-to-Have (P1) — v1.1

- [ ] `get_method_il_cfg(method_token)` — basic blocks from IL (re-mcp `get_basic_blocks` analogue).
- [ ] `find_reflection_invokes` — locate `MethodInfo.Invoke`, `Activator.CreateInstance`, `Type.GetType` call sites with resolved literal arguments.
- [ ] `detect_protector` — heuristic ID for ConfuserEx / Eazfuscator / .NET Reactor / Babel / Themida-.NET. Returns name + confidence + signals. (Serves M5.)
- [ ] `compute_method_hash(method_token, algo="sha256")` — hash over normalized IL for cross-binary diffing.
- [ ] `diff_assemblies(id_a, id_b)` — types/methods added/removed/modified between two loaded assemblies.
- [ ] `get_authenticode_info` / `verify_strong_name`.
- [ ] `read_pdb_info` — embedded PortablePdb document list, source-link URLs.
- [ ] `list_security_attributes` — declarative CAS / `[Security*]`.
- [ ] `export_il(token, format="ilasm"|"json")` — round-trippable IL export.
- [ ] `export_csharp_signatures` — C#-style signatures (no bodies) for API surface docs. (Serves D1.)
- [ ] `take_snapshot` / `list_snapshots` / `restore_snapshot` — sidecar + module state.
- [ ] `set_pinvoke_info` — change DllImport target on a method.

### 6.3 Future Considerations (P2) — v2+

- C# pseudocode via ICSharpCode.Decompiler integration (decision: pluggable backend, not bundled).
- HTTP/SSE transports.
- Multi-module assembly editing with cross-module reference rewriting.
- Single-file bundle extraction (`Microsoft.NET.HostModel`).
- ReadyToRun / NGen image inspection.
- de4dot pre-pass integration.
- Embedded native code disassembly (delegate to re-mcp via cross-server orchestration).
- IL->IL transforms library (constant folding, dead-code removal) for cleaning obfuscated samples.

## 7. Tool Pinning Strategy

re-mcp pins ~30 tools visible to the client; rest are reached via `call`/`search_tools`. Proposed pinned set (24):

```
open_assembly, close_assembly, save_assembly, list_assemblies, get_assembly_info,
list_types, get_type, list_methods, get_method, disassemble_method,
get_xrefs_to, get_xrefs_from, get_call_graph,
list_pinvokes, list_assembly_refs, list_user_strings, find_code_by_string,
search_il, find_dangerous_apis,
rename_type, rename_method, patch_il,
search_tools, get_schema, call, batch, execute
```

All others (~100 estimated) reachable via `search_tools` / `call`.

## 8. Success Metrics

### Leading (first 30 days post-GA)
- **Install/run conversion:** >=40% of users who `git clone` reach a successful `open_assembly` call.
- **Tool call distribution:** at least 15 distinct tools used across the median session (validates the surface isn't overbuilt for a narrow path).
- **Patch round-trip success rate:** >=95% of `patch_il` -> `save_assembly` flows produce a CLR-loadable assembly. Measured by automated test corpus.
- **Token efficiency:** median p50 tokens-per-investigation < 50k for a 1-MB assembly (vs equivalent re-mcp baseline on a comparable native binary).

### Lagging (first 90 days)
- **Adoption:** >=500 GitHub stars, >=3 third-party MCP-client integration mentions (Cline, Continue, Claude Desktop configs in the wild).
- **CTF case studies:** >=5 public writeups using re-dotnet end-to-end.
- **Malware reports:** >=3 vendor/analyst blog posts where re-dotnet appears in the workflow.
- **Issue quality ratio:** bug-fix issues / feature-request issues <= 1:2 (signals stability, not chaos).

### Counter-metrics (watch for regressions)
- **Save-corruption rate:** any non-zero is a P0 incident.
- **OOM rate on >100 MB assemblies:** must stay below 5%.

## 9. Open Questions

| # | Question | Owner | Blocking? |
|---|---|---|---|
| ~~Q1~~ | ~~IL assembler input syntax for `patch_il`.~~ | — | **RESOLVED — see §11.1** |
| ~~Q2~~ | ~~Sidecar file format.~~ | — | **RESOLVED — see §11.2** |
| ~~Q3~~ | ~~Renames + strong-name signing on save.~~ | — | **RESOLVED — see §11.3** |
| ~~Q4~~ | ~~`dangerous_apis` rule set source.~~ | — | **RESOLVED — see §11.4** |
| ~~Q5~~ | ~~AssemblyRef auto-resolution.~~ | — | **RESOLVED — see §11.5** |
| ~~Q6~~ | ~~Telemetry.~~ | — | **RESOLVED — see §11.6** |
| ~~Q7~~ | ~~License.~~ | — | **RESOLVED — see §11.7** |
| ~~Q8~~ | ~~Project naming.~~ | — | **RESOLVED — see §11.8** |

All blocking open questions resolved.

## 10. Timeline & Phasing

**Hard dependencies:**
- AsmResolver >= 6.x (current stable; covers PE, CIL, metadata, signatures, PDB).
- `ModelContextProtocol` C# SDK >= current preview (stdio transport).
- .NET 9 SDK for build.

**No hard external deadlines.** Proposed cadence:

| Phase | Scope | Target |
|---|---|---|
| **M0 — Skeleton** (2 wks) | Repo scaffold, stdio server boots, `open_assembly` + `get_assembly_info` + `list_types` + `list_methods`. Test against 10-sample corpus. | T+2w |
| **M1 — Read-only complete** (4 wks) | All P0 read tools (sections 6.1 except renames + IL patches). End-to-end on malware sample, 3rd-party lib, CTF binary. | T+6w |
| **M2 — Write path** (3 wks) | Renames + IL patching + save round-trip. Patch-and-load test on full corpus. | T+9w |
| **M3 — Hardening / v1.0 GA** (2 wks) | Sidecar persistence, undo/redo, meta-tools, docs. | T+11w |
| **M4 — v1.1** (4 wks) | P1 features (CFG, protector detect, diff, snapshots, API export). | T+15w |

**Phasing rationale:** read-only ships before write so users can adopt for triage without trusting the patch path; write path lands once the read model is stable enough to validate "did the patch do what I meant."

## 11. Resolved Decisions

### 11.1 IL assembler input syntax for `patch_il` — **ilasm-compatible text**

**Decision.** `patch_il` accepts an ilasm-compatible text subset as its canonical `new_il` input. A structured-JSON alternative (`patch_il_struct`) is deferred to v1.1.

```
patch_il(
  method_token = "0x06000123",
  offset       = "IL_0020",
  new_il       = "ldarg.0\nbrtrue.s IL_0030"
)
```

**Rationale.**
- LLMs already speak ilasm (ECMA-335, ildasm output, dnSpy edit panes all use it). A custom JSON DSL would have zero prior exposure.
- Every .NET RE tool uses ilasm syntax — dnSpyEx, ildasm/ilasm, ILSpy. Tool transfer is free.
- Compact: one line of ilasm vs. ~5 lines of JSON per instruction.
- Labels (`IL_0020`, `brtrue.s L1`) work naturally; JSON would need a label table.
- AsmResolver provides the opcode table (`CilOpCodes`); a parser for the subset we need (opcode + simple operand + label, no `.assembly extern` / `.class` blocks) is ~500 LOC.

**Rejected alternatives.** Pure JSON (forces mental translation); dnlib-style programmatic API (not a text syntax, can't be MCP input).

**Trade-off accepted.** We own a small ilasm parser. Scoped to single-method bodies to keep it bounded.

### 11.2 Sidecar file format — **JSON, single file per assembly, schema-versioned**

**Decision.** Sidecars are stored at `./.redotnet/<assembly-stem>.json` with a `version` field for future migration.

```json
{
  "version": 1,
  "assembly": "Foo.dll",
  "module_mvid": "a1b2c3...",
  "annotations": {
    "0x06000123": {
      "rename": "DecryptPayload",
      "comment": "XORs body with key from #US row 4",
      "color": "ff8800"
    }
  },
  "il_comments": { "0x06000123": { "0x0020": "loop start" } },
  "bookmarks": [ { "slot": 0, "token": "0x06000123", "desc": "entry of stage 2" } ]
}
```

**Rationale.**
- Git-friendly: analysts share annotations via repos (CTF teams, malware-zoo collaborations). JSON diffs cleanly.
- Volume stays small. Sidecars hold metadata *about* assemblies, not content. A 10k-type assembly typically yields <1000 annotations (a few hundred KB), loading in <100 ms.
- Zero deps: `System.Text.Json` is in the BCL. SQLite would require `Microsoft.Data.Sqlite` + native lib bundling.
- Inspectable: `cat` / hand-edit / grep without a tool.
- `version` field is the escape hatch — if real workloads exceed ~10 MB, ship a v2 migrator to SQLite without breaking v1 users.

**Rejected alternatives.** SQLite (premature optimization); proprietary binary IDA-style .idb (tooling burden, opaque, no git story).

**Trade-off accepted.** Full rewrite on each save (no incremental writes). Revisit only if we hit the size ceiling above.

### 11.3 Renames + strong-name signing on save — **explicit consent, never silent strip**

**Decision.** `save_assembly` takes a `strong_name_strategy` parameter. Default refuses to silently break trust on strong-named inputs.

```
save_assembly(
  id = "Foo",
  strong_name_strategy = "preserve" | "strip" | "resign" | "force-invalid",
  strong_name_key      = "path/to/key.snk"   # required for "resign"
)
```

| Strategy | Behavior |
|---|---|
| `"preserve"` (**default**) | If the assembly was strong-named, **fail with a structured error** listing the four options. No surprises. |
| `"strip"` | Remove SN header + signature. Result loads but no longer claims an identity. Logs a warning. |
| `"resign"` | Re-sign with the provided `.snk`. Required when the user actually owns the key. |
| `"force-invalid"` | Keep the SN header with the now-incorrect hash. For CTFs / cracking where the loader has SN verification disabled. Logs a loud warning. |

**Rationale.**
- Strong names are a trust signal, even if weak. Silently stripping turns `Microsoft.Foo, PublicKeyToken=b03f5f7f11d50a3a` into "some random DLL" — breaks partial-trust loaders, GAC binding, and downstream consumers unexpectedly.
- CTF/cracking use cases are real — `force-invalid` is the explicit "I know this breaks signatures, I've handled it" lever.
- Mirrors AsmResolver's own design — `StrongNamePrivateKey` requires explicit opt-in.
- Error-on-default beats prompt-or-flag. MCP tools have no interactive prompt; refusing with a useful error is the right shape.

**Rejected alternatives.** Silent strip (footgun — users won't notice until prod loaders reject the patched DLL); refuse-all-SN-saves (kills the CTF/research use case entirely).

**Trade-off accepted.** One extra parameter that ~80% of users (non-SN assemblies) never see; they get a clean default-path save.

### 11.4 `dangerous_apis` rule set — **hybrid: bundled defaults + user override**

**Decision.** Built-in rule bundles ship with the server (`deserialization`, `process`, `reflection`, `crypto`, `network`, `file`). An optional user file at `~/.redotnet/dangerous_apis.yaml` (or `$REDOTNET_RULES` env var) merges into / overrides bundles by name.

**Rationale.**
- Works immediately for the 90% case — no setup.
- Security researchers iterate on rules faster than our release cadence; the override file is their escape hatch.
- Pattern matches semgrep, eslint, Bandit — defaults + user config is the established shape for security tooling.
- Bundled rules are version-pinned to the server release, preserving reproducibility when no override file is present.

**Rejected alternatives.** Built-in only (stales faster than malware evolves); external only (worse first-run UX).

### 11.5 AssemblyRef resolution — **manual by default, opt-in auto-resolve via flag**

**Decision.** `open_assembly(path)` loads only that file. Unresolved refs return as TypeRefs with `assembly_ref` + `name` fields. Auto-resolution opt-in:

```
re-dotnet --search-paths "C:\Refs;C:\Program Files\dotnet\shared\Microsoft.NETCore.App\9.0.0"
# or
REDOTNET_SEARCH_PATHS="..."
```

When set, AsmResolver's `ModuleResolver` chain lazily loads missing dependencies.

**Rationale.**
- Predictability over magic. Auto-pulling random `mscorlib.dll` from the GAC or framework cache leads to "which version did it actually pick?" reproducibility bugs — especially harmful in malware analysis where unresolved refs are themselves signal.
- Most RE workflows target a single sample; loading the full BCL is wasted memory and time.
- The dev-reversing-3rd-party-lib workflow (story D2) genuinely benefits, which is why the flag exists rather than being buried.
- A per-call `open_assembly(path, search_paths=[...])` parameter can be added in v1.1 without breaking the default.

**Rejected alternatives.** Auto-resolve by default (footgun for analysts); never auto-resolve (kills D2).

### 11.6 Telemetry — **none, in v1**

**Decision.** Zero network calls from the server. No usage counters, no update checks, no error reporting. Local-only logging via user-controlled `--log-file <path>`. Aggregate insight gathered only through opt-in GitHub issue templates that ask users to paste relevant tool-call summaries.

**Rationale.**
- Trust is the product. Users include malware analysts working on hostile samples and pentesters under NDA / air-gap engagements. Any network call:
  - Risks timing-correlation leakage (when did this sample get analyzed?).
  - Creates a sample-name exfiltration surface if scrubbing has bugs.
  - Forces users to firewall the tool, which kills updates anyway.
- Removes consent / privacy-policy / regional-compliance scope from v1.
- Default-off can become opt-in later; the reverse is much harder to retrofit credibly.

**Rejected alternatives.** Opt-out telemetry (incompatible with security-tool trust model); opt-in telemetry in v1 (infra cost without proven need).

### 11.7 License — **MIT**

**Decision.** MIT license for source code, samples, and docs.

**Rationale.**
- **AsmResolver is MIT.** Our core dep — aligning licenses removes friction for downstream users embedding both libraries.
- **MCP C# SDK is MIT.** No conflict.
- **.NET RE ecosystem leans MIT.** dnSpyEx (the fork of GPL-era dnSpy) is MIT; ILSpy is MIT. License matches where our users live.
- We hold no patents requiring Apache-2.0's grant; introducing patent clauses to an RE tool would be theater.
- Shorter text lowers the bar for forks and embedding, which matches the "be a building block" goal.

**Rejected alternatives.** Apache-2.0 (patent grant is overhead we don't need; less ecosystem alignment); GPL-family (locks out commercial RE tooling embedding, a key adoption channel).

### 11.8 Naming — **`re-dotnet` everywhere**

**Decision.**

| Surface | Name |
|---|---|
| GitHub repo | `re-dotnet` |
| CLI binary | `re-dotnet` |
| MCP server `name` field | `re-dotnet` |
| NuGet package | `ReDotnet` (PascalCase per NuGet convention) |

The local working directory `D:\working\dotnet\re-dotnet-mcp` stays as-is; the published name is `re-dotnet`.

**Rationale.**
- Direct extension of `re-mcp` naming — agents and humans familiar with the predecessor recognize the relationship instantly.
- One name everywhere (repo, CLI, server) — no `-mcp` suffix needed since MCP is implicit from context.
- Pre-flight check before reserving: confirm no popular `re-dotnet` package exists on NuGet / GitHub at the time of repo creation.

**Rejected alternatives.** `re-dotnet-mcp` (redundant suffix); `dotnet-mcp` (brand collision with dotnet CLI tooling); `dnRE-mcp` (cryptic, no mnemonic value).
