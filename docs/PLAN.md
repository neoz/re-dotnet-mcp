# PLAN: re-dotnet implementation (M0 - M3)

Companion to [PRD.md](./PRD.md). Covers full v1.0 GA (M0 - M3). M4 / P1 / P2 deferred.

**Last updated:** 2026-05-17

---

## 1. Overview

Build a stdio MCP server in .NET 9 that wraps **AsmResolver** to expose .NET assembly inspection and IL patching as tools for LLM clients. Greenfield repo at `D:\working\dotnet\re-dotnet-mcp`.

Confirmed inputs from planning:

| Decision | Choice |
|---|---|
| Plan scope | Full v1.0 (M0 - M3) |
| Solution layout | Multi-project: Core + Server + Tests |
| MCP SDK | `ModelContextProtocol` (official) |
| Test framework | xUnit |
| Test corpus | Synthetic, built at test time |
| IL parser | Dedicated design section in this doc |
| CI | Not in this plan |

---

## 2. Solution layout

```
re-dotnet-mcp/
  ReDotnet.sln
  src/
    ReDotnet.Core/                # Pure library: AsmResolver wrappers, sidecar, IL parser, mutation log
      Workspace/                  # Loaded modules, workspace registry, undo/redo stack
      Inspection/                 # Read-only services (types, methods, strings, xrefs, ...)
      Mutation/                   # Renames, IL patches, mutation log entries
      Il/                         # IL parser + serializer (ilasm subset)
      Sidecar/                    # .redotnet/<stem>.json read/write, schema versioning
      Search/                     # search_il, search_bytes, find_dangerous_apis rule loader
      Resources/                  # Resource extraction + auto-detect
      Save/                       # save_assembly orchestration, strong-name strategies
    ReDotnet.Server/              # Console app: MCP stdio host, tool classes, pinning
      Program.cs                  # entry point, CLI args (--search-paths, --log-file, --rules)
      Tools/                      # one class per tool family (LifecycleTools, TypeTools, ...)
      MetaTools/                  # search_tools, get_schema, call, batch, execute
      Envelope/                   # Pagination, BackendError, response shaping helpers
  tests/
    ReDotnet.Tests/               # xUnit tests against Core
      Corpus/                     # SyntheticCorpusFixture: compiles C# fixtures at test-init
      Fixtures/                   # *.cs source for the synthetic corpus
      Inspection/                 # tests per Core area
      Mutation/
      Il/
      RoundTrip/                  # patch -> save -> reload -> assert
  docs/
    PRD.md
    PLAN.md
  README.md                       # (created at M3, not before)
  LICENSE                         # MIT, dropped in at M0
  .gitignore
```

**Why three projects, not one:** Core is unit-testable without spinning the MCP host. Server is a thin adapter that maps MCP tool calls to Core APIs and shapes the envelope. Tests target Core directly; only a small slice exercises the Server end-to-end over a piped stdio pair.

**Why not a separate `Tools` assembly:** premature. We can move tools out of Server later if it grows past one class per area without churn.

---

## 3. Dependencies

| Package | Used by | Notes |
|---|---|---|
| `AsmResolver` (>= 6.x) | Core | PE, CIL, metadata, signatures |
| `AsmResolver.DotNet` | Core | Module/Assembly definitions |
| `AsmResolver.PE.File` | Core | Sections, raw bytes |
| `AsmResolver.Symbols.Pdb.Portable` | Core | Embedded PortablePdb reads (M2/M3) |
| `ModelContextProtocol` | Server | Official C# SDK, stdio transport |
| `System.Text.Json` | Core, Server | Sidecar + envelopes (BCL, no add) |
| `Microsoft.Extensions.Hosting` | Server | Lifetime + DI, recommended by MCP SDK |
| `Microsoft.Extensions.Logging.Console` | Server | `--log-file` writer |
| `YamlDotNet` | Core | `dangerous_apis.yaml` user rule override |
| `xunit`, `xunit.runner.visualstudio` | Tests | |
| `Microsoft.CodeAnalysis.CSharp` | Tests | Compile synthetic corpus fixtures at test init |

**.NET 9 SDK** required (per PRD). Both projects target `net9.0`. Core has no Windows-only deps; Server inherits the same (stdio is portable).

---

## 4. Architectural decisions

### 4.1 Workspace registry

A single `WorkspaceRegistry` DI-scoped service holds all open assemblies, keyed by `assembly_id` (filename stem). Each `Workspace` entry contains:

- `ModuleDefinition` (AsmResolver)
- Loaded `Sidecar` (lazy-loaded from `.redotnet/<stem>.json`)
- `MutationLog` (in-memory, for undo/redo)
- Original file path + `OpenAt` timestamp

Tools take `assembly_id` and look up the workspace. `open_assembly` returns the id; `close_assembly(save=False)` evicts. Server lifetime is the process; no cross-session persistence beyond the sidecar.

**Conflict resolution:** if the same path is opened twice with different `assembly_id`, refuse. If a second module with the same stem is opened, append a numeric suffix and return the actual id used.

### 4.2 Tool registration

Use the SDK's attribute-based registration (`[McpServerTool]`, `[Description]`). One static or scoped class per tool family. Pinned tools (PRD section 7) are marked; non-pinned tools are filtered from the advertised list at startup but remain reachable via the `call` meta-tool.

**Pinning mechanism:** wrap the SDK's tool collection with a filter at host build time. The `search_tools` meta-tool enumerates the full set (pinned + hidden) using metadata captured at registration.

### 4.3 Identifier resolution

A single `TokenResolver` service accepts the four identifier forms from PRD section 5.1 and returns a canonical `MetadataMember` handle. Order:

1. `{module,token}` object - exact match.
2. Hex string starting with `0x` - parse as MetadataToken, validate table byte.
3. String starting with `@0x` - RVA lookup via PE section table.
4. Otherwise - fully-qualified name parser: `Namespace.Type::Method(Sig)`. Signature optional if unambiguous; if ambiguous, return a structured "ambiguous" error listing all candidates with their tokens.

All inspection responses return both `token` and `name` so the agent can pick.

### 4.4 Response envelope and pagination

A `Page<T>` record `{ items, offset, limit, total, has_more }` matching re-mcp exactly. Default `limit=100`, max `1000`. Tools that return lists accept `offset` + `limit`.

Errors: `BackendError` exception type carrying `{ code, message, detail }`. The MCP host converts to `isError=true` with the structured detail in the content.

### 4.5 Mutations and undo / redo

Every mutation tool creates a `MutationLogEntry` capturing:

- Kind (rename, il_patch, comment, color, bookmark, ...)
- Target token / scope
- `old` state (snapshot of what changed)
- `new` state
- Timestamp

The log is a stack with a redo stack mirror. `undo` pops, reverses, and pushes to redo. `redo` does the inverse. `save_assembly` does **not** clear the undo stack (PRD M3 says sidecar persistence at GA; in-process undo is enough).

Mutation tools always return `old_*` fields so the agent has the original even without consulting the log.

### 4.6 Save semantics

`save_assembly` orchestrates:

1. Validate `strong_name_strategy` against module's current SN state.
2. For `preserve` on a SN-signed module, throw `BackendError(code="strong-name-required", detail=<options>)`.
3. For `resign`, load the `.snk`, set AsmResolver's `StrongNamePrivateKey`.
4. For `strip`, clear the SN directory.
5. For `force-invalid`, keep SN header but skip rehash.
6. Determine output path: `path` param, else `<stem>.patched.<ext>` (never overwrite source by default).
7. Call `module.Write(outputPath)`.
8. Return `{ output_path, bytes_written, sn_strategy_applied }`.

`save_sidecar_only` skips the module write and just flushes the sidecar JSON.

### 4.7 Sidecar persistence

Single JSON file per assembly per PRD section 11.2 schema. `SidecarStore` is responsible for atomic-rename writes (write to `.tmp`, rename over target) and `version` migration. Schema is owned by Core; Server doesn't touch it directly.

`module_mvid` is recorded on first save and validated on subsequent loads - a MVID mismatch on an existing sidecar returns a warning, not an error (analyst may have intentionally swapped the binary).

### 4.8 AssemblyRef resolution

PRD section 11.5: opt-in. Wire `ModuleResolver` to AsmResolver's `NetCoreAssemblyResolver` configured with `--search-paths` (CLI) or `REDOTNET_SEARCH_PATHS` (env). When unset, fall back to AsmResolver's default `DefaultMetadataResolver` with empty search paths - unresolved refs surface as the literal AssemblyRef row.

### 4.9 Dangerous-APIs rule engine

Bundled rules ship as embedded JSON resources in Core (one resource per preset: deserialization, process, reflection, crypto, network, file). At startup the Server loads `~/.redotnet/dangerous_apis.yaml` (or `$REDOTNET_RULES`) and merges by rule name - user entries override bundled ones; new names extend.

Rule shape:
```yaml
- id: binary-formatter-deserialize
  preset: deserialization
  match:
    type: System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
    method: Deserialize
  severity: high
  rationale: "BinaryFormatter is unsafe across all .NET versions"
```

Matching scans every method's IL for `call`/`callvirt`/`newobj` operands resolving to a member matching the rule. Linear scan; cache per-method on demand.

---

## 5. IL parser design (dedicated)

This is the highest-risk component (PRD section 11.1: ~500 LOC custom parser). Detailed here because parser bugs surface as silent miscompiles - patches that "succeed" but produce wrong IL.

### 5.1 Scope

**In:** single-method body fragments. Opcode + operand + label. ECMA-335 short and long forms. All operand kinds AsmResolver's `CilOpCode` enumerates: `InlineNone`, `InlineI`, `InlineI8`, `InlineR`, `InlineString`, `InlineMethod`, `InlineField`, `InlineType`, `InlineTok`, `InlineSig`, `InlineSwitch`, `InlineVar`, `InlineArgument` (and short forms).

**Out:** `.assembly extern`, `.class`, `.method` declarations, exception-handler clauses (handled via separate `set_method_exception_handlers` in M2/M3), `.locals init` (handled separately by `replace_method_body`'s `locals` param).

### 5.2 Grammar (sketch)

```
body         := instruction+
instruction  := (label ':')? opcode operand? ('//' comment)?
label        := 'IL_' HEX{4} | identifier
opcode       := identifier         // "ldarg.0", "brtrue.s", etc.
operand      := int_lit
              | hex_lit            // 0x06000123 - method/field/type token
              | float_lit
              | string_lit         // "foo\nÿ" - C-style escapes
              | label              // for branch targets
              | type_ref           // "[mscorlib]System.String"
              | method_ref         // "instance void [mscorlib]System.Object::.ctor()"
              | field_ref          // "string [mscorlib]System.String::Empty"
              | switch_list        // '(' label (',' label)* ')'
              | var_ref            // local index or name
              | arg_ref            // argument index or name
```

### 5.3 Pipeline

1. **Lex.** Newline-significant. Tokens: ident, hex, int, float, string, `:`, `,`, `(`, `)`, `[`, `]`, `::`, `//comment`.
2. **Parse.** One pass produces a list of `RawInstruction { label?, opcode, operand_text }`.
3. **Resolve operands.** Tokens resolved via `TokenResolver`; type/method/field refs parsed by a sub-parser that delegates to AsmResolver's `TypeNameParser` and member-signature parsing.
4. **Label pass.** Build `label -> instruction index` map; rewrite branch operands to indices. Unknown labels - structured error citing line number.
5. **Encode.** For each instruction, emit a `CilInstruction` using the AsmResolver opcode table. Short-form opcodes are auto-chosen when the operand fits (e.g., `ldc.i4.s` over `ldc.i4` when value fits in sbyte) - or trust the user's explicit choice if they wrote `brtrue` vs `brtrue.s`.
6. **Validate.** Run AsmResolver's `CilMethodBody.VerifyLabels()` and a max-stack pass (`CilMaxStackComputer`). Fail with structured error before mutating the live method body.

### 5.4 Error model

Every parser error carries `{ line, column, snippet, expected, found }`. Errors are batched up to 20 per parse so the agent gets the full picture in one round-trip, not one error per call.

### 5.5 Tests

Round-trip property: for every instruction in every method of every corpus assembly, `disassemble_method` followed by parsing must yield byte-equivalent IL. This is the floor; any divergence is a parser bug.

---

## 6. Milestone breakdown

### M0 - Skeleton (2 weeks)

**Goal:** stdio server boots, four read tools work, one synthetic corpus assembly loads end-to-end.

Tasks:

1. Solution scaffold: `dotnet new sln`, three `dotnet new` projects per layout above.
2. Add packages (section 3) with explicit version pins. Add `LICENSE` (MIT) and `.gitignore`.
3. `WorkspaceRegistry`, `Workspace`, `TokenResolver` stubs in Core.
4. `Page<T>` and `BackendError` envelope plumbing.
5. Server `Program.cs`: parse `--log-file`, `--search-paths`, `--rules`; build MCP host on stdio.
6. Tools in M0 scope:
   - `open_assembly(path, keep_open=true, assembly_id=null)`
   - `close_assembly(id, save=false)`
   - `list_assemblies()`
   - `get_assembly_info(id)`
   - `list_types(filter_regex?, namespace?, kind?, visibility?, offset?, limit?)`
   - `list_methods(filter_regex?, type?, modifier?, offset?, limit?)`
7. Synthetic corpus fixture: `Corpus/SyntheticCorpusFixture` compiles `Fixtures/Clean.cs` (a couple of classes/methods/fields) via Roslyn at test init, writes to a temp dir, exposes its path to tests.
8. Tests:
   - Lifecycle: open / list / info / close round-trip.
   - `list_types` returns expected types, namespace filter, kind filter (class vs enum).
   - `list_methods` pagination correctness (boundary at limit, has_more).
   - Token resolution for hex form and FQN form.

Exit criteria: `dotnet test` green; `dotnet run --project src/ReDotnet.Server -- ` opens a stdio session and responds to `tools/list` and the six tool calls against the synthetic Clean.dll.

### M1 - Read-only complete (4 weeks)

**Goal:** every P0 read tool from PRD section 6.1 except renames and IL patches.

Tasks, grouped:

1. **Module / metadata:** `get_module_info`, `list_streams`, `list_sections`, `get_metadata_table_counts`, `resolve_token`.
2. **Types deep:** `get_type`, `get_type_layout`, `list_namespaces`.
3. **Methods deep:** `get_method`, `disassemble_method` (IL listing with offsets + resolved operands - feeds the parser's round-trip test), `get_method_locals`, `get_method_exception_handlers`.
4. **Fields / properties / events:** all five tools.
5. **Refs / xrefs:** `list_assembly_refs`, `list_module_refs`, `list_type_refs`, `list_member_refs`, `get_xrefs_to`, `get_xrefs_from`, `get_call_graph`.
   - Xrefs require an IL scan across all methods. Build an `XrefIndex` lazily on first xref call per workspace; invalidate on mutation. Cache key: workspace id + module timestamp.
6. **Imports / exports:** `list_pinvokes`, `list_unmanaged_exports`, `list_type_forwarders`.
7. **Strings / search:** `list_user_strings`, `list_ldstr_strings`, `find_code_by_string`, `search_il`, `search_bytes`, `find_dangerous_apis` (loads bundled rules, optionally merges user file).
8. **Resources / attributes:** `list_resources`, `read_resource`, `list_custom_attributes`.
9. **Comments / bookmarks / colors (sidecar reads):** `get_comment`, `list_bookmarks`, `get_color` (writes deferred to M2 since they exercise the mutation path).

Corpus additions: `Fixtures/WithPInvoke.cs`, `WithResources.cs`, `WithExceptionHandlers.cs`, `WithCustomAttrs.cs`, `WithGenerics.cs`.

Exit criteria: each tool above has at least one happy-path and one edge-case test (empty result, pagination boundary, missing target). Manual run against one real-world DLL (analyst's choice from PRD user-story samples) succeeds end-to-end.

### M2 - Write path (3 weeks)

**Goal:** renames, IL patches, save round-trip, mutation tracking.

Tasks:

1. **MutationLog** with undo/redo stack per workspace.
2. **Sidecar writes:** `set_comment`, `set_method_il_comment`, `set_bookmark`, `delete_bookmark`, `set_color`. Each enters the mutation log.
3. **Renames:**
   - `rename_type(token, new_name, new_namespace?)` - update TypeDef row; rewrite same-module TypeRef rows that pointed to it (none, since TypeRefs are external); rewrite `ldstr` operands only when the user explicitly opts in (default: off, since string-bound references are heuristic).
   - `rename_method`, `rename_field`, `rename_parameter`, `rename_local`. Local renames go to PortablePdb when present, else sidecar.
4. **IL parser** (section 5 above). Land standalone in Core with its own test suite *before* wiring to patch tools.
5. **IL patching:** `patch_il(method_token, offset, new_il)`, `nop_il_range`, `replace_method_body(method_token, il_text, locals?)`, `patch_bytes(rva, hex)`.
   - Each captures `old_il` (full method body before) for undo and for response.
6. **Save:** `save_assembly` with the four `strong_name_strategy` modes (section 4.6), `save_assembly_as` (alias with required `path`), `save_sidecar_only`.
7. **Undo / redo:** `undo()`, `redo()` - returns the entry that was reverted.
8. **Round-trip tests:**
   - Patch a method to flip `brfalse` to `brtrue` on the CTF fixture, save, reload the saved DLL into a fresh `Workspace`, assert the IL contains `brtrue`.
   - Rename a method, save, reload, assert new name.
   - Run the CLR loader on saved output (`AppDomain.Load` in a sub-process, or `dotnet path/to/saved.dll` for the .exe fixture). If load fails, fail the test.
   - Strong-name strategy matrix: signed fixture x [preserve (must throw), strip, resign with test .snk, force-invalid]. Each asserts the saved file's PE state matches expectation.

Corpus additions: `Fixtures/StrongNamed.cs` (compiled with `Microsoft.CodeAnalysis` AssemblyName + StrongNameKeyFile pointing at a test-only `.snk` generated in the fixture init), `Fixtures/CtfCrackme.cs` (a single method with a `brfalse` to flip).

Exit criteria: every mutation tool has a happy-path test that includes save + reload. Round-trip success rate (PRD section 8) measurable as `passing round-trip tests / total round-trip tests`.

### M3 - Hardening / v1.0 GA (2 weeks)

**Goal:** meta-tools, docs, polish, ship.

Tasks:

1. **Meta-tools** (PRD section 7): `search_tools`, `get_schema`, `call`, `batch`, `execute`. Test that hidden tools are not in `tools/list` but are reachable via `call`.
2. **Pinning filter:** confirm exactly the 24 tools from PRD section 7 are advertised when the server starts with defaults.
3. **CLI polish:** `--help`, `--version`, structured `--log-file` JSON lines, exit codes (0 normal, 2 bad args, 3 fatal).
4. **Error envelope hardening:** verify every Core throw path becomes a structured MCP error; add tests for malformed inputs (bad token, missing assembly id, regex syntax error, etc).
5. **README.md** with: install, three-tool quickstart, link to PRD, link to PLAN. (PRD-driven - this counts as requested.)
6. **Sample MCP client config:** snippet for Claude Desktop / Cline / Continue stdio entries (paste-ready).
7. **Manual acceptance pass:** run through every PRD user story (M1-M5, S1-S4, D1-D3, C1-C3) against the corpus + one real assembly. Document any gaps as v1.1 issues.

Exit criteria: all P0 tools shipped, manual user-story matrix passes, README + sample client configs in place. Tag `v1.0.0`.

---

## 7. Test strategy

**Unit tests** (Core) own correctness of inspection, mutation, parsing, sidecar. They run against synthetic fixtures only - no real binaries in the repo.

**Round-trip tests** (Tests/RoundTrip) own the patch-save-reload property. They are the gate for PRD section 8 success metric ">=95% patch round-trip success."

**Server tests** (a small slice in Tests/) spin a stdio pair (anonymous pipes) and exercise `tools/list` + a handful of tool calls. Bulk of behavior is verified at the Core layer.

**Synthetic corpus** is rebuilt on every `dotnet test` run by the `SyntheticCorpusFixture` collection fixture using Roslyn (`Microsoft.CodeAnalysis.CSharp`). One temp dir per test session, reused across tests. This is deterministic, reproducible across machines, and avoids licensing/malware concerns.

---

## 8. Open implementation questions

Smaller than the PRD's resolved ones; surface them as you implement, don't pre-commit.

1. **Pagination total cost.** Computing `total` for `list_methods` etc. requires a full enumeration even when only one page is returned. For 100k-method assemblies this is wasted work per call. Option: cache total per workspace per query signature; invalidate on mutation.
2. **Xref index build cost.** First xref call on a large assembly may take seconds. Decide: build eagerly on `open_assembly` (slow open, fast xrefs), lazily (fast open, slow first xref), or behind a `build_xref_index` explicit tool.
3. **`rename_*` blast radius display.** When `rename_method` would only update one TypeDef row (callers are external), we should still report `affected_refs: 0` so the agent knows. Same-module call-site rewriting needs the xref index - decide whether rename forces an xref build or no-ops same-module rewrites without one.
4. **PortablePdb writeback.** AsmResolver supports writing PortablePdb but the parameter/local name update path is non-obvious. Spike at start of M2.
5. **Multi-module assemblies.** PRD treats single-module as the main case; multi-module appears only in `{module,token}` identifier form. M0-M3 should load a multi-module input but tools may return single-module results. Verify and document scope at M3.

---

## 9. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| IL parser bugs cause silent miscompiles | Med | High | Round-trip property test from M2 day one; reject parse on ambiguity rather than guess |
| AsmResolver API changes between 6.x minors | Low | Med | Pin exact version in `Directory.Packages.props`; bump deliberately |
| Strong-name saving subtleties (delay-sign, public-key-only) break re-sign | Med | Med | Cover the four strategies in the corpus matrix; document delay-sign as "use force-invalid" |
| MCP SDK preview API churn | Med | Low | Encapsulate SDK touch-points in `Server/Envelope/`; rewrite Server adapter on bump, Core untouched |
| Xref index memory on 100MB+ assemblies | Low | Med | Counter-metric in PRD (<5% OOM); add `--max-index-mb` cap before GA if needed |
| `replace_method_body` locals re-computation gets the type signatures wrong | Med | Med | Require explicit `locals` array in v1; auto-infer is a v1.1 stretch |

---

## 10. Explicitly deferred (do not pull forward)

Out of scope for M0 - M3, listed here so they stay out:

- All PRD section 6.2 P1 features (CFG, protector detect, diff, snapshots, signatures export, etc.) - that is M4.
- All PRD section 6.3 P2 features.
- CI / CD pipeline (per planning answer).
- C# pseudocode decompilation (PRD non-goal).
- HTTP / SSE transports (v2).
- Telemetry (PRD section 11.6 - zero network).

---

## 11. Ordering rationale

M0 first because a working stdio loop with one read tool de-risks the SDK integration before we depend on it for everything.

M1 (read-only) before M2 (write) because users can adopt the server for triage workflows the moment M1 ships, and the read model is what M2's mutations validate against.

M2 lands the IL parser as its own deliverable before the patch tools depend on it, since parser bugs are silent miscompiles and easiest to find in isolation.

M3 is short on purpose - the meta-tools, error envelope hardening, and docs are mechanical once M0-M2 are sound. If anything slips, slip M4, not M3.
