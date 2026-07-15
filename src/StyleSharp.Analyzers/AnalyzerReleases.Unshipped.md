; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1218 | Ordering | Warning | Sst1218OverloadsGroupedAnalyzer
SST1319 | Naming | Warning | Sst1319EnumNamingAnalyzer
SST1521 | Layout | Warning | Sst1521LineTooLongAnalyzer
SST1522 | Layout | Warning | Sst1522FileTooLongAnalyzer
SST1523 | Layout | Warning | Sst1523MethodTooLongAnalyzer
SST1524 | Layout | Warning | Sst1524SwitchSectionTooLongAnalyzer
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
SST1468 | Maintainability | Warning | NonShortCircuitOperatorAnalyzer
SST1469 | Maintainability | Warning | Sst1469ValueTypeNullComparisonAnalyzer
SST1470 | Maintainability | Warning | Sst1470RemoveRethrowOnlyCatchAnalyzer
SST1471 | Maintainability | Warning | Sst1471MagicNumberAnalyzer
SST1472 | Maintainability | Warning | Sst1472TooManyParametersAnalyzer
SST1473 | Maintainability | Warning | Sst1473FloatingPointEqualityAnalyzer
SST1474 | Maintainability | Warning | Sst1474IdenticalOperandsAnalyzer
SST1475 | Maintainability | Warning | Sst1475DuplicateConditionAnalyzer
SST1476 | Maintainability | Warning | IdenticalBranchesAnalyzer
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
SST1488 | Maintainability | Warning | ExceptionConstructorAnalyzer
SST1489 | Maintainability | Warning | ExceptionConstructorAnalyzer
SST1490 | Maintainability | Warning | Sst1490RedundantBaseListEntryAnalyzer
SST1491 | Maintainability | Warning | Sst1491RedundantModifierAnalyzer
SST1492 | Maintainability | Warning | Sst1492SelfAssignmentGuardAnalyzer
SST1493 | Maintainability | Warning | Sst1493MethodReturnsConstantAnalyzer
SST1494 | Maintainability | Warning | Sst1494RedundantDefaultArgumentAnalyzer
SST1495 | Maintainability | Warning | Sst1495ReferenceEqualityOnValueEqualTypeAnalyzer
SST1496 | Maintainability | Warning | Sst1496AbstractTypeWithoutAbstractMembersAnalyzer
SST1497 | Maintainability | Warning | Sst1497UnusedLocalAnalyzer
SST1498 | Maintainability | Warning | Sst1498PrivateMemberUsedOnlyByNestedTypeAnalyzer
SST1499 | Maintainability | Warning | Sst1499MutableStaticFieldAnalyzer
SST1904 | Concurrency | Warning | LockTargetAnalyzer
SST1905 | Concurrency | Warning | Sst1905AsyncVoidAnalyzer
SST1658 | Documentation | Warning | Sst1658NoRepeatedWordsAnalyzer
SST1659 | Documentation | Warning | Sst1659EmptyCommentAnalyzer
SST2008 | Modernization | Warning | Sst2008IsNotPatternAnalyzer
SST2009 | Modernization | Warning | Sst2009UseExceptionFilterAnalyzer
SST2010 | Modernization | Disabled | Sst2010UseTimeProviderAnalyzer
SST2011 | Modernization | Warning | Sst2011RecordInstantsInUtcAnalyzer
SST2012 | Modernization | Warning | Sst2012UseGuidEmptyAnalyzer
SST2013 | Modernization | Warning | Sst2013MergeNestedIfAnalyzer
SST2014 | Modernization | Warning | Sst2014AvoidGotoAnalyzer
SST2015 | Modernization | Warning | Sst2015IsolateIncrementAnalyzer
SST2016 | Modernization | Warning | Sst2016PreferDateTimeOffsetAnalyzer
SST2017 | Modernization | Warning | Sst2017UseDateOnlyOrTimeOnlyAnalyzer
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
SST2244 | ModernSyntax | Warning | Sst2244UppercaseLiteralSuffixAnalyzer
SST2245 | ModernSyntax | Warning | Sst2245UseWhileOverForAnalyzer
SST2300 | Design | Warning | Sst2300DisposePatternAnalyzer
SST2301 | Design | Warning | Sst2301EquatableTypeShouldBeSealedAnalyzer
SST2302 | Design | Warning | Sst2302InconsistentOperatorOverloadsAnalyzer
SST2303 | Design | Warning | Sst2303MisusedFlagsAttributeAnalyzer
SST2304 | Design | Warning | Sst2304EventHandlerSignatureAnalyzer
SST2305 | Design | Warning | Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer
SST2306 | Design | Warning | Sst2306ReturnEmptyCollectionNotNullAnalyzer
SST2307 | Design | Warning | Sst2307InferableTypeParameterAnalyzer
SST2308 | Design | Warning | Sst2308ObsoleteWithoutExplanationAnalyzer
SST2309 | Design | Warning | Sst2309OptionalParameterAnalyzer
SST2310 | Design | Warning | Sst2310ObsoleteCodeShouldBeRemovedAnalyzer
SST2311 | Design | Warning | Sst2311PublicConstantFieldAnalyzer
SST2312 | Design | Warning | Sst2312TypeInGlobalNamespaceAnalyzer
SST2313 | Design | Warning | Sst2313EnumStorageAnalyzer
SST2314 | Design | Warning | Sst2314ObsoleteWithoutDiagnosticIdAnalyzer
SST2315 | Design | Warning | Sst2315OwnsDisposableFieldAnalyzer
SST2316 | Design | Warning | Sst2316DisposeWithoutInterfaceAnalyzer
SST2317 | Design | Warning | Sst2317NativeResourceWithoutSafeHandleAnalyzer
SST2318 | Design | Disabled | Sst2318DuplicateMemberBodyAnalyzer
SST2319 | Design | Warning | Sst2319UnreachableOptionalDefaultAnalyzer
SST2320 | Design | Warning | Sst2320AmbiguousInheritedInterfaceMemberAnalyzer
SST2321 | Design | Warning | Sst2321LibraryProcessTerminationAnalyzer
SST2400 | Correctness | Warning | Sst2400SwappedArgumentsAnalyzer
SST2401 | Correctness | Warning | Sst2401CatchNullReferenceAnalyzer
SST2402 | Correctness | Warning | Sst2402StaticFieldWrittenInConstructorAnalyzer
SST2403 | Correctness | Warning | Sst2403ThisEscapesConstructorAnalyzer
SST2404 | Correctness | Warning | Sst2404IteratorValidatesTooLateAnalyzer
SST2405 | Correctness | Warning | Sst2405DebuggerDisplayNamesMissingMemberAnalyzer
SST2406 | Correctness | Warning | LoopConditionAnalyzer
SST2407 | Correctness | Warning | Sst2407EventNeverRaisedAnalyzer
SST2408 | Correctness | Warning | Sst2408StringBuilderNeverReadAnalyzer
SST2409 | Correctness | Warning | Sst2409ThrowsGeneralExceptionAnalyzer
SST2410 | Correctness | Warning | Sst2410DisposableNeverDisposedAnalyzer
SST2411 | Correctness | Warning | LoopConditionAnalyzer
SST2412 | Correctness | Warning | LoopConditionAnalyzer
SST2413 | Correctness | Warning | LoopConditionAnalyzer
SST2414 | Correctness | Warning | IdenticalBranchesAnalyzer
SST2415 | Correctness | Warning | NonShortCircuitOperatorAnalyzer
SST2416 | Correctness | Warning | Sst2416SignedRemainderTestAnalyzer
SST2417 | Correctness | Warning | Sst2417TransposedCompoundAssignmentAnalyzer
SST2418 | Correctness | Warning | Sst2418DiscardedImmutableResultAnalyzer
SST2419 | Correctness | Warning | Sst2419SelfCollectionOperationAnalyzer
SST2420 | Correctness | Warning | Sst2420IndexOfSkipsFirstAnalyzer
SST2421 | Correctness | Warning | Sst2421ReadonlyGenericFieldWriteAnalyzer
SST2422 | Correctness | Warning | Sst2422BackingFieldMismatchAnalyzer
SST2423 | Correctness | Warning | Sst2423DisposableReturnedFromUsingAnalyzer
SST2424 | Correctness | Warning | OverrideParameterContractAnalyzer
SST2425 | Correctness | Warning | Sst2425BaseCallDropsOptionalArgumentAnalyzer
SST2426 | Correctness | Warning | OverrideParameterContractAnalyzer
SST2427 | Correctness | Warning | Sst2427HidingGeneralOverloadAnalyzer
SST2428 | Correctness | Warning | Sst2428StaticInitializerReadsLaterFieldAnalyzer
SST2429 | Correctness | Warning | Sst2429AccessorIgnoresValueAnalyzer
SST2430 | Correctness | Warning | Sst2430SerializationCallbackSignatureAnalyzer
SST2431 | Correctness | Warning | Sst2431ToStringReturnsNullAnalyzer
SST2432 | Correctness | Warning | Sst2432RedundantGetTypeAnalyzer
SST2433 | Correctness | Warning | Sst2433CallerInfoParameterOrderAnalyzer
SST2434 | Correctness | Warning | Sst2434ArrayCovarianceAnalyzer
SST2435 | Correctness | Warning | Sst2435ValueEqualityFastPathAnalyzer
SST2436 | Correctness | Warning | Sst2436NullEventRaiseAnalyzer
SST2437 | Correctness | Warning | Sst2437RecursiveGenericInheritanceAnalyzer
SST2438 | Correctness | Warning | LoggerCallAnalyzer
SST2439 | Correctness | Warning | LoggerCallAnalyzer
SST2440 | Correctness | Warning | LoggerCallAnalyzer
SST2441 | Correctness | Warning | LoggerCallAnalyzer
SST2442 | Correctness | Warning | LoggerCallAnalyzer
SST2443 | Correctness | Warning | Sst2443LoggerCategoryAnalyzer
SST2444 | Correctness | Warning | Sst2444InvalidRegexPatternAnalyzer
SST2445 | Correctness | Warning | Sst2445CultureSensitiveDateFormatAnalyzer
SST2446 | Correctness | Warning | Sst2446DiscardedStreamReadAnalyzer
SST1119 | Readability | Warning | LiteralFormattingAnalyzer
SST1138 | Readability | Warning | EmptyCodeAnalyzer
SST1219 | Ordering | Warning | Sst1219DefaultSectionLastAnalyzer
SST2018 | Modernization | Warning | Sst2018RedundantNullCheckBesidePatternAnalyzer

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1101 | Readability | Disabled | Condensed into SST1117; instance member qualification is now one configurable rule.
SST1434 | Maintainability | Warning | Moved to PerformanceSharp.Analyzers as PSH1002; the rule is performance-motivated.
SST1900 | Concurrency | Warning | Moved to PerformanceSharp.Analyzers as PSH1300; the rule is performance-motivated.
SST2229 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1101; the rule is performance-motivated.
SST2230 | ModernSyntax | Warning | Moved to PerformanceSharp.Analyzers as PSH1102; the rule is performance-motivated.
SST2233 | ModernSyntax | Disabled | Moved to PerformanceSharp.Analyzers as PSH1100; the rule is performance-motivated.
