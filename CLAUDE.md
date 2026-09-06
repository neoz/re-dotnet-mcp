# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

`re-dotnet` is an MCP (Model Context Protocol) server in .NET 9 that wraps
AsmResolver 6.0.1 to expose .NET assembly inspection + IL patching to LLM
clients over stdio. The design is fully spec'd in `docs/PRD.md` and
`docs/PLAN.md` — those two are the source of truth for scope and decisions
already made (identifier forms, sidecar shape, strong-name strategies,
dangerous-API rules, etc.). Read them before proposing structural changes.

## Build, test, run

```
dotnet restore
dotnet build
dotnet test                                                 # all tests
dotnet test --filter FullyQualifiedName~IlAssemblerTests    # single class
dotnet test --filter DisplayName~Branch_with_label          # single test
dotnet run --project src/ReDotnet.Server -- --help          # CLI help
dotnet run --project src/ReDotnet.Server                    # stdio MCP server
```

There is no CI in this repo (deliberate, per PLAN). Central package versions
live in `Directory.Packages.props`; do not pin versions in individual csprojs.
Both `Nullable` and `ImplicitUsings` are enabled globally via
`Directory.Build.props`.

## Three-project layout — why it's split this way

- `src/ReDotnet.Core/` — pure library. All AsmResolver usage, sidecar I/O, IL
  assembler, mutation log live here. Unit-testable without spinning the MCP
  host. **Keep AsmResolver imports out of `ReDotnet.Server`.**
- `src/ReDotnet.Server/` — thin MCP adapter. Each `Tools/*Tools.cs` class
  takes Core services in its constructor and shapes return DTOs for the wire.
  If a tool grows logic beyond "call Core service, wrap result," that logic
  belongs in Core.
- `tests/ReDotnet.Tests/` — xUnit. Targets Core directly for almost
  everything; only `Meta/ToolCatalogTests.cs` reaches into Server reflection.

## Architectural load-bearing pieces

These are the abstractions you will touch often. Read these before adding
features:

- **`WorkspaceRegistry`** keys open assemblies by `assembly_id` (filename
  stem). Re-opening the same path returns the existing workspace; opening a
  different path with a colliding stem appends a numeric suffix. Every tool
  begins with `_registry.Get(id)`.
- **`TokenResolver`** is the single entry point for the four PRD identifier
  forms (hex token, `@0x` RVA, `{module,token}`, FQN like
  `Ns.Type::Method(Sig)`). When ambiguous it throws `BackendError.Ambiguous`
  with candidate tokens — callers should not pre-disambiguate.
- **`Page<T>` + `BackendError`** are the envelope contract. New list-returning
  tools take `offset`/`limit` and return `Page<T>` (then the Server wraps it
  in `PageDto<T>` for snake-case wire shape). Errors that should surface as
  structured MCP `isError=true` must be `BackendError`, not generic exceptions.
- **Mutation log + undo/redo.** Every mutation tool records a
  `MutationLogEntry` with `Kind`/`Old`/`New`. Rename/IL-patch undo is best-
  effort because reverting the live AsmResolver tree is mutation-specific —
  sidecar mutations have full undo, code mutations expose the old value so the
  agent can re-invoke. Don't add a new mutation kind without updating
  `UndoRedoService` if you can revert it cleanly.
- **IL assembler (`Core/Il/IlAssembler.cs`).** Lex→parse→encode pipeline. Two
  documented limitations to know before extending it:
  - External member operands take **hex tokens only** today (e.g.
    `call 0x0A000005`). Full `[mscorlib]Type::Method(Sig)` parsing is
    deferred to v1.1; the assembler surfaces a structured error pointing
    callers at the hex form.
  - `patch_il` requires **byte-size-preserving** patches; analysts pad with
    `nop` or use `replace_method_body` for size changes.
- **Sidecar = JSON, atomic-rename writes.** Schema is `SidecarDocument` with a
  `version` field. Never break the v1 shape without bumping `version` and
  adding migration in `SidecarStore.LoadOrCreate`.
- **Strong-name strategies.** `SaveService` enforces the PRD 11.3 contract:
  default `preserve` *fails* on signed inputs rather than silently stripping.
  Adding a new strategy means extending the `switch` in `SaveAssembly` and
  documenting it in PRD §11.3.

## Test conventions

- The **synthetic corpus** (`tests/.../Corpus/SyntheticCorpusFixture.cs`)
  compiles `Corpus/Fixtures/*.cs` via Roslyn at test init using
  `TRUSTED_PLATFORM_ASSEMBLIES` as the reference set. To add a new fixture
  shape, drop a new `.cs` in `Corpus/Fixtures/` — it is auto-discovered and
  exposed via `corpus.Get("<stem>")`. The csproj copies these files to the
  test output via `<None Include="Corpus\Fixtures\*.cs" CopyToOutputDirectory>`.
- Test classes that load corpus fixtures use `[Collection("SyntheticCorpus")]`
  and take `SyntheticCorpusFixture` via constructor injection.
- `tests/.../RoundTrip/` owns the patch→save→reload property. This is the
  gate for PRD §8's ">=95% round-trip success" metric; new write tools
  should land with a round-trip test, not just an in-memory mutation test.

## Tool registration + meta-tools

Tools are MCP-registered via `[McpServerToolType]` on the class and
`[McpServerTool(Name = "snake_case")]` on each method, then wired in
`Program.cs` with `.WithTools<T>()`. Tool names must stay snake_case for
re-mcp parity (PRD §G3). When you add a tool:

1. Implement the logic in a Core service.
2. Add a thin wrapper method in the appropriate `Server/Tools/*Tools.cs`
   class (or a new class if it's a new family).
3. If it should be advertised by default, add it to
   `Server/MetaTools/PinningRegistry.Pinned`. `ToolCatalogTests` will fail if
   a pinned name is missing from the discovered catalog.
4. The `call`, `batch`, `execute` meta-tools reflect over the assembly to
   invoke any registered tool by name — no separate registration needed.

## Things that are deferred (don't pull forward unprompted)

PLAN §10 lists the explicit "do not pull forward" set for v1.0: P1/P2 features
(CFG, protector detect, diff, snapshots, etc.), CI/CD, C# decompilation, HTTP
transports, telemetry. If you find a TODO that hints at one of these, leave it.
