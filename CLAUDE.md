# CLAUDE.md

Guidance for working in this repository (StyleSharp.Analyzers — Roslyn analyzers
and code fixes). The published NuGet package is `StyleSharp.Analyzers`; the GitHub
repo is `RoslynCommonAnalyzers`.

## Build & test

```bash
# Run from src/

# Build / test the floor (Roslyn 4.8) — the default slot
dotnet build StyleSharp.Analyzers.slnx -c Release
dotnet test  --project tests/StyleSharp.Analyzers.Tests/StyleSharp.Analyzers.Tests.csproj -c Release

# TUnit / Microsoft.Testing.Platform notes
# - `dotnet test` must still be run from src/ in this repo so the relative project paths resolve.
# - Runner-specific arguments must come after `--`.
# - For focused local runs, `dotnet run` is usually easier than `dotnet test` because TUnit
#   exposes its CLI flags directly there.
# - TUnit filtering uses tree-node filters, not VSTest `--filter` syntax:
#     dotnet run --project tests/StyleSharp.Analyzers.Tests/StyleSharp.Analyzers.Tests.csproj -c Release -- --treenode-filter "/*/*/MyTestClass/*"
#     dotnet test --project tests/StyleSharp.Analyzers.Tests/StyleSharp.Analyzers.Tests.csproj -c Release -- --treenode-filter "/*/*/*/MyTestMethod"
# - Tree-node filter pattern: `/Assembly/Namespace/Class/Method[Property=Value]`
# - Wildcards are supported with `*`, and OR within a segment uses `(A)|(B)`.

# Build a specific Roslyn slot
dotnet build StyleSharp.Analyzers.CodeFixes/StyleSharp.Analyzers.CodeFixes.csproj -c Release -p:RoslynVersion=roslyn5.3

# Pack (builds every slot, emits one nupkg with all analyzers/dotnet/<slot>/cs folders)
dotnet pack StyleSharp.Analyzers.Package/StyleSharp.Analyzers.Packages.csproj -c Release

# Benchmarks
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*"
```

Tests use **TUnit** (Microsoft Testing Platform) and the
`Microsoft.CodeAnalysis.Testing` verifiers with `{|SSTxxxx:name|}` markup.

## Conventions (follow these)

- **No suppressions by default.** Never use `#pragma warning disable`, `<NoWarn>`,
  `[SuppressMessage]`, or `.editorconfig` severity downgrades to silence a rule —
  fix the underlying issue. The one allowed exception is a
  `SuppressMessageAttribute` on a proven perf-motivated large `switch` statement
  when the switch is measurably better than the non-suppressed alternatives.
  Keep that exception narrow, document the justification inline, and do not use it
  for anything else. The repo builds its own source with the analyzer + the analyzer +
  the analyzer under `TreatWarningsAsErrors`, including the benchmark project.

- **Repo layout:** repo metadata stays at the repository root, but build entry
  points live under `src/`. Run `dotnet` commands from `src/`; projects are
  grouped under `src/`, `tests/`, `benchmarks/`, and `tools/` inside that folder.

- **Performance / allocations first.** Analyzer callbacks run on every keystroke.
  Keep the no-diagnostic path allocation-free; compute suggested names only after
  a violation is found. See **[docs/PERFORMANCE.md](docs/PERFORMANCE.md)**.

- **No LINQ in production code.** Do not use LINQ anywhere under
  `src/StyleSharp.Analyzers/` or `src/StyleSharp.Analyzers.CodeFixes/`.
  Even seemingly small query expressions add iterator, closure, and collection
  overhead that is too expensive on analyzer hot paths. Use explicit `for` /
  `foreach` loops and a few locals instead.

- **Keep the LINQ guardrail in place.** Production analyzer/code-fix projects remove
  the implicit `System.Linq` global using via `<Using Remove="System.Linq" />`.
  Preserve that guardrail so accidental LINQ usage fails at compile time.

- **Prefer concrete allocation shapes.** If the final size is known, prefer arrays
  over `List<T>`. If a mutable buffer is still required, give `List<T>`,
  `Dictionary<TKey, TValue>`, and `HashSet<T>` a sensible initial capacity instead
  of relying on default zero-capacity growth.

- **Avoid incidental materialization.** Do not introduce `ToList()`/`ToArray()` just
  to continue processing. Prefer a single-pass loop, or copy once into the exact
  concrete collection needed by the downstream API.

- **Hot path scans should be single-pass when practical.** Prefer one indexed scan
  over repeated rescans when the same syntax/token collection is being queried for
  multiple facts.

- **Roslyn syntax-list helpers are allowed.** `SyntaxTokenList.Any(SyntaxKind.X)` and
  similar Roslyn helpers are not LINQ. Do not rewrite them solely to satisfy the
  no-LINQ rule; only replace them when a real repeated-scan perf win is justified.

- **Static helpers, not base classes.** Shared logic lives in `internal static`
  helper classes operating on the passed-in model (`NamingHelper`,
  `ArgumentsOrParameterOnSameLineHelper`, `FieldClassification`,
  `NamingConventions`). No abstract analyzer base classes.

- **One shared code fix per family.** Naming rules all use
  `NamingRenameCodeFixProvider`; analyzers stash the suggested name in the
  diagnostic's `Properties[NamingDiagnostic.NewNameKey]`. Add new fixable naming
  ids to `NamingRules.AllFixableIds`.

- **Configuration is `.editorconfig` only — never a JSON file.** Options are read
  from the compiler's `AnalyzerConfigOptionsProvider` (no direct file I/O), using
  the CA-analyzer key convention: `stylesharp.<option>` (general) and
  `stylesharp.<RuleId>.<option>` (rule-specific override). See
  **[docs/CONFIGURATION.md](docs/CONFIGURATION.md)**.

- **Records on netstandard2.0** are enabled via `Polyfills/IsExternalInit.cs`
  (`#if !NET`), so value types are `readonly record struct` instead of
  hand-written `IEquatable<T>`.

## Multi-Roslyn targeting

The analyzer + code-fix assemblies build once per Roslyn **slot** and pack under
`analyzers/dotnet/<slot>/cs` so the SDK auto-loads the highest slot `<=` the host
compiler's `CompilerApiVersion`:

| Slot | Roslyn | Host |
| --- | --- | --- |
| `roslyn4.8` | 4.8.0 (floor) | .NET 8 SDK / VS 17.8 (C# 12) and .NET 9 |
| `roslyn4.14` | 4.14.0 | .NET 10 SDK / VS 17.14 (C# 14) |
| `roslyn5.3` | 5.3.0 | .NET 11 line (C# 15) |

Slot wiring lives in `src/Directory.Build.props` (`RoslynVersion` → package version +
`ROSLYN_*_OR_GREATER` constants + segregated `bin`/`obj`). Keep these assemblies
`netstandard2.0` (RS1041). Funnel all `ImmutableArray` creation through
`ImmutableArrays.Of(...)` — the 4.8 floor can't bind collection expressions for
`ImmutableArray` while 4.14+ requires them.

**For new C# 15 syntax** (union types, etc.) the current Roslyn does not yet
expose the syntax, so prefer **version-tolerant structural detection** — probe a
well-known type/interface/attribute by name (e.g. the `IUnion` marker in
`UnionMemberNamingAnalyzer`, mirroring `SourceDocParserLib`) — and gate the whole
rule on the marker being present so it costs nothing otherwise. Use real 5.x APIs
behind `#if ROSLYN_5_OR_GREATER` only when structural probing won't do.

## Diagnostic id scheme

- `SST11xx` — readability rules, mirroring the analyzer's `SA11xx` numbers. The
  "parameters/arguments must be on unique lines" family (one analyzer per syntax
  kind) lives at the **end** of the range, `SST1150`–`SST1171`, since the analyzer has
  no per-kind equivalent; they supersede the the analyzer `the rule`/`the rule` split-list
  rules, which are therefore not ported separately.
- `SST13xx` — naming rules, mirroring the analyzer's `SA13xx` numbers for
  discoverability, but **adapted to .NET runtime conventions** (e.g. SST1309
  requires private fields to be `_camelCase`, inverting the rule).

Adding a rule: descriptor in `NamingRules` (or inline), an analyzer, tests, a
`docs/rules/SST####.md` page, and a row in `AnalyzerReleases.Unshipped.md`
(RS2000). Configurable options go in `.editorconfig` and `docs/CONFIGURATION.md`.
