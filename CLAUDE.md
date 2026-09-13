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
  The second is a rule the code physically cannot satisfy: `Polyfills/IsExternalInit.cs`
  suppresses the empty-type rule because the compiler only requires that type to
  *exist* for `init` accessors to compile on netstandard2.0, so it has no members by
  design. Keep both exceptions narrow, document the justification inline, and do not
  use a suppression anywhere a code change would do. The repo builds its own source
  under `TreatWarningsAsErrors` with a strict analyzer set, including the benchmark
  project.

- **Repo layout:** repo metadata stays at the repository root, but build entry
  points live under `src/`. Run `dotnet` commands from `src/`; projects are
  grouped under `src/`, `tests/`, and `benchmarks/` inside that folder.

- **Performance / allocations first.** Analyzer callbacks run on every keystroke.
  Keep the no-diagnostic path allocation-free; compute suggested names only after
  a violation is found. The full doctrine, with the measurements behind it, is in
  **[Performance](#performance--the-doctrine-not-a-link)** below — read it before
  touching an analyzer, code fix or shared helper.

- **No LINQ in production analyzer or code-fix code**, and keep the guardrail that
  enforces it: those projects remove the implicit `System.Linq` global using via
  `<Using Remove="System.Linq" />`, so accidental LINQ fails at compile time.
  Use explicit `for`/`foreach` loops and a few locals. See
  [Performance](#performance--the-doctrine-not-a-link).

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

- **Never build metadata references inside a test.** `MetadataReference.CreateFromFile`
  memory-maps the assembly and holds the mapping for as long as the reference lives,
  and it caches nothing. A helper that walks `TRUSTED_PLATFORM_ASSEMBLIES` per call
  therefore maps the whole platform again for every test that touches it and keeps
  each copy alive — roughly 40 ms and 200-odd mappings a time. A metadata reference
  is immutable and Roslyn shares one across compilations, so take the cached sets from
  `RuntimeMetadataReferences` (`Platform`, or `CoreLibrary` when binding the primitives
  is enough) in `src/Shared/Tests/`, which every test project links. The same applies to
  `ReferenceAssemblies`: use the shared statics in `AnalyzerFrameworks`, and never call
  `AddPackages`/`AddAssemblies` in a test body — each call returns a **new**
  `ReferenceAssemblies` whose NuGet resolution is then redone per test. Where a test
  genuinely needs an extra package, cache the composed instance in a static.

## `docs/` is written for the user, never for us

Everything under `docs/` is consumer documentation. The reader is a developer who just saw one of
our diagnostics in their editor and wants to get on with their day. They do not work on this
repository and never will.

A rule page answers four questions:

1. **What does the analyzer fire on?** The shape in their code that triggers it.
2. **What does good and bad code look like?** A short bad example and the corrected version.
3. **What can be configured?** The `.editorconfig` keys, their values and defaults.
4. **When should it be suppressed?** The cases where the rule is not worth following.

Where a code fix exists, say what it does to their code — that is the thing they are about to accept
in the editor, so it matters more than anything about the analyzer.

Those four are the backbone. **Other consumer information is allowed, but only where it earns its
place** — a trap the reader will hit, a related rule that interacts with this one, a language-version
or framework limit that changes whether the rule applies. The test is whether it changes what the
reader does. If it does not, cut it. Never add a section merely because another page has one.

**Never put implementation detail on a rule page.** No syntactic fast paths, no semantic model, no
allocation counts for the analyzer, no benchmark figures, no description of how a lookup is cached or
gated. If a sentence would still be true had we implemented the rule a completely different way, it
is probably about the user's code and belongs. If it describes what our analyzer touches, when, or
how cheaply, it does not.

The one exception that is easy to get backwards: a `PSH` rule exists *because* something costs the
user time or memory. "Each call allocates a closure" describes **their** code and is the whole point
of the page. "The clean path is syntactic" describes **ours** and must go.

Design and performance doctrine lives in this file. Do not link `docs/` pages to it.

Write plainly: short sentences, one idea each, active voice. Assume basic C# and nothing about
Roslyn.

## Performance — the doctrine, not a link

Analyzer callbacks run on **every keystroke** and on every build. A wasteful pattern multiplied
across 637 analyzers and millions of syntax nodes is the whole difference between this package set
and the sluggish third-party analyzers it exists to beat. Everything below is measured on this
repository, not inherited wisdom.

### The number that matters is the clean path

The overwhelming majority of callbacks see code that does **not** violate the rule. That path is the
headline figure: **healthy is 0.00–0.57% of sampled bytes; 1% or more is a defect.**

Measure on three corpora, never two:

| Path | Corpus | What it isolates |
| --- | --- | --- |
| `startup` | one empty class | the fixed cost every compilation pays before the rule can have anything to say |
| `clean` | non-violating sources | `startup` plus the per-node cost of deciding "nothing to report" |
| `violating` | sources that trip the rule | the above plus the diagnostics the rule exists to produce |

**Measure `startup` or the other two lie.** A short corpus is mostly compilation, so a rule that
resolves ten metadata types at compilation start looks like a per-node defect when it is a fixed
cost paid once. `clean - startup` is the genuine per-node cost.

A code fix has two paths instead, and they differ by ~150x, so never merge them:
`register` (`RegisterCodeFixesAsync`, run whenever the IDE populates the lightbulb — hot, must decide
cheaply) and `apply` (resolving the action, which exists to produce syntax — allocation here is
output, not waste).

### Resolve metadata lazily, and cache it — both properties, or it is worse

This is the single largest defect the traces found: resolving well-known symbols eagerly in
`RegisterCompilationStartAction` cost **71 MB across 276 analyzers on an empty file**. Deferring it
cut the set to 36.5 MB. It looks like the textbook Roslyn idiom, which is exactly why it survived so
long — do not restore it.

A first-demand holder must be **resolved at most once per compilation** *and* **not at all when
nothing needs it**. Moving `GetTypeByMetadataName` into a per-node callback satisfies the second and
breaks the first, which is worse than what it replaced.

Keep the `lock`, but off the fast path — measured cold at 8 threads / warm at 1 thread:
no synchronisation 25,392 us / 3,842 us and the builder runs **8 times**; `lock` inside the queried
method 3,951 us / 3,435 us; **fast path inlined with the lock in its own method 4,067 us / 1,266 us
and the builder runs once.** A `lock` in the method body blocks inlining, so the warm path pays for
a slow path it never takes.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal bool Contains(ISymbol symbol, CancellationToken cancellationToken) =>
    (Volatile.Read(ref _targets) ?? Resolve(cancellationToken)).Contains(symbol);

[MethodImpl(MethodImplOptions.NoInlining)]
private HashSet<ISymbol> Resolve(CancellationToken cancellationToken)
{
    lock (_gate)
    {
        var targets = _targets;
        if (targets is null)
        {
            targets = Collect(compilation, cancellationToken);
            Volatile.Write(ref _targets, targets);
        }

        return targets;
    }
}
```

Where the cached value is cheap (two or three metadata lookups), a race is harmless: use
`Interlocked.CompareExchange` over **one immutable record holding every resolved value**. Never
publish the fields one at a time. And gate the rule on the probe — a project whose framework lacks
the suggested API must pay nothing and must never be told to use it.

### Concurrency: lock-free only where it measures

`ConcurrentDictionary.GetOrAdd` needs a factory that **captures nothing**, passed state through the
`TArg` overload. Measured on a read-heavy 256-key cache at 1 thread / 8 threads: `lock` +
`Dictionary` 8.93 / 266.08 us; static factory **4.04 / 34.81 us (2.21x, 7.64x faster)**; a
**capturing lambda 14.25 us — 1.60x *slower* than the lock it replaced** at one thread, which is the
common case for a single file. Removing a lock is a means, not the goal: if a site cannot use a
non-capturing factory, keep the lock.

A container only needs to be concurrent if two callbacks can reach it at once. Written from a
per-node or per-symbol callback → concurrent is required (`EnableConcurrentExecution` is on). Built
in a compilation-start body and then only read, or a per-invocation local → use the plain type.

### Build syntax with the full factory overload, never a mutator chain

Every `WithX(...)` returns a brand new node, so a chain of three allocates three and discards two.
`SyntaxFactory` and every node's `Update(...)` take all children at once — use them. `Token(kind)`
also seeds elastic-marker trivia a later formatting pass must reconcile.

```csharp
SyntaxFactory.Token(default, SyntaxKind.SemicolonToken, closeBrace.TrailingTrivia);  // one allocation
SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(...);              // two, one discarded
```

A single `WithX(...)` on an existing node is already one allocation — leave it. Only collapse one
when its receiver is a `SyntaxFactory.` call whose full overload absorbs it.

### Inside the callback

- **Syntax before semantics.** Decide with syntax alone wherever possible; `GetSymbolInfo` /
  `GetTypeInfo` / `GetDeclaredSymbol` bind, and binding is the expensive thing. Order guards cheap to
  expensive and return the moment the verdict is decided.
- **`GetDeclaredSymbol` over per-entry `GetSymbolInfo`** when a loop walks *declarations*. When it
  resolves *references* to symbols declared elsewhere, `GetSymbolInfo` is the only API that answers
  it — that residual is irreducible, record it as such.
- **No per-node collections.** No `HashSet`, `List`, array or `StringBuilder` per node; a few locals
  and a single pass. Where a buffer is genuinely needed, pre-size it; prefer an array when the final
  size is known.
- **Struct enumerators.** `SeparatedSyntaxList<T>`, `SyntaxList<T>`, `ChildSyntaxList` and
  `SyntaxTriviaList` enumerate without allocating — `foreach` them, never `.ToList()`. Never
  `ToList()`/`ToArray()` just to keep processing.
- **`GetLocation()` only on the report path.** It allocates; prefer `node.Span` +
  `tree.GetLineSpan(span)` and materialise a `Location` only when reporting.
- **No `DescendantNodes()` on a hot path.** It allocates an iterator per call. Use the shared
  `DescendantTraversalHelper` with a `static` visitor, or index `ChildNodesAndTokens()`, so an early
  exit costs nothing.
- **Descriptors are `static readonly`**, built once, never per invocation.
- **Never cache an `ISymbol`, `SyntaxNode` or `Location`** in long-lived state — they root large
  graphs. Extract the small value and keep that.
- **Register the narrowest action** for the exact `SyntaxKind`s, with `EnableConcurrentExecution()`
  and `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`.

### No LINQ in production code

`System.Linq` is removed from the global usings in `src/StyleSharp.Analyzers*`,
`src/PerformanceSharp.Analyzers*` and `src/SecuritySharp.Analyzers*`, so a query or a
`Select`/`Where`/`ToList` will not compile. **Keep that guardrail.** Iterator state machines,
closures and convenience materialization are too easy to miss in review and too expensive here.
Roslyn's own syntax-list helpers (`SyntaxTokenList.Any(SyntaxKind.X)` and friends) are **not** LINQ
and are fine.

### Two shapes that look like defects and are not

- **A closure captured at registration time is free.** `Initialize` and a compilation-start body run
  once per compilation, so a lambda capturing per-compilation state costs one display class per
  compilation and hands it to a node action that runs millions of times. There is no cheaper form.
  Across 637 analyzers, **no** startup row had a real `<>c__DisplayClass` as its heaviest allocating
  frame — the category was 288 audit findings and zero measured bytes. What looks like a closure in
  a trace is usually `+<>c.`, the compiler's cached singleton for a lambda that captures nothing.
- **A helper that produces a suggestion is producing output.** `NamingHelper.SuggestPascalCase` and
  friends run only after a rule has decided the name is wrong. Find every caller before "fixing" a
  string allocation: if the caller computes the suggestion *before* deciding, the caller is the
  defect, not the helper. Of 21 flagged utility sites, 19 were correctly not defects.

### Never accept or reject a shape by inspection

A syntax audit finds *candidates*; only the trace says which cost anything, and this repository has
been burned in both directions — eager metadata dismissed as idiom was the biggest defect in the
set, and closure-capture treated as a backlog was worth nothing. Join the audit to the sweep and let
the bytes decide. One `GCAllocationTick` samples roughly every 100 KB, so under about five ticks a
"share of allocation" resolves nothing; fix that with more iterations inside the same window, not a
bigger corpus.

Tooling and the reproducible commands live outside the repo in
`~/source/glennawatson/benchmarking`; the in-repo harnesses are
`benchmarks/{StyleSharp,PerformanceSharp,SecuritySharp}.Analyzers.Benchmarks` (BenchmarkDotNet,
`[MemoryDiagnoser]`). Pin any run whose timing matters: `taskset -c 0-6 nice -n -20`.

### Checklist for a new or reviewed rule

- [ ] Narrowest action for the exact `SyntaxKind`(s); concurrency and generated-code flags set.
- [ ] No LINQ, no per-node collections, no instance helper state in the callback.
- [ ] `foreach` over struct enumerators; no `.ToList()`.
- [ ] `GetLocation()` only on the report path.
- [ ] Semantic work gated behind a syntactic fast path.
- [ ] Any metadata lookup resolved lazily **and** cached per compilation, and the rule silent when
      the API is absent.
- [ ] Syntax built with full factory / `Update(...)` overloads, not `WithX()` chains.
- [ ] Descriptors `static readonly`.
- [ ] The clean path measured at under 1% of sampled bytes, or its residual named: output the method
      must produce, a framework call, or a forced box.

## Multi-Roslyn targeting

The analyzer + code-fix assemblies build once per Roslyn **slot** and pack under
`analyzers/dotnet/<slot>/cs` so the SDK auto-loads the highest slot `<=` the host
compiler's `CompilerApiVersion`:

The host column is the measured `csc -version` of each SDK, not its marketing
name — the slot is chosen by `CompilerApiVersion`, and the two diverge badly
(the .NET 10 SDK reports Roslyn 5.x, not 4.14).

| Slot | Roslyn | Host (`csc -version`) |
| --- | --- | --- |
| `roslyn4.8` | 4.8.0 (floor) | .NET 8 SDK (4.11) / VS 17.8, C# 12 |
| `roslyn4.14` | 4.14.0 | .NET 9 SDK (4.14), C# 13 |
| `roslyn5.3` | 5.3.0 | .NET 10 SDK through 10.0.3xx (5.6), C# 14 |
| `roslyn5.9` | 5.9.0 | .NET 10 SDK 10.0.4xx (5.9) and .NET 11 (5.11), C# 15 |

No shipping SDK reports exactly 5.3; that slot serves the 5.3–5.8 range. 5.9 is
the highest `Microsoft.CodeAnalysis` on NuGet — the .NET 11 compiler is 5.11 but
no matching package is published, so 5.9 is the ceiling we can build against.

Slot wiring lives in `src/Directory.Build.props` (`RoslynVersion` → package version +
`ROSLYN_*_OR_GREATER` constants + segregated `bin`/`obj`). Keep these assemblies
`netstandard2.0` (RS1041). Funnel all `ImmutableArray` creation through
`ImmutableArrays.Of(...)` — the 4.8 floor can't bind collection expressions for
`ImmutableArray` while 4.14+ requires them.

**For C# 15 syntax**, the 5.9 slot exposes the real syntax model —
`UnionDeclarationSyntax`, `SyntaxKind.WithElement` (collection expression
arguments), `ClosedKeyword`, `SafeKeyword`, `UnsafeExpression`, and indexers
inside an `ExtensionBlockDeclaration`. Two ways to reach it, in order of
preference:

1. **Version-tolerant structural detection** where a symbol will do — probe a
   well-known type/interface/attribute by name (e.g. the `IUnion` marker in
   `Sst1315UnionMemberNamingAnalyzer`) and gate the whole rule on it being
   present, so the rule costs nothing otherwise and works on every slot.
2. **`#if ROSLYN_5_9_OR_GREATER`** when the rule genuinely needs the syntax
   nodes. Gating loses no coverage: a host too old to load the 5.9 slot cannot
   compile C# 15 either, so there is nothing for the rule to find there.

Keep the repo's own `LangVersion` pinned rather than `latest` — see the comment
in `src/Directory.Build.props`. Under `latest` the language surface floats with
whichever SDK is installed, and the .NET 11 SDK resolving C# 15 turns on style
rules whose fixes cannot compile on the floor.

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

When a rule moves between packages it gets a **new id** in the destination — ids
are never reused or shared across packages, and a retired id is never handed to
a different rule.

Adding a rule: descriptor in the group's `Rules` class (or inline), an analyzer,
tests, and a `docs/rules/<ID>.md` page. Configurable options go in
`.editorconfig` and `docs/CONFIGURATION.md`.

**Every new rule is wired into both `.editorconfig` files before it ships.** The
repo's own root `.editorconfig` gets a severity line so this codebase is held to
the rule, and the package's `recommended-*.editorconfig` preset gets one so
consumers do — commented out where the rule is opt-in. A rule that ships in
neither is enforced nowhere and nobody discovers it.

There are no analyzer release-tracking files, and RS2008 is off in
`.editorconfig`: every rule ships as soon as it is written, so the files only
restated the descriptors. `docs/rules/` says what a rule does and the GitHub
release notes say what changed.
