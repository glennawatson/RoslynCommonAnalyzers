[![CI Build](https://github.com/glennawatson/RoslynCommonAnalyzers/actions/workflows/ci.yml/badge.svg)](https://github.com/glennawatson/RoslynCommonAnalyzers/actions/workflows/ci.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![NuGet](https://img.shields.io/nuget/v/Blazor.Common.Analyzers.svg?logo=nuget&label=Blazor.Common.Analyzers)](https://www.nuget.org/packages/Blazor.Common.Analyzers/)
[![Downloads](https://img.shields.io/nuget/dt/Blazor.Common.Analyzers.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/Blazor.Common.Analyzers/)
[![GitHub stars](https://img.shields.io/github/stars/glennawatson/RoslynCommonAnalyzers?style=social)](https://github.com/glennawatson/RoslynCommonAnalyzers/stargazers)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>
<a href="https://github.com/glennawatson/RoslynCommonAnalyzers">
  <img width="160" height="160" src="https://raw.githubusercontent.com/glennawatson/RoslynCommonAnalyzers/main/icons/icon.png" alt="Blazor.Common.Analyzers">
</a>
<br>

# Blazor.Common.Analyzers

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
id is **`Blazor.Common.Analyzers`**; the repository is `RoslynCommonAnalyzers`.

> Diagnostics share the `RCGS` prefix. Every rule is category `Readability`,
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
dotnet add package Blazor.Common.Analyzers
```

It is a `DevelopmentDependency` analyzer package — it adds no assemblies to your
output and is not transitive to consumers of your library.

## Rules

Each rule has a documentation page under [`docs/rules`](docs/rules); the
`helpLinkUri` on every diagnostic points there.

| Rule | Applies to |
| --- | --- |
| [RCGS0001](docs/rules/RCGS0001.md) | Constructor declaration parameters |
| [RCGS0002](docs/rules/RCGS0002.md) | Method declaration parameters |
| [RCGS0003](docs/rules/RCGS0003.md) | Delegate declaration parameters |
| [RCGS0004](docs/rules/RCGS0004.md) | Indexer declaration parameters |
| [RCGS0005](docs/rules/RCGS0005.md) | Invocation (method call) arguments |
| [RCGS0006](docs/rules/RCGS0006.md) | Object creation (`new T(...)`) arguments |
| [RCGS0007](docs/rules/RCGS0007.md) | Element access (`x[...]`) arguments |
| [RCGS0008](docs/rules/RCGS0008.md) | Attribute arguments |
| [RCGS0009](docs/rules/RCGS0009.md) | Anonymous method (`delegate(...)`) parameters |
| [RCGS0010](docs/rules/RCGS0010.md) | Parenthesized lambda parameters |
| [RCGS0011](docs/rules/RCGS0011.md) | `record` / `record struct` primary-constructor parameters |
| [RCGS0012](docs/rules/RCGS0012.md) | `class` primary-constructor parameters (C# 12) |
| [RCGS0013](docs/rules/RCGS0013.md) | `struct` primary-constructor parameters (C# 12) |
| [RCGS0014](docs/rules/RCGS0014.md) | Target-typed `new(...)` arguments |
| [RCGS0015](docs/rules/RCGS0015.md) | `: base(...)` / `: this(...)` constructor-initializer arguments |
| [RCGS0016](docs/rules/RCGS0016.md) | `record Foo(...) : Bar(args)` base-type arguments |
| [RCGS0017](docs/rules/RCGS0017.md) | Local function parameters |
| [RCGS0018](docs/rules/RCGS0018.md) | `operator` declaration parameters |
| [RCGS0019](docs/rules/RCGS0019.md) | Conversion-operator declaration parameters (never reports — conversion ops always have one parameter; kept for symmetry) |
| [RCGS0020](docs/rules/RCGS0020.md) | Generic type-parameter lists — `class Foo<T1, T2>`, `void M<T1, T2>()` |
| [RCGS0021](docs/rules/RCGS0021.md) | Generic type-argument lists — `Foo<int, string>` |
| [RCGS0022](docs/rules/RCGS0022.md) | Function-pointer parameter lists — `delegate*<int, string, void>` |

## Configuring severity

These are formatting/readability conventions, so tune them per project in
`.editorconfig`:

```ini
# bump everything to a build error
dotnet_diagnostic.RCGS0001.severity = error
# … or turn one rule off
dotnet_diagnostic.RCGS0007.severity = none
```

Prefer `.editorconfig` over scattering `#pragma warning disable` / `[SuppressMessage]`.

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
| `Blazor.Common.Analyzers` | the `DiagnosticAnalyzer`s (`netstandard2.0`) |
| `Blazor.Common.Analyzers.CodeFixes` | the `CodeFixProvider`s (`netstandard2.0`) |
| `Blazor.Common.Analyzers.Package` | packs the two above into the `Blazor.Common.Analyzers` NuGet package |
| `Blazor.Common.Analyzers.Tests` | TUnit tests using `Microsoft.CodeAnalysis.Testing` |

## Building and testing

```bash
dotnet restore Blazor.Common.Analyzers.sln
dotnet build Blazor.Common.Analyzers.sln --configuration Release
dotnet test --solution Blazor.Common.Analyzers.sln --configuration Release
```

`TreatWarningsAsErrors` is on and the repo's `.editorconfig` is strict — the
build is clean only when every analyzer (StyleCop, Roslynator, SonarAnalyzer,
the .NET analyzers) is satisfied. Fix issues rather than suppressing them.

## Contributing

Issues and PRs welcome. Adding a rule means: a `Rcgs####…Analyzer.cs`, a
matching `…CodeFixProvider.cs`, a `Rcgs####…AnalyzersUnitTest.cs` (markup-based
`CSharpCodeFixVerifier` tests), a `docs/rules/RCGS####.md` page, and a row in
`AnalyzerReleases.Unshipped.md`.

## License

MIT — see [LICENSE](LICENSE).
