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
SST1453 | Maintainability | Warning | Sst1453UnreachableCodeAnalyzer
SST1454 | Maintainability | Warning | Sst1454CompositeFormatStringAnalyzer
SST1455 | Maintainability | Warning | Sst1455UnnecessaryUnsafeModifierAnalyzer
SST1456 | Maintainability | Warning | Sst1456ReadonlyMutableStructFieldAnalyzer
SST1457 | Maintainability | Warning | GlobalSuppressionTargetAnalyzer
SST1458 | Maintainability | Warning | GlobalSuppressionTargetAnalyzer
SST1459 | Maintainability | Warning | Sst1459UnnecessaryParenthesesAnalyzer
SST1460 | Maintainability | Warning | Sst1460ReadonlyStructMemberAnalyzer
SST1461 | Maintainability | Warning | Sst1461UnusedParameterAnalyzer
SST1462 | Maintainability | Warning | Sst1462DisabledDiagnosticSuppressionAnalyzer
SST1463 | Maintainability | Warning | Sst1463NameofLiteralAnalyzer
SST2008 | Modernization | Warning | Sst2008IsNotPatternAnalyzer
SST2234 | ModernSyntax | Warning | Sst2234NullableShorthandAnalyzer
SST2235 | ModernSyntax | Warning | Sst2235StaticLocalFunctionAnalyzer
SST2236 | ModernSyntax | Warning | Sst2236UsingDeclarationAnalyzer
SST2237 | ModernSyntax | Warning | Sst2237FileScopedNamespaceAnalyzer
SST2238 | ModernSyntax | Warning | Sst2238NestedPropertyPatternAnalyzer
SST2239 | ModernSyntax | Warning | Sst2239MethodGroupAnalyzer
SST2240 | ModernSyntax | Warning | Sst2240ConditionalDelegateInvocationAnalyzer
SST2241 | ModernSyntax | Warning | Sst2241PrimaryConstructorStorageAnalyzer
SST2242 | ModernSyntax | Warning | Sst2242EnumSwitchStatementMappingAnalyzer

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1101 | Readability | Disabled | Condensed into SST1117; instance member qualification is now one configurable rule.
SST1434 | Maintainability | Warning | Moved to PerformanceSharp.Analyzers as PSH1002; the rule is performance-motivated.
SST1900 | Concurrency | Warning | Moved to PerformanceSharp.Analyzers as PSH1300; the rule is performance-motivated.
SST2229 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1101; the rule is performance-motivated.
SST2230 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1102; the rule is performance-motivated.
SST2233 | ModernSyntax | Disabled | Moved to PerformanceSharp.Analyzers as PSH1100; the rule is performance-motivated.
