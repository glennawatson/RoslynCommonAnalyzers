# Design — Analyzer Performance Guide

**Date:** 2026-06-05
**Status:** Approved design, pending implementation plan
**Scope:** First task of a larger initiative (rebrand → performance foundation →
StyleCop port → C# 14/15 rules). This spec covers **only** the performance
foundation: a written guide plus a seed benchmark harness.

## Naming note (important)

The project rebrand is **not yet decided**. `SharpStyle` was rejected because
the package `SharpStyles` already exists on NuGet with non-trivial downloads.
**This task introduces no new brand name.** The guide is written
name-neutrally: it refers to "the project" / "the analyzer package" generically,
and where examples need real identifiers it uses the *current* ones
(`Blazor.Common.Analyzers`, diagnostic prefix `RCGS`) with an explicit note that
all names are provisional and will change in the dedicated rebrand task. The
seed benchmark project follows the current repo naming
(`Blazor.Common.Analyzers.Benchmarks`) so no soon-to-change brand is baked in; it
will be renamed wholesale alongside everything else during the rebrand.

## Problem & motivation

StyleCop analyzers have a reputation for being among the slowest analyzers in the
.NET ecosystem. This project intends to port and modernize much of the StyleCop
rule set, so it must establish performance discipline *before* the rule count
grows. Analyzers run on every keystroke in the IDE and on every build; an
allocation-per-node or LINQ-per-node pattern multiplied across dozens of rules
and millions of syntax nodes is the difference between a snappy and a sluggish
editing experience.

The current codebase already contains a representative anti-pattern. In
`Blazor.Common.Analyzers/ArgumentsOrParameterOnSameLineHelper.cs`, the
`Analyze<T>` method (the shared hot path behind most of the 22 existing rules)
does the following **on every parameter/argument list node in the compilation**:

```csharp
var diffChecker = new HashSet<int> { parameterLine };                          // heap alloc per node
var lineNumbers = list.Select(x => x.GetLocation().GetLineSpan().StartLinePosition.Line).ToList(); // closure + List alloc + LINQ
diffChecker.UnionWith(lineNumbers);
var allDifferent = diffChecker.Count == list.Count + 1;
```

That is three avoidable heap allocations (a `HashSet<int>`, a closure, a
`List<int>`) plus repeated `GetLocation().GetLineSpan()` calls per node, when the
same decision ("are all items on distinct lines, or all on one line?") can be
made allocation-free with a single manual pass over the list. This method is the
worked example the guide is built around.

## Goal

Make "benchmarked and allocation-disciplined" the default for every current and
future analyzer/code-fix, by delivering:

1. **`docs/PERFORMANCE.md`** — the authoritative performance doctrine.
2. **A seed BenchmarkDotNet harness** — proving the guide's claims with real
   numbers, including a before/after of the `Analyze<T>` method above.

The guide is modeled on two references in the user's other repos:
- **NuSourceDocs `BENCHMARKS.md`** — measurement rigor: a BenchmarkDotNet harness,
  a fixed hardware/runtime header convention, `[MemoryDiagnoser]` allocation
  columns, and reproduce instructions.
- **ReactiveUI.Binding.SourceGenerators `CLAUDE.md`** — analyzer-specific "What to
  Avoid" conventions: no `ISymbol`/`SyntaxNode` in cached state, no LINQ in hot
  paths, manual loops, `ConditionalWeakTable<Compilation, T>` symbol caching.

## Non-goals

- No rename / rebrand (separate task).
- No new analyzer rules, no StyleCop port, no C# 15 work.
- No change to the *behavior* of the existing 22 analyzers. The optimized
  `Analyze<T>` variant in the harness is a benchmark artifact for the
  before/after comparison; whether to land the optimization in the shipping
  analyzer is called out as a follow-up, not done here.
- No CI performance-regression gate yet (mentioned in the guide as future work).

## Deliverable 1 — `docs/PERFORMANCE.md`

A single Markdown document. Section outline:

1. **Why analyzer performance matters.** Keystroke-time and build-time execution;
   the StyleCop slowness reputation this project exists to fix; the multiplier
   effect (rules × nodes).

2. **Measure first.** How to benchmark in this repo:
   - The BenchmarkDotNet harness (Deliverable 2): how to run it, `ShortRunJob`
     rationale, `[MemoryDiagnoser]`.
   - The fixed **hardware/runtime header convention** (copied from NuSourceDocs):
     every results table states CPU, OS, .NET version, BDN version, job config.
   - `dotnet build /p:ReportAnalyzer=true` for per-analyzer wall-clock from the
     compiler itself, and a pointer to Roslyn's `AnalyzerRunner`.
   - Rule: **no performance claim without a number behind it.**

3. **Registration discipline.**
   - Register the narrowest action that works; prefer
     `RegisterSyntaxNodeAction` with explicit `SyntaxKind`s over tree/semantic
     actions.
   - `EnableConcurrentExecution()` and
     `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze)` (or
     `.None` where generated code should be skipped) — state the intended choice
     and why.
   - Use `RegisterCompilationStartAction` to resolve and cache per-compilation
     state (well-known symbols) once, instead of per node.

4. **Allocation discipline (the callback hot path).** The core of the guide.
   - No LINQ, no capturing closures, no per-node collection allocations
     (`HashSet`/`List`/arrays).
   - Manual `for`/`foreach` over `SeparatedSyntaxList<T>` (it has a struct
     enumerator — do not call `.Select`/`.ToList`).
   - Minimize `GetLocation()` / `GetLineSpan()`: compute line positions from the
     `SyntaxTree` once, or compare token line numbers directly.
   - Bail out cheaply: cheapest checks first (e.g. `list.Count <= 1`).
   - **Worked example:** the real `Analyze<T>` method, shown before and after,
     with the harness numbers from Deliverable 2 inline.

5. **Symbol & semantic-model discipline.**
   - Syntactic fast-path before any semantic query; only call
     `GetSymbolInfo`/`GetTypeInfo` when syntax can't decide.
   - Cache well-known symbols with `ConditionalWeakTable<Compilation, T>`; never
     repeat `GetTypeByMetadataName`.
   - Use `SymbolEqualityComparer.Default`; never put `ISymbol`/`SyntaxNode`/
     `Location` into long-lived/cached state.

6. **Code-fix & FixAll performance.** Code fixes run on demand (less hot), but
   provide a `FixAllProvider`, avoid recomputation across fixes, and keep
   document rewrites minimal.

7. **Performance budgets & acceptance criteria.**
   - Every new rule ships with at least one benchmark.
   - Target: **zero or near-zero allocations on the no-diagnostic path** (the
     common case where code is already well-formatted).
   - An **anti-patterns table** (mirrors the binding repo's "What to Avoid").
   - A **per-rule performance checklist** for use when adding/reviewing a rule.
   - Note CI regression-gating as future work.

## Deliverable 2 — Seed benchmark harness

A new project, `Blazor.Common.Analyzers.Benchmarks` (provisional name, per the
naming note), added to the solution.

**Project conventions** (mirroring the NuSourceDocs benchmark csproj):
- `Microsoft.NET.Sdk`, `OutputType=Exe`, a current `net*` TFM (the analyzer
  itself stays `netstandard2.0`; the benchmark host does not).
- `IsPackable=false`, `IsPublishable=false`.
- `PackageReference` to `BenchmarkDotNet` (added to `Directory.Packages.props`).
- `ProjectReference` to the existing analyzer project.
- The repo currently builds with `TreatWarningsAsErrors=true` and a strict
  analyzer set; the benchmark project narrows only what it must (e.g. the
  benchmark-method warnings the NuSourceDocs harness suppresses) **without**
  violating the project's no-suppressions policy for production code — these are
  benchmark-host build settings, not rule suppressions. Confirm during planning.

**Benchmarks included in the seed:**

1. **Core-logic micro-benchmark (the proof).** Parse a representative source
   snippet once with `CSharpSyntaxTree.ParseText`, extract a
   `SeparatedSyntaxList<ParameterSyntax>` (and an argument list), then benchmark
   two implementations of the line-uniqueness decision under `[MemoryDiagnoser]`:
   - `Baseline_HashSetLinq` — the current `Analyze<T>` logic.
   - `Optimized_ManualScan` — an allocation-free single-pass equivalent.
   Across both the all-on-one-line and jagged inputs. This yields the
   before/after time + allocation numbers embedded in the guide.

2. **End-to-end analyzer benchmark.** Build a `Compilation` from a synthetic
   source containing many parameter/argument lists, wrap it with
   `CompilationWithAnalyzers`, and benchmark `GetAnalyzerDiagnosticsAsync` for the
   current analyzer over the whole compilation. This is the realistic "what the
   IDE/build actually pays" figure and establishes the harness pattern future
   per-rule benchmarks follow.

**Outputs:** BenchmarkDotNet writes its `*-report-github.md` artifacts; the
relevant numbers are transcribed into `docs/PERFORMANCE.md` with the hardware
header, exactly as NuSourceDocs does.

## Components & boundaries

| Unit | Purpose | Depends on |
|---|---|---|
| `docs/PERFORMANCE.md` | Human-facing doctrine + checklist + anti-patterns | references the harness for numbers |
| `Blazor.Common.Analyzers.Benchmarks` (project) | Runnable proof + reusable benchmark pattern | BenchmarkDotNet, analyzer project, Roslyn |
| Core-logic benchmark | Before/after of `Analyze<T>` | parsed `SeparatedSyntaxList` |
| End-to-end benchmark | Realistic full-compilation cost | `CompilationWithAnalyzers` |

## Build sequence (for the implementation plan)

1. Add `BenchmarkDotNet` to `Directory.Packages.props`.
2. Create the benchmark project; add it to the solution; reference the analyzer
   project. Get a trivial benchmark running in Release.
3. Write the core-logic micro-benchmark (baseline + optimized) and capture
   numbers.
4. Write the end-to-end `CompilationWithAnalyzers` benchmark and capture numbers.
5. Write `docs/PERFORMANCE.md`, embedding the captured numbers and the worked
   `Analyze<T>` before/after.
6. Link `docs/PERFORMANCE.md` from `README.md` (and note the convention that
   future CLAUDE.md / contributor docs point to it).

## Testing & verification

- Benchmark project **builds in Release** and **runs** end-to-end (`dotnet run -c
  Release -- --filter "*"`), producing report artifacts.
- The optimized variant demonstrably allocates less than the baseline (the
  numbers must show it, not just assert it).
- `docs/PERFORMANCE.md` numbers match the generated report artifacts (no
  invented figures).
- Existing solution build and the 22 analyzers' tests remain green (this task
  does not modify shipping analyzer behavior).

## Open questions deferred to later tasks

- Final project/package name and diagnostic prefix (rebrand task).
- Whether to land the optimized `Analyze<T>` into the shipping analyzer.
- CI performance-regression gating.
