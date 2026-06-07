# Performance guide

This is the performance doctrine for every analyzer and code fix in
StyleSharp.Analyzers. Analyzers run on **every keystroke** in the IDE and on
**every build**; a wasteful pattern multiplied across dozens of rules and
millions of syntax nodes is the difference between a snappy and a sluggish
editing experience. the analyzer's reputation for slowness is precisely what this
project sets out to beat, so performance is a first-class requirement here, not
an afterthought.

This document describes *approaches*. Concrete measurements are not pinned here
(they go stale); produce them on demand with the benchmark harness under
`StyleSharp.Analyzers.Benchmarks` (see [Measure first](#measure-first)).

## Guiding principles

1. **The analyzer callback is a hot path.** Treat every line inside a
   `Register…Action` callback as if it runs millions of times, because it does.
2. **Zero allocations on the common path.** The common case is code that is
   already correct and produces *no* diagnostic. That path must not allocate.
3. **Prefer fast static helpers.** Shared logic lives in `static` helper classes
   operating on the syntax/semantic model passed in — no per-call object state,
   no instance allocation. This is the default paradigm for the library.
4. **Syntax before semantics.** A syntactic check is far cheaper than a semantic
   one. Decide with syntax alone whenever possible; reach for the semantic model
   only when syntax genuinely cannot answer the question.
5. **No LINQ in production code.** Treat LINQ as banned in
   `src/StyleSharp.Analyzers/` and `src/StyleSharp.Analyzers.CodeFixes/`.
   Iterator state machines, delegate captures, and convenience materialization
   are too easy to hide in code review and too expensive to pay on hot paths.
6. **Measure, don't guess.** Every performance claim is backed by a benchmark.

## Measure first

The harness is `src/benchmarks/StyleSharp.Analyzers.Benchmarks`
(BenchmarkDotNet, `[MemoryDiagnoser]`, `ShortRun`, default out-of-process toolchain).

```bash
# Run from src/

# Everything
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*"

# Just the core line-scan micro-benchmark, or just the end-to-end throughput
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*LineScan*"
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*Throughput*"

# Hot-path micro-benchmarks for the most common analyzer pipelines
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*HotPathBenchmarks*"

# Opt-in EventPipe profiling for allocation and CPU hot spots
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*HotPathProfiledAllocBenchmarks*"
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*HotPathProfiledCpuBenchmarks*"

# Target a single hot path when you want one trace per analyzer/path
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*TupleElementName_Clean*"
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*Spacing_Violating*"

# Isolate one analyzer family end-to-end when the combined hot-path suite is too broad
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*TupleElementNameBenchmarks*"
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*UseNameofBenchmarks*"
```

Two complementary lenses:

- **Micro** (`LineScanBenchmarks`) — the decision logic in isolation. Use it to
  prove a rewrite is allocation-free and faster than what it replaced.
- **Hot-path micro** (`HotPathBenchmarks`) — focused clean/violating corpora for
  the hottest analyzer pipelines (`SpacingAnalyzer`, tuple element access,
  `UseNameofAnalyzer`, `ArgumentGuardAnalyzer`, and the shared jagged-list helper).
- **Single-analyzer hot suites** (`TupleElementNameBenchmarks`,
  `UseNameofBenchmarks`) — full `CompilationWithAnalyzers` runs for the specific
  hot analyzer families whose internal fast paths are already covered by
  `HotPathBenchmarks`.
- **End-to-end** (`AnalyzerThroughputBenchmarks`) — the analyzers run over a real
  compilation through `CompilationWithAnalyzers`. This is the realistic
  "what the IDE/build pays" figure and the surface for hunting bottlenecks.
- **EventPipe** (`HotPathProfiledAllocBenchmarks` / `HotPathProfiledCpuBenchmarks`) —
  opt-in allocation and CPU sampling runs for terminal-driven hotspot hunting.
  Each hot analyzer family exposes both clean and violating paths so you can
  profile the common path first, then isolate the report path separately.

### EventPipe workflow

Use the profiled hot-path suites when a rule is known to do semantic work,
token scans, or other non-trivial analysis:

- `LineScan_*` — shared jagged-list helper used by the SST115x family.
- `TupleElementName_*` — `TupleElementNameAnalyzer` name-rewrite detection.
- `UseNameof_*` — `UseNameofAnalyzer` constructor-argument scanning.
- `ArgumentGuard_*` — `ArgumentGuardAnalyzer` throw-helper matching.
- `Spacing_*` — `SpacingAnalyzer` token-walk over a synthetic compilation.

Recommended loop:

1. Run `HotPathBenchmarks` first to see which hot path is slow or allocates.
2. Re-run the matching `HotPathProfiledAllocBenchmarks` or
   `HotPathProfiledCpuBenchmarks` method with a narrow `--filter`.
3. Open the exported `.speedscope.json` trace from `BenchmarkDotNet.Artifacts/`
   and inspect the hottest frames before changing code.
4. Make the smallest change that removes work from the clean path.
5. Re-run the same benchmark/filter to confirm the improvement.

When you want a quick terminal summary instead of opening Speedscope manually,
use the local trace filter:

```bash
# Newest matching EventPipe export under BenchmarkDotNet.Artifacts/
dotnet run --project tools/TraceFocus -- --pattern "ExtensionBlockProfiledCpuBenchmarks.ExtensionBlock_Violating(Nodes_ 1000)"

# Explicit file, plus a narrower analyzer-specific include
dotnet run --project tools/TraceFocus -- --file "BenchmarkDotNet.Artifacts/StyleSharp.Analyzers.Benchmarks.ParameterListLayoutProfiledCpuBenchmarks.ParameterListLayout_Violating(Nodes_ 1000)-20260606-212946.speedscope.json" --include "ParameterListLayoutAnalyzer"
```

`TraceFocus` reads BenchmarkDotNet's `*.speedscope.json` output directly,
filters out the default BenchmarkDotNet / threading / analyzer-driver noise,
and prints the remaining hot frames plus the hottest analyzer-visible stacks.

When a benchmark isn't granular enough, ask the compiler itself:

```bash
dotnet build -c Release /p:ReportAnalyzer=true   # per-analyzer wall-clock in the build log
```

Always quote the hardware and runtime alongside any number you record (CPU, OS,
.NET version, BenchmarkDotNet version, job config) — a number without its
environment is meaningless.

## Registration discipline

- **Register the narrowest action that works.** Prefer
  `RegisterSyntaxNodeAction` constrained to the exact `SyntaxKind`s you handle.
  Avoid `RegisterSyntaxTreeAction` / whole-tree walks when a node action covers
  the case.
- **Enable concurrency and skip generated code.** In `Initialize`:

  ```csharp
  context.EnableConcurrentExecution();
  context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
  context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
  ```

  `GeneratedCodeAnalysisFlags.None` skips generated files entirely — free
  savings on the common case where a project has generated code you don't want
  to lint.
- **Resolve per-compilation state once.** If a rule needs well-known symbols,
  resolve them in a `RegisterCompilationStartAction` and cache them, rather than
  calling `GetTypeByMetadataName` per node. Cache keyed on `Compilation` via a
  `ConditionalWeakTable<Compilation, T>` so it's collected with the compilation.
- **Descriptors are static.** Build each `DiagnosticDescriptor` once as
  `static readonly`; never per invocation.
- **Don't over-consolidate for perf.** Splitting one rule per analyzer class is
  fine: the analysis driver's overhead is dominated by parsing and the
  compilation walk, not by the number of *our* analyzers registered. Merge
  classes only when it improves clarity, not as a performance tactic.

## Allocation discipline (the callback hot path)

This is where rules are won or lost. Inside a callback:

- **No LINQ in production analyzer/code-fix code.** `Select`/`Where`/`ToList` and
  similar operators are banned in `src/StyleSharp.Analyzers/` and
  `src/StyleSharp.Analyzers.CodeFixes/`. Use a manual `for`/`foreach`.
- **No per-node collections.** No `HashSet`, `List`, arrays, or `StringBuilder`
  allocated per node. Decide with a couple of locals.
- **Use struct enumerators.** `SeparatedSyntaxList<T>`, `SyntaxList<T>`,
  `ChildSyntaxList`, and `SyntaxTriviaList` all enumerate without allocating —
  `foreach` directly, never `.ToList()`.
- **Minimize `GetLocation()` / `GetLineSpan()`.** `GetLocation()` allocates a
  `Location` object; prefer `SyntaxNode.Span` + `tree.GetLineSpan(span)` (a
  struct result over the tree's cached line table) and only materialize a
  `Location` when you actually report.
- **Cheapest checks first; bail early.** Order guards from cheap to expensive
  (e.g. `list.Count <= 1`) and return the moment the verdict is decided.

### Worked example

The shared list-layout check used by all the `SST00xx` rules went from this
allocating shape:

```csharp
// Before: a HashSet, a LINQ closure, a List, and a Location per item —
// on every parameter/argument list node in the compilation.
var diffChecker = new HashSet<int> { parameterLine };
var lineNumbers = list.Select(x => x.GetLocation().GetLineSpan().StartLinePosition.Line).ToList();
diffChecker.UnionWith(lineNumbers);
var allDifferent = diffChecker.Count == list.Count + 1;
```

to an allocation-free single pass:

```csharp
// After: zero heap allocations, early exit. Exploits that item start lines are
// monotonically non-decreasing — a list is "jagged" precisely when some adjacent
// pair shares a line AND some adjacent pair is separated.
var previousLine = tree.GetLineSpan(listNode.Span).StartLinePosition.Line;
var sawShared = false;
var sawSeparated = false;

foreach (var item in list)
{
    var line = tree.GetLineSpan(item.Span).StartLinePosition.Line;
    if (line == previousLine) sawShared = true;
    else sawSeparated = true;

    if (sawShared && sawSeparated)
    {
        context.ReportDiagnostic(Diagnostic.Create(rule, context.Node.GetLocation()));
        return;
    }

    previousLine = line;
}
```

The lesson generalizes: look for a property of the input (here, monotonic line
numbers) that lets a single pass with a few locals replace a set/collection.

## Symbol & semantic-model discipline

- **Syntactic fast-path first.** Only call `GetSymbolInfo` / `GetTypeInfo` /
  `GetDeclaredSymbol` after syntax has failed to decide. These bind, which is
  expensive.
- **Cache well-known symbols** per compilation (see registration discipline);
  never repeat `GetTypeByMetadataName`.
- **Compare symbols with `SymbolEqualityComparer.Default`.**
- **Never put `ISymbol`, `SyntaxNode`, or `Location` into long-lived/cached
  state.** They root large object graphs (and, for incremental scenarios, defeat
  caching). Extract the small value you need (a string, a flag) and keep that.

## Code-fix & FixAll performance

Code fixes run on demand, so they're far less hot than analyzers — but still:

- Provide a `FixAllProvider` (`WellKnownFixAllProviders.BatchFixer` when it fits)
  so bulk fixes batch instead of re-running per occurrence.
- Compute the minimal rewrite; don't re-walk the whole document.
- Don't do semantic work in the fix that the analyzer already proved.

## Anti-patterns

| Anti-pattern | Why it hurts | Do instead |
|---|---|---|
| LINQ in production code | Iterator/closure/materialization overhead hidden in hot paths | Manual `for`/`foreach` |
| `HashSet`/`List` per node | Heap allocation per node | A few local variables / single pass |
| `.ToList()` on a `SeparatedSyntaxList` | Throws away the struct enumerator | `foreach` the list directly |
| `GetLocation()` per item | Allocates a `Location` each call | `tree.GetLineSpan(node.Span)`; locate only when reporting |
| `GetTypeByMetadataName` per node | Re-resolves symbols repeatedly | Resolve once in `CompilationStartAction`, cache per `Compilation` |
| Semantic query when syntax suffices | Binding is expensive | Syntactic fast-path; semantics only as fallback |
| `ISymbol`/`SyntaxNode` in cached state | Roots large graphs | Extract and cache the small value |
| Instance state on a helper | Per-call allocation | `static` helpers over the passed-in model |

## Checklist for a new (or reviewed) rule

- [ ] Registered the narrowest action for the exact `SyntaxKind`(s).
- [ ] `EnableConcurrentExecution` + `ConfigureGeneratedCodeAnalysis`.
- [ ] No LINQ, no per-node collections, no instance helper state in the callback.
- [ ] `foreach` over struct enumerators; no `.ToList()`.
- [ ] `GetLocation()` only on the report path.
- [ ] Any semantic work is gated behind a syntactic fast-path and uses cached
      well-known symbols.
- [ ] Descriptors are `static readonly`.
- [ ] A benchmark exists (micro and/or end-to-end) and shows zero/near-zero
      allocation on the no-diagnostic path.

## Performance-sensitive rules

Most StyleSharp rules are narrow `RegisterSyntaxNodeAction` checks over a single
`SyntaxKind` and are effectively free. A few carry a higher cost or are heuristic; the
table records them so the cost is a deliberate choice, not a surprise. Where a rule is
**off by default**, enable it in `.editorconfig` only when you want it.

| Rule(s) | Cost | Default | Notes |
| --- | --- | --- | --- |
| SST1305 (Hungarian notation) | Heuristic + per-name `string` slicing | **Off** | Pattern-matches name prefixes against an allow-list; inherently fuzzy. Opt-in. |
| SST1306 / SST1308 / SST1310 (field-name styles) | Cheap | **Off** | Conflict with the runtime `_camelCase` convention (SST1309); shipped for consumers who want the the analyzer style. |
| SST1507, SST1517, SST1518 (blank-line / file-boundary) | One `RegisterSyntaxTreeAction` line-table scan per file | On | Scans the cached line table once; no per-node cost. |
| SST1512 / SST1515 (single-line comment spacing) | `RegisterSyntaxTreeAction` + a `FindTrivia` per candidate line | On | Heaviest of the layout rules — still once per file, not per node. |
| SST1626 (misplaced `///`) | `FirstAncestorOrSelf` walk per doc comment | On | Only runs on documentation trivia, which is sparse. |
| SST1516 / SST1201–SST1217 (ordering) | Pairwise scan of a member / using list | On | Lists are short; single pass, no allocations on the clean path. |

### Rules intentionally **not** ported for performance reasons

- **the rule (spelling)** — needs an embedded dictionary and per-word lookups over all
  documentation text. Too heavy for an always-on analyzer; not ported.
- **the rule → [SST1503](rules/SST1503.md)** *is* ported but **off by default**: the
  repository's `.editorconfig` defers brace-omission to the analyzer's `the rule` (measured faster).
  Enable `SST1503` instead of `the rule` if you are not running the analyzer.
- **the rule (use built-in type alias)** is left to the analyzer's `the rule` (measured
  faster); StyleSharp does not duplicate it.

When adding a rule that must walk the whole tree (a `RegisterSyntaxTreeAction`), prefer a
single pass over the cached line table or token stream, and add it to the table above so
the cost stays visible.
