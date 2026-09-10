; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PSH1024 | Allocations | Warning | Build a BitArray from a span instead of a temporary array
PSH1318 | Concurrency | Warning | Validate options asynchronously instead of blocking
