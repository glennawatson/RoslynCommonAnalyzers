# RoslynCommonAnalyzers

A set of Roslyn analyzers and code fixes that enforce common readability conventions, distributed as the `Blazor.Common.Analyzers` NuGet package.

## What it does

The analyzers make sure that parameter and argument lists are formatted consistently: either every parameter/argument is on the same line, or each one is on its own line. "Jagged" layouts (some on one line, some wrapped) are reported, and an accompanying code fix rewrites them onto separate lines.

| Rule | Applies to |
| --- | --- |
| RCGS0001 | Constructor declarations |
| RCGS0002 | Method declarations |
| RCGS0003 | Delegate declarations |
| RCGS0004 | Indexer declarations |
| RCGS0005 | Invocation expressions |
| RCGS0006 | Object creation expressions |
| RCGS0007 | Element access expressions |
| RCGS0008 | Attribute arguments |
| RCGS0009 | Anonymous method expressions |
| RCGS0010 | Parenthesized lambda expressions |

## Installation

```sh
dotnet add package Blazor.Common.Analyzers
```

## Building

```sh
dotnet build Blazor.Common.Analyzers.sln
dotnet test Blazor.Common.Analyzers.sln
```

Analyzer and code-fix projects target `netstandard2.0` and are built against the Roslyn version that ships with Visual Studio 2022 17.14 (`Microsoft.CodeAnalysis.* 4.14.0`). Tests run on TUnit.

## License

MIT — see [LICENSE](LICENSE).
