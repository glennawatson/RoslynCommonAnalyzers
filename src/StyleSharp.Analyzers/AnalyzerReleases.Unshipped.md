; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1710 | Extensions | Warning | Prefer an extension indexer over an accessor-shaped extension method
SST2106 | CollectionExpressions | Warning | Pass the capacity or comparer through a collection expression
SST2289 | ModernSyntax | Info | This unsafe block no longer needs an unsafe context
SST2337 | Design | Warning | Declare a fully known hierarchy as closed
SST2338 | Design | Warning | Declare a hand-rolled discriminated type as a union
SST2499 | Correctness | Warning | Read the exit status rather than the exit code when a process can be signalled
