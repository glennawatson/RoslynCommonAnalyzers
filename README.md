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

`StyleSharp.Analyzers` is a performance-focused Roslyn analyzer and code-fix package for .NET codebases. What started as the unique-line list family has grown into a broader ruleset covering spacing, readability, ordering, layout, naming, maintainability, documentation, extension blocks, records, concurrency, modernization, collection expressions, and modern C# syntax.

The package is analyzer-only, targets `netstandard2.0`, ships as a `DevelopmentDependency`, and packs analyzer/code-fix assemblies for multiple Roslyn slots under one NuGet package.

## Installation

```bash
dotnet add package StyleSharp.Analyzers
```

The package adds no runtime assemblies to your output and is not transitive to consumers of your library.

## Documentation

- Full rule catalog: [`docs/README.md`](docs/README.md)
- Configuration: [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md)
- Performance guidance: [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md)
- Ready-to-use preset: [`recommended.editorconfig`](recommended.editorconfig)

The rule catalog is intentionally split out of this README so the GitHub landing page stays readable while the detailed rule index can grow with the project.

## Rule Categories

- `Spacing` for token and whitespace conventions
- `Readability` for query layout, unique-line lists, tuple/null/style conventions, expression simplification, redundant-code removal, and similar readability rules
- `Ordering` for using ordering, modifier ordering, accessor ordering, and member ordering
- `Layout` for brace placement, blank-line rules, and block consistency
- `Naming` for .NET naming conventions
- `Maintainability` for access modifiers, precedence, auto-properties, nameof, trailing commas, redundant modifiers, and related rules
- `Documentation` for XML docs, file headers, and documentation quality rules
- `Extensions` for C# 14 extension-block conventions
- `Records` for record and record-struct conventions
- `Concurrency` for lock usage rules
- `Modernization` for runtime throw-helper adoption
- `CollectionExpressions` for collection-expression usage
- `ModernSyntax` for new language features such as the C# 14 `field` keyword

Unless a rule is marked opt-in, it is enabled by default at `Warning` severity.

## Configuration

StyleSharp is configured entirely through `.editorconfig`. Severity uses the standard `dotnet_diagnostic.<RuleId>.severity` keys, and rule options use the compiler-provided analyzer config system.

```ini
[*.cs]
dotnet_diagnostic.SST1309.severity = warning
stylesharp.tuple_element_naming = pascal_case
stylesharp.union_member_naming = pascal_case
```

See [`docs/CONFIGURATION.md`](docs/CONFIGURATION.md) for the full option list and the recommended configuration approach.

## Repository Layout

| Project | Purpose |
| --- | --- |
| `src/StyleSharp.Analyzers` | analyzer implementations |
| `src/StyleSharp.Analyzers.CodeFixes` | code-fix implementations |
| `src/StyleSharp.Analyzers.Package` | NuGet packaging |
| `src/tests/StyleSharp.Analyzers.Tests` | TUnit + `Microsoft.CodeAnalysis.Testing` test suite |
| `src/benchmarks/StyleSharp.Analyzers.Benchmarks` | BenchmarkDotNet perf harness |

## Building And Testing

Run these commands from `src/`:

```bash
dotnet build StyleSharp.Analyzers.slnx -c Release
dotnet test --project tests/StyleSharp.Analyzers.Tests/StyleSharp.Analyzers.Tests.csproj -c Release
```

To pack every Roslyn slot into the published NuGet package:

```bash
dotnet pack StyleSharp.Analyzers.Package/StyleSharp.Analyzers.Packages.csproj -c Release
```

## Contributing

Issues and pull requests are welcome.

When adding a rule, update all of the following:

- analyzer implementation
- code-fix implementation if the rule is fixable
- tests
- `docs/rules/SST####.md`
- `src/StyleSharp.Analyzers/AnalyzerReleases.Unshipped.md`
- `recommended.editorconfig` if the rule should appear in the preset

Performance is a first-class requirement. Read [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) before changing analyzer hot paths, and benchmark changes rather than guessing.

## License

MIT — see [LICENSE](LICENSE).

## Acknowledgements

This project was inspired by [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers). The analyzers and code fixes here are written from scratch, and no StyleCop.Analyzers source code was copied. Some diagnostic IDs, titles, and messages do mirror StyleCop's for familiarity, so StyleCop.Analyzers' MIT license is included in [LICENSE](LICENSE).

Thanks to Sam Harwell and the StyleCop.Analyzers contributors. Their work set the standard this project learned from.
