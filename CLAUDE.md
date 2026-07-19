# CLAUDE.md

Guidance for working in this repository (Roslyn analyzers and code fixes). Three
NuGet packages are published from here: `StyleSharp.Analyzers` (style, layout,
naming, documentation, readability), `PerformanceSharp.Analyzers` (runtime
performance: allocations, collections/LINQ usage, strings, concurrency/async,
faster API selection), and `SecuritySharp.Analyzers` (runtime security:
cryptography, transport, secrets, injection, serialization, web hardening, AI
input trust boundaries; `SES####`). The GitHub repo is `RoslynCommonAnalyzers`. Each package
has its own analyzer/code-fix/package/test/benchmark project family; the same
conventions apply to both.

## Build & test

```bash
# Run from src/

# Build / test the floor (Roslyn 4.8) — the default slot
dotnet build RoslynCommonAnalyzers.slnx -c Release
dotnet test  --project tests/StyleSharp.Analyzers.Tests/StyleSharp.Analyzers.Tests.csproj -c Release
dotnet test  --project tests/PerformanceSharp.Analyzers.Tests/PerformanceSharp.Analyzers.Tests.csproj -c Release

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

# Build a specific Roslyn slot (same -p:RoslynVersion switch for the PerformanceSharp projects)
dotnet build StyleSharp.Analyzers.CodeFixes/StyleSharp.Analyzers.CodeFixes.csproj -c Release -p:RoslynVersion=roslyn5.3

# Pack (builds every slot, emits one nupkg per package with all analyzers/dotnet/<slot>/cs folders)
dotnet pack StyleSharp.Analyzers.Package/StyleSharp.Analyzers.Packages.csproj -c Release
dotnet pack PerformanceSharp.Analyzers.Package/PerformanceSharp.Analyzers.Packages.csproj -c Release

# Benchmarks
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*"
dotnet run -c Release --project benchmarks/PerformanceSharp.Analyzers.Benchmarks -- --filter "*"
```

Tests use **TUnit** (Microsoft Testing Platform) and the
`Microsoft.CodeAnalysis.Testing` verifiers with `{|SSTxxxx:name|}` /
`{|PSHxxxx:name|}` markup.

## Conventions (follow these)

- **No suppressions by default.** Never use `#pragma warning disable`, `<NoWarn>`,
  `[SuppressMessage]`, or `.editorconfig` severity downgrades to silence a rule —
  fix the underlying issue. The one allowed exception is a
  `SuppressMessageAttribute` on a proven perf-motivated large `switch` statement
  when the switch is measurably better than the non-suppressed alternatives.
  Keep that exception narrow, document the justification inline, and do not use it
  for anything else. The repo builds its own source under `TreatWarningsAsErrors`
  with a strict analyzer set, including the benchmark project.

- **Repo layout:** repo metadata stays at the repository root, but build entry
  points live under `src/`. Run `dotnet` commands from `src/`; projects are
  grouped under `src/`, `tests/`, `benchmarks/`, and `tools/` inside that folder.

- **Performance / allocations first.** Analyzer callbacks run on every keystroke.
  Keep the no-diagnostic path allocation-free; compute suggested names only after
  a violation is found. See **[docs/PERFORMANCE.md](docs/PERFORMANCE.md)**.

- **No LINQ in production code.** Do not use LINQ anywhere under
  `src/StyleSharp.Analyzers/`, `src/StyleSharp.Analyzers.CodeFixes/`,
  `src/PerformanceSharp.Analyzers/`, or `src/PerformanceSharp.Analyzers.CodeFixes/`.
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

- **Avoid `DescendantNodes()` on analyzer/code-fix hot paths.** We have measured
  Roslyn's iterator-based descendant walks as a recurring perf problem in this
  repo. When a rule needs preorder descendant traversal, prefer a shared static
  helper over `ChildNodesAndTokens()` or a direct-member scan that avoids walking
  into irrelevant subtrees. Keep `DescendantNodes()` only when a benchmark shows
  no better alternative for that site; use
  `DescendantTraversalBenchmarks` in `StyleSharp.Analyzers.Benchmarks` when
  evaluating rewrites.

- **Roslyn syntax-list helpers are allowed.** `SyntaxTokenList.Any(SyntaxKind.X)` and
  similar Roslyn helpers are not LINQ. Do not rewrite them solely to satisfy the
  no-LINQ rule; only replace them when a real repeated-scan perf win is justified.

- **Static helpers, not base classes.** Shared logic lives in `internal static`
  helper classes operating on the passed-in model (`NamingHelper`,
  `ArgumentsOrParameterOnSameLineHelper`, `FieldClassification`,
  `NamingConventions`). No abstract analyzer base classes.

- **File naming and folders.** Source files are grouped into category subfolders
  mirroring the `*Rules.cs` descriptor classes (StyleSharp: `Spacing/`,
  `Readability/`, `Ordering/`, `Naming/`, `Maintainability/`, `Layout/`,
  `Documentation/`, `Extensions/`, `Records/`, `Concurrency/`, `Modernization/`,
  `CollectionExpressions/`, `ModernSyntax/`, `Design/`, `Correctness/`; PerformanceSharp: `Allocations/`,
  `Collections/`, `Strings/`, `Concurrency/`, `ApiSelection/`), with shared logic
  in `Helpers/` and descriptors in `Rules/`. Folders are organizational only — the
  namespace stays flat (`StyleSharp.Analyzers` / `PerformanceSharp.Analyzers` and
  their `.CodeFixes` twins); `the rule` is intentionally off. An analyzer (or code
  fix) that reports **exactly one** id is named `Sst<id><Concept>Analyzer` /
  `Psh<id><Concept>Analyzer` (e.g. `Sst1400AccessModifierAnalyzer`,
  `Psh1300PreferLockTypeAnalyzer`), and its code fix mirrors it
  (`Sst1400AccessModifierCodeFixProvider`) in the same folder — so the file name
  stays in sync with the type, and grepping the bare id lands on both. An analyzer
  that reports **multiple** ids keeps a descriptive name (`SpacingAnalyzer`,
  `MemberDocumentationAnalyzer`) for perf — bundling ids into one tree walk
  matters more than a 1:1 file map — and **must** enumerate every id it reports in
  its XML-doc summary/`<remarks>` so the id stays greppable. Shared code fixes
  that span ids (`NamingRenameCodeFixProvider`) also stay descriptive.

- **One shared code fix per family.** Naming rules all use
  `NamingRenameCodeFixProvider`; analyzers stash the suggested name in the
  diagnostic's `Properties[NamingDiagnostic.NewNameKey]`. Add new fixable naming
  ids to `NamingRules.AllFixableIds`.

- **Configuration is `.editorconfig` only — never a JSON file.** Options are read
  from the compiler's `AnalyzerConfigOptionsProvider` (no direct file I/O), using
  the CA-analyzer key convention: `stylesharp.<option>` /
  `performancesharp.<option>` (general, per package) and
  `stylesharp.<RuleId>.<option>` / `performancesharp.<RuleId>.<option>`
  (rule-specific override). See **[docs/CONFIGURATION.md](docs/CONFIGURATION.md)**.

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
`Sst1315UnionMemberNamingAnalyzer`, mirroring `SourceDocParserLib`) — and gate the whole
rule on the marker being present so it costs nothing otherwise. Use real 5.x APIs
behind `#if ROSLYN_5_OR_GREATER` only when structural probing won't do.

## Never suggest an API without proving it exists

A rule that says "use `X` instead" is wrong — not merely unhelpful, **wrong** — when
the analyzed project targets a framework that has no `X`. The user gets a diagnostic
they cannot fix, or a code fix that does not compile.

So: **any rule that suggests an API must resolve that API in the analyzed
compilation and stay silent when it is absent.** Probe it; never infer it from a TFM
string, a language version, or an assumption. Resolve it lazily, once, and gate the
whole rule on it so a project that cannot use the suggestion pays nothing.

This is not hypothetical. The overloads below all look universal and are not:

| Suggestion | First available in |
| --- | --- |
| `string.Contains(char)`, `StartsWith(char)`, `EndsWith(char)`, `IndexOf(char)` | .NET Core 2.0+ — **absent on netstandard2.0 and .NET Framework** |
| `string.Concat(ReadOnlySpan<char>, …)`, `Encoding.GetString(ReadOnlySpan<byte>)` | .NET Core 2.1+ |
| `Convert.ToHexString` | .NET 5+ (`ToHexStringLower` is .NET 9+) |
| `Random.Shared`, `DateTime.UnixEpoch`, `TimeProvider`, `CompositeFormat` | .NET 6 / Core 2.1 / .NET 8 respectively |
| `Enumerable.Order`, `FrozenDictionary`, `SearchValues`, `GetAlternateLookup` | .NET 7 / .NET 8 / .NET 9 |

Where the *syntax* rather than an API is the suggestion (collection expressions,
raw strings, `field`, primary constructors), gate on `LanguageVersion` the same way.

The same applies to a code fix: bind the rewritten call speculatively before
offering it, so a fix that would not compile is never offered.

## Diagnostic id schemes

Both packages use four-digit ids grouped by the hundreds digit; each group maps
1:1 to a `Rules/<Group>Rules.cs` descriptor class and a category subfolder.

### StyleSharp (`SST####`)

| Range | Group |
| --- | --- |
| `SST10xx` | Spacing |
| `SST11xx` | Readability — the "parameters/arguments must be on unique lines" family (one analyzer per syntax kind) lives at the **end** of the range, `SST1150`–`SST1171` |
| `SST12xx` | Ordering |
| `SST13xx` | Naming, **adapted to .NET runtime conventions** (e.g. SST1309 requires private fields to be `_camelCase`) |
| `SST14xx` | Maintainability. **This range is full** (only `SST1409` is unused; `SST1434` moved to PerformanceSharp and is never reused). New rules of this flavour go to `SST23xx` or `SST24xx`. |
| `SST15xx` | Layout |
| `SST16xx` | Documentation |
| `SST17xx` | Extensions (extension blocks/methods) |
| `SST18xx` | Records |
| `SST19xx` | Concurrency conventions (lock-target safety) |
| `SST20xx` | Modernization (throw helpers, patterns) |
| `SST21xx` | Collection expressions |
| `SST22xx` | Modern syntax |
| `SST23xx` | Design — the shape of a type's public surface: interface contracts (`IDisposable`, `IEquatable<T>`), operator and event conventions, what a member exposes |
| `SST24xx` | Correctness — code that compiles and runs but does not do what it says: mismatched argument order, a guard that runs too late, a reference to a member that is not there |

### PerformanceSharp (`PSH####`)

Rules whose primary motivation is the **runtime performance of the user's code**
live here, never in StyleSharp. A style rule that merely mentions perf stays in
StyleSharp; a perf rule that also reads nicely still belongs in PerformanceSharp.

| Range | Group | Examples of what belongs |
| --- | --- | --- |
| `PSH10xx` | Allocations & GC | closure/delegate allocations, `Array.Empty`, empty finalizers, boxing, struct copies |
| `PSH11xx` | Collections & enumeration | LINQ on hot paths, `Count()` vs `Count`, `TryGetValue`, double lookups, indexer over `First()`/`Last()` |
| `PSH12xx` | Strings & text | `StringComparison` without case-conversion allocations, char overloads, `StringBuilder` patterns, spans |
| `PSH13xx` | Concurrency & async | `System.Threading.Lock`, async overloads in async contexts, task combinators |
| `PSH14xx` | API selection | one-shot `HashData`, cached options/`SearchValues`, cheaper runtime-service APIs |

When a rule moves between packages it gets a **new id** in the destination and a
"Removed Rules" row (with a pointer) in the source package's
`AnalyzerReleases.Unshipped.md` — ids are never reused or shared across packages.

Adding a rule: descriptor in the group's `Rules` class (or inline), an analyzer,
tests, a `docs/rules/<ID>.md` page, and a row in that package's
`AnalyzerReleases.Unshipped.md` (RS2000). Configurable options go in
`.editorconfig` and `docs/CONFIGURATION.md`.
