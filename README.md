[![CI Build](https://github.com/glennawatson/RoslynCommonAnalyzers/actions/workflows/ci.yml/badge.svg)](https://github.com/glennawatson/RoslynCommonAnalyzers/actions/workflows/ci.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![NuGet](https://img.shields.io/nuget/v/StyleSharp.Analyzers.svg?logo=nuget&label=StyleSharp.Analyzers)](https://www.nuget.org/packages/StyleSharp.Analyzers/)
[![Downloads](https://img.shields.io/nuget/dt/StyleSharp.Analyzers.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/StyleSharp.Analyzers/)
[![GitHub stars](https://img.shields.io/github/stars/glennawatson/RoslynCommonAnalyzers?style=social)](https://github.com/glennawatson/RoslynCommonAnalyzers/stargazers)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>
<a href="https://github.com/glennawatson/RoslynCommonAnalyzers">
  <img width="160" height="160" src="https://raw.githubusercontent.com/glennawatson/RoslynCommonAnalyzers/main/icons/icon.png" alt="StyleSharp.Analyzers">
</a>
<br>

# StyleSharp.Analyzers

A small set of Roslyn analyzers and code fixes that enforce **one** readability
convention everywhere it can apply: a comma-delimited list — method/constructor
parameters, call/`new` arguments, attribute arguments, primary-constructor
parameters, generic type-parameter and type-argument lists, function-pointer
parameter lists, and more — must either sit **entirely on one line** or have
**each item on its own line**. A "jagged" layout (some items sharing a line,
others wrapped) is reported, and the accompanying code fix rewrites the list so
every item is on its own line.

```csharp
// 👎 jagged — flagged
void Configure(string host, int port,
    bool useTls);

// 👍 each parameter on its own line
void Configure(
    string host,
    int port,
    bool useTls);

// 👍 all on one line
void Configure(string host, int port, bool useTls);
```

The analyzers target `netstandard2.0`, have **no runtime dependencies**, ship as
a development-only NuGet package, and are built against the Roslyn that ships
with Visual Studio 2022 17.14 (`Microsoft.CodeAnalysis.*` 4.14.0). The package
id is **`StyleSharp.Analyzers`**; the repository is `RoslynCommonAnalyzers`.

> Diagnostics share the `SST` prefix. Every rule is category `Readability`,
> default severity `Warning`, and ships with a code fix.

## Table of contents

- [Installation](#installation)
- [Rules](#rules)
- [Configuring severity](#configuring-severity)
- [How it works](#how-it-works)
- [Building and testing](#building-and-testing)
- [Contributing](#contributing)
- [License](#license)

## Installation

```bash
dotnet add package StyleSharp.Analyzers
```

It is a `DevelopmentDependency` analyzer package — it adds no assemblies to your
output and is not transitive to consumers of your library.

## Rules

Each rule has a documentation page under [`docs/rules`](docs/rules); the
`helpLinkUri` on every diagnostic points there.

| Rule | Applies to |
| --- | --- |
| [SST0001](docs/rules/SST0001.md) | Constructor declaration parameters |
| [SST0002](docs/rules/SST0002.md) | Method declaration parameters |
| [SST0003](docs/rules/SST0003.md) | Delegate declaration parameters |
| [SST0004](docs/rules/SST0004.md) | Indexer declaration parameters |
| [SST0005](docs/rules/SST0005.md) | Invocation (method call) arguments |
| [SST0006](docs/rules/SST0006.md) | Object creation (`new T(...)`) arguments |
| [SST0007](docs/rules/SST0007.md) | Element access (`x[...]`) arguments |
| [SST0008](docs/rules/SST0008.md) | Attribute arguments |
| [SST0009](docs/rules/SST0009.md) | Anonymous method (`delegate(...)`) parameters |
| [SST0010](docs/rules/SST0010.md) | Parenthesized lambda parameters |
| [SST0011](docs/rules/SST0011.md) | `record` / `record struct` primary-constructor parameters |
| [SST0012](docs/rules/SST0012.md) | `class` primary-constructor parameters (C# 12) |
| [SST0013](docs/rules/SST0013.md) | `struct` primary-constructor parameters (C# 12) |
| [SST0014](docs/rules/SST0014.md) | Target-typed `new(...)` arguments |
| [SST0015](docs/rules/SST0015.md) | `: base(...)` / `: this(...)` constructor-initializer arguments |
| [SST0016](docs/rules/SST0016.md) | `record Foo(...) : Bar(args)` base-type arguments |
| [SST0017](docs/rules/SST0017.md) | Local function parameters |
| [SST0018](docs/rules/SST0018.md) | `operator` declaration parameters |
| [SST0019](docs/rules/SST0019.md) | Conversion-operator declaration parameters (never reports — conversion ops always have one parameter; kept for symmetry) |
| [SST0020](docs/rules/SST0020.md) | Generic type-parameter lists — `class Foo<T1, T2>`, `void M<T1, T2>()` |
| [SST0021](docs/rules/SST0021.md) | Generic type-argument lists — `Foo<int, string>` |
| [SST0022](docs/rules/SST0022.md) | Function-pointer parameter lists — `delegate*<int, string, void>` |

## Configuring

StyleSharp is configured entirely through **`.editorconfig`** — there is no
`stylecop.json`-style file. Severity is set the standard way:

```ini
# bump everything to a build error
dotnet_diagnostic.SST0001.severity = error
# … or turn one rule off
dotnet_diagnostic.SST0007.severity = none
```

Rules that expose options read them from `.editorconfig` too, following the .NET
CA-analyzer convention (`stylesharp.<option>` general, `stylesharp.<RuleId>.<option>`
rule-specific):

```ini
[*.cs]
stylesharp.tuple_element_naming = pascal_case   # SST1316
stylesharp.union_member_naming = pascal_case    # SST1315
```

See **[docs/CONFIGURATION.md](docs/CONFIGURATION.md)** for the full list and the
rationale for using `.editorconfig` over a separate JSON file. Prefer
`.editorconfig` over scattering `#pragma warning disable` / `[SuppressMessage]`.

## How it works

Every rule is a `SyntaxNodeAnalysisContext`-based `DiagnosticAnalyzer` registered
for a specific `SyntaxKind` (or two — e.g. `RecordDeclaration` +
`RecordStructDeclaration`). It pulls the relevant `SeparatedSyntaxList`, and
reports when the items are split across lines such that they are neither all on
one line nor all on separate lines. The code fix rebuilds the list with a newline
after the opening token and each separator (and re-indents one level deeper than
the owning declaration), via the shared helpers in `ArgumentsOrParameterOnSameLineHelper`
and `UniqueLineCodeFixerHelper`.

The solution follows the standard analyzer layout:

| Project | Purpose |
| --- | --- |
| `StyleSharp.Analyzers` | the `DiagnosticAnalyzer`s (`netstandard2.0`) |
| `StyleSharp.Analyzers.CodeFixes` | the `CodeFixProvider`s (`netstandard2.0`) |
| `StyleSharp.Analyzers.Package` | packs the two above into the `StyleSharp.Analyzers` NuGet package |
| `StyleSharp.Analyzers.Tests` | TUnit tests using `Microsoft.CodeAnalysis.Testing` |

## Building and testing

```bash
dotnet restore StyleSharp.Analyzers.slnx
dotnet build StyleSharp.Analyzers.slnx --configuration Release
dotnet test --solution StyleSharp.Analyzers.slnx --configuration Release
```

`TreatWarningsAsErrors` is on and the repo's `.editorconfig` is strict — the
build is clean only when every analyzer (StyleCop, Roslynator, SonarAnalyzer,
the .NET analyzers) is satisfied. Fix issues rather than suppressing them.

## Contributing

Issues and PRs welcome. Adding a rule means: a `Sst####…Analyzer.cs`, a
matching `…CodeFixProvider.cs`, a `Sst####…AnalyzersUnitTest.cs` (markup-based
`CSharpCodeFixVerifier` tests), a `docs/rules/SST####.md` page, and a row in
`AnalyzerReleases.Unshipped.md`.

Performance is a first-class requirement: read the **[performance
guide](docs/PERFORMANCE.md)** before writing or reviewing a rule, and benchmark
with `StyleSharp.Analyzers.Benchmarks`.

## License

MIT — see [LICENSE](LICENSE).
