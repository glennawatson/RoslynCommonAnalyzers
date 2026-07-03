[![CI Build](https://github.com/glennawatson/RoslynCommonAnalyzers/actions/workflows/ci.yml/badge.svg)](https://github.com/glennawatson/RoslynCommonAnalyzers/actions/workflows/ci.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_RoslynCommonAnalyzers&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_RoslynCommonAnalyzers)
[![NuGet](https://img.shields.io/nuget/v/StyleSharp.Analyzers.svg?logo=nuget&label=StyleSharp.Analyzers)](https://www.nuget.org/packages/StyleSharp.Analyzers/)
[![Downloads](https://img.shields.io/nuget/dt/StyleSharp.Analyzers.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/StyleSharp.Analyzers/)
[![NuGet](https://img.shields.io/nuget/v/PerformanceSharp.Analyzers.svg?logo=nuget&label=PerformanceSharp.Analyzers)](https://www.nuget.org/packages/PerformanceSharp.Analyzers/)
[![Downloads](https://img.shields.io/nuget/dt/PerformanceSharp.Analyzers.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/PerformanceSharp.Analyzers/)
[![GitHub stars](https://img.shields.io/github/stars/glennawatson/RoslynCommonAnalyzers?style=social)](https://github.com/glennawatson/RoslynCommonAnalyzers/stargazers)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>
<a href="https://github.com/glennawatson/RoslynCommonAnalyzers">
  <img width="160" height="160" src="https://raw.githubusercontent.com/glennawatson/RoslynCommonAnalyzers/main/icons/icon.png" alt="StyleSharp.Analyzers">
</a>
<br>

# RoslynCommonAnalyzers

Two fast Roslyn analyzer and code-fix packages for .NET codebases, built from one repository:

- **`StyleSharp.Analyzers`** (`SST####`) — style and consistency: spacing, readability, ordering, layout, naming, maintainability, documentation, extension blocks, records, lock-target safety, modernization, collection expressions, and modern C# syntax.
- **`PerformanceSharp.Analyzers`** (`PSH####`) — runtime performance of *your* code: avoidable allocations, collection and LINQ enumeration costs, string handling, concurrency and async patterns, and cheaper API selection.

Both packages are analyzer-only, target `netstandard2.0`, ship as a `DevelopmentDependency`, and pack analyzer/code-fix assemblies for multiple Roslyn slots under one NuGet package each. The analyzers themselves are engineered for build-time speed: allocation-free no-diagnostic paths, syntax-first candidate filtering, and per-rule benchmarks.

## Installation

```bash
dotnet add package StyleSharp.Analyzers
dotnet add package PerformanceSharp.Analyzers
```

Each package adds no runtime assemblies to your output and is not transitive to consumers of your library. They are independent — install either or both.

## Documentation

- Full rule catalog (both packages): [`docs/README.md`](docs/README.md)
- Configuration: [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)
- Performance guidance: [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md)
- Ready-to-use presets: [`recommended.editorconfig`](recommended.editorconfig) (StyleSharp) and [`recommended-performancesharp.editorconfig`](recommended-performancesharp.editorconfig) (PerformanceSharp)

The rule catalog is intentionally split out of this README so the GitHub landing page stays readable while the detailed rule index can grow with the project.

## Rule Categories

### StyleSharp (`SST####`)

- `Spacing` for token and whitespace conventions
- `Readability` for query layout, unique-line lists, tuple/null/style conventions, expression simplification, redundant-code removal, and similar readability rules
- `Ordering` for using ordering, modifier ordering, accessor ordering, and member ordering
- `Layout` for brace placement, blank-line rules, and block consistency
- `Naming` for .NET naming conventions, async-method suffixes, and override parameter names
- `Maintainability` for access modifiers, precedence, auto-properties, nameof, trailing commas, redundant modifiers, exception handling, type design, and related rules
- `Documentation` for XML docs, file headers, and documentation quality rules
- `Extensions` for C# 14 extension-block conventions
- `Records` for record and record-struct conventions
- `Concurrency` for lock-target safety rules
- `Modernization` for runtime throw-helper adoption and pattern-matching forms
- `CollectionExpressions` for collection-expression usage
- `ModernSyntax` for new language features such as the C# 14 `field` keyword

### PerformanceSharp (`PSH####`)

- `Allocations` (`PSH10xx`) for closure/delegate allocations, throwaway empty arrays, and empty finalizers
- `Collections` (`PSH11xx`) for LINQ on hot paths, redundant iterator layers, O(1) count/indexer alternatives, and repeated dictionary/set lookups
- `Strings` (`PSH12xx`) for case-conversion comparison allocations, char overloads, and `StringBuilder` patterns
- `Concurrency` (`PSH13xx`) for `System.Threading.Lock` adoption and async/concurrency perf patterns
- `ApiSelection` (`PSH14xx`) for cheaper framework APIs such as one-shot `HashData`

Rules whose primary motivation is runtime performance live in PerformanceSharp; rules about style and consistency live in StyleSharp. Unless a rule is marked opt-in, it is enabled by default at `Warning` severity.

## Configuration

Both packages are configured entirely through `.editorconfig`. Severity uses the standard `dotnet_diagnostic.<RuleId>.severity` keys, and rule options use the compiler-provided analyzer config system with a per-package prefix (`stylesharp.` / `performancesharp.`).

```ini
[*.cs]
dotnet_diagnostic.SST1309.severity = warning
stylesharp.tuple_element_naming = pascal_case

dotnet_diagnostic.PSH1100.severity = warning
performancesharp.avoid_linq_on_hot_path = true
```

See [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md) for the full option list and the recommended configuration approach.

## Repository Layout

| Project | Purpose |
| --- | --- |
| `src/StyleSharp.Analyzers` / `src/PerformanceSharp.Analyzers` | analyzer implementations |
| `src/StyleSharp.Analyzers.CodeFixes` / `src/PerformanceSharp.Analyzers.CodeFixes` | code-fix implementations |
| `src/StyleSharp.Analyzers.Package` / `src/PerformanceSharp.Analyzers.Package` | NuGet packaging |
| `src/tests/*` | TUnit + `Microsoft.CodeAnalysis.Testing` test suites |
| `src/benchmarks/*` | BenchmarkDotNet perf harnesses |

## Building And Testing

Run these commands from `src/`:

```bash
dotnet build RoslynCommonAnalyzers.slnx -c Release
dotnet test --project tests/StyleSharp.Analyzers.Tests/StyleSharp.Analyzers.Tests.csproj -c Release
dotnet test --project tests/PerformanceSharp.Analyzers.Tests/PerformanceSharp.Analyzers.Tests.csproj -c Release
```

To pack every Roslyn slot into the published NuGet packages:

```bash
dotnet pack StyleSharp.Analyzers.Package/StyleSharp.Analyzers.Packages.csproj -c Release
dotnet pack PerformanceSharp.Analyzers.Package/PerformanceSharp.Analyzers.Packages.csproj -c Release
```

## Contributing

Issues and pull requests are welcome.

When adding a rule, update all of the following (in whichever package the rule belongs to):

- analyzer implementation
- code-fix implementation if the rule is fixable
- tests
- `docs/rules/SST####.md` or `docs/rules/PSH####.md`
- that package's `AnalyzerReleases.Unshipped.md`
- the matching preset (`recommended.editorconfig` / `recommended-performancesharp.editorconfig`) if the rule should appear there

Performance is a first-class requirement. Read [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) before changing analyzer hot paths, and benchmark changes rather than guessing.

## License

MIT — see [LICENSE](LICENSE).

## Acknowledgements

The analyzers and code fixes here are written from scratch. No source code was copied from the projects below. License references are included in [LICENSE](LICENSE) where applicable.

- [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers), licensed under the MIT License, inspired the original shape of the project. Thanks to Sam Harwell and the StyleCop.Analyzers contributors.
- [Roslynator](https://github.com/dotnet/roslynator), licensed under the Apache License 2.0, inspired some diagnostic design choices. Thanks to Josef Pihrt and the Roslynator contributors.
- The .NET SDK analyzer guidance and [dotnet/roslyn](https://github.com/dotnet/roslyn), licensed under the MIT License, inspired parts of the style-rule coverage. Thanks to the .NET and Roslyn teams.
