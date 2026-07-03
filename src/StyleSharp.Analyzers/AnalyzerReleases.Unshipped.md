; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1442 | Maintainability | Warning | FunctionComplexityAnalyzer
SST1443 | Maintainability | Warning | FunctionComplexityAnalyzer
SST1444 | Maintainability | Warning | SingleIterationLoopAnalyzer
SST1445 | Maintainability | Warning | Sst1445UnnecessaryUsingDirectiveAnalyzer
SST1446 | Maintainability | Warning | Sst1446InheritanceDepthAnalyzer
SST1447 | Maintainability | Warning | Sst1447BaseObjectEqualityDelegationAnalyzer
SST1448 | Maintainability | Warning | Sst1448CallerInfoArgumentAnalyzer
SST1449 | Maintainability | Warning | Sst1449NoConsoleOutputAnalyzer
SST1451 | Maintainability | Warning | Sst1451DateTimeKindAnalyzer
SST1452 | Maintainability | Warning | Sst1452UnusedTypeParameterAnalyzer
SST2234 | ModernSyntax | Warning | Sst2234NullableShorthandAnalyzer

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1101 | Readability | Disabled | Condensed into SST1117; instance member qualification is now one configurable rule.
SST1434 | Maintainability | Warning | Moved to PerformanceSharp.Analyzers as PSH1002; the rule is performance-motivated.
SST1900 | Concurrency | Warning | Moved to PerformanceSharp.Analyzers as PSH1300; the rule is performance-motivated.
SST2229 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1101; the rule is performance-motivated.
SST2230 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1102; the rule is performance-motivated.
SST2233 | ModernSyntax | Disabled | Moved to PerformanceSharp.Analyzers as PSH1100; the rule is performance-motivated.
