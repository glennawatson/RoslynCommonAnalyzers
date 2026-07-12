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
SST1464 | Maintainability | Warning | Sst1464UnwrapElseAfterJumpAnalyzer
SST1465 | Maintainability | Warning | Sst1465CollapseElseIntoElseIfAnalyzer
SST1466 | Maintainability | Warning | Sst1466RemoveCaseBesideDefaultAnalyzer
SST1467 | Maintainability | Warning | Sst1467UseForeachOverManualEnumeratorAnalyzer
SST1468 | Maintainability | Warning | Sst1468UseShortCircuitOperatorAnalyzer
SST1469 | Maintainability | Warning | Sst1469ValueTypeNullComparisonAnalyzer
SST1470 | Maintainability | Warning | Sst1470RemoveRethrowOnlyCatchAnalyzer
SST1471 | Maintainability | Warning | Sst1471MagicNumberAnalyzer
SST1472 | Maintainability | Warning | Sst1472TooManyParametersAnalyzer
SST1473 | Maintainability | Warning | Sst1473FloatingPointEqualityAnalyzer
SST1474 | Maintainability | Warning | Sst1474IdenticalOperandsAnalyzer
SST1475 | Maintainability | Warning | Sst1475DuplicateConditionAnalyzer
SST1476 | Maintainability | Warning | Sst1476IdenticalBranchesAnalyzer
SST1477 | Maintainability | Warning | Sst1477IntegerDivisionAsFloatingPointAnalyzer
SST1478 | Maintainability | Warning | Sst1478SuspiciousShiftCountAnalyzer
SST1479 | Maintainability | Warning | Sst1479MeaninglessCountComparisonAnalyzer
SST1480 | Maintainability | Warning | Sst1480ExceptionNeverThrownAnalyzer
SST1481 | Maintainability | Warning | Sst1481RedundantBitwiseOperationAnalyzer
SST1482 | Maintainability | Warning | Sst1482MutableGetHashCodeAnalyzer
SST1483 | Maintainability | Warning | Sst1483VirtualCallInConstructorAnalyzer
SST1484 | Maintainability | Warning | Sst1484ShadowedDeclarationAnalyzer
SST1485 | Maintainability | Warning | Sst1485UnexpectedThrowAnalyzer
SST1486 | Maintainability | Warning | Sst1486DuplicatedStringLiteralAnalyzer
SST1487 | Maintainability | Warning | Sst1487OverwrittenCollectionElementAnalyzer
SST1658 | Documentation | Warning | Sst1658NoRepeatedWordsAnalyzer
SST2008 | Modernization | Warning | Sst2008IsNotPatternAnalyzer
SST2009 | Modernization | Warning | Sst2009UseExceptionFilterAnalyzer
SST2234 | ModernSyntax | Warning | Sst2234NullableShorthandAnalyzer
SST2235 | ModernSyntax | Warning | Sst2235StaticLocalFunctionAnalyzer
SST2236 | ModernSyntax | Warning | Sst2236UsingDeclarationAnalyzer
SST2237 | ModernSyntax | Warning | Sst2237FileScopedNamespaceAnalyzer
SST2238 | ModernSyntax | Warning | Sst2238NestedPropertyPatternAnalyzer
SST2239 | ModernSyntax | Warning | Sst2239MethodGroupAnalyzer
SST2240 | ModernSyntax | Warning | Sst2240ConditionalDelegateInvocationAnalyzer
SST2241 | ModernSyntax | Warning | Sst2241PrimaryConstructorStorageAnalyzer
SST2242 | ModernSyntax | Warning | Sst2242EnumSwitchStatementMappingAnalyzer
SST2243 | ModernSyntax | Warning | Sst2243UseRawStringLiteralAnalyzer

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1101 | Readability | Disabled | Condensed into SST1117; instance member qualification is now one configurable rule.
SST1434 | Maintainability | Warning | Moved to PerformanceSharp.Analyzers as PSH1002; the rule is performance-motivated.
SST1900 | Concurrency | Warning | Moved to PerformanceSharp.Analyzers as PSH1300; the rule is performance-motivated.
SST2229 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1101; the rule is performance-motivated.
SST2230 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1102; the rule is performance-motivated.
SST2233 | ModernSyntax | Disabled | Moved to PerformanceSharp.Analyzers as PSH1100; the rule is performance-motivated.
