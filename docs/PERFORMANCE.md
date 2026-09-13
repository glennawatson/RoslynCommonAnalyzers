# Performance guide

The performance doctrine for every analyzer, code fix and shared helper in this repository lives in
**[CLAUDE.md](../CLAUDE.md#performance--the-doctrine-not-a-link)**, in the repository root.

It is kept there, inline, rather than here: it is the guidance that has to be in front of anyone
changing a hot path, and a linked document is a document that gets skipped. There is one copy so the
two cannot drift.

It covers:

- the three-corpus measurement (`startup` / `clean` / `violating`), and why omitting `startup` makes
  the other two numbers lie
- the healthy threshold for the no-diagnostic path, which is the number that matters
- the two paths a code fix has, and why they are judged differently
- lazy metadata resolution done right, and the cached first-demand holder shape
- lock-free concurrency where it measures, and where keeping the lock is faster
- building syntax with full factory and `Update(...)` overloads instead of `WithX()` chains
- allocation discipline inside a callback
- the two shapes that look like defects and are not
- the checklist for a new or reviewed rule

## Benchmarks

```bash
# Run from src/
dotnet run -c Release --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*"
dotnet run -c Release --project benchmarks/PerformanceSharp.Analyzers.Benchmarks -- --filter "*"
dotnet run -c Release --project benchmarks/SecuritySharp.Analyzers.Benchmarks -- --filter "*"
```

Narrow with a filter (`--filter "*DescendantTraversalBenchmarks*"`, `--filter "*CodeFixBenchmarks*"`),
and pin any run whose timing matters:

```bash
taskset -c 0-6 nice -n -20 dotnet run -c Release \
  --project benchmarks/StyleSharp.Analyzers.Benchmarks -- --filter "*HotPathBenchmarks*"
```

## Rules with a deliberate cost

Most rules are narrow `RegisterSyntaxNodeAction` checks over a single `SyntaxKind` and are
effectively free. These carry a higher cost or are heuristic, recorded so the cost stays a deliberate
choice. Where a rule is **off by default**, enable it in `.editorconfig` only when you want it.

| Rule(s) | Cost | Default | Notes |
| --- | --- | --- | --- |
| SST1305 (Hungarian notation) | Heuristic + per-name `string` slicing | **Off** | Pattern-matches name prefixes against an allow-list; inherently fuzzy. |
| SST1306 / SST1308 / SST1310 (field-name styles) | Cheap | **Off** | Conflict with the runtime `_camelCase` convention (SST1309); shipped for consumers who want the alternative. |
| SST1507, SST1517, SST1518 (blank-line / file-boundary) | One `RegisterSyntaxTreeAction` line-table scan per file | On | Scans the cached line table once; no per-node cost. |
| SST1512 / SST1515 (single-line comment spacing) | `RegisterSyntaxTreeAction` + a `FindTrivia` per candidate line | On | Heaviest of the layout rules — still once per file. |
| SST1626 (misplaced `///`) | `FirstAncestorOrSelf` walk per doc comment | On | Only runs on documentation trivia, which is sparse. |
| SST1516 / SST1201–SST1217 (ordering) | Pairwise scan of a member / using list | On | Lists are short; single pass, no allocations on the clean path. |
| SST1503 (require braces) | Cheap | **Off** | Enforcing braces is opt-in, so the rule only runs where it is wanted. |

Spelling checks are intentionally not shipped: an embedded dictionary with per-word lookups over all
documentation text is too heavy for an always-on analyzer. Built-in type alias enforcement is left to
other tooling.

A rule that must walk the whole tree (`RegisterSyntaxTreeAction`) should make a single pass over the
cached line table or token stream, and be added to the table above so the cost stays visible.
