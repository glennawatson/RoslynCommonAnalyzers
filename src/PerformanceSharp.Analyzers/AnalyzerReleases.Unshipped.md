; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PSH1000 | Allocations | Warning | Psh1000StaticAnonymousFunctionAnalyzer
PSH1001 | Allocations | Warning | Psh1001UseArrayEmptyAnalyzer
PSH1002 | Allocations | Warning | Psh1002EmptyFinalizerAnalyzer (moved from StyleSharp.Analyzers SST1434)
PSH1003 | Allocations | Warning | Psh1003InParameterWithNonReadonlyStructAnalyzer
PSH1004 | Allocations | Warning | Psh1004HoistConstantArrayArgumentsAnalyzer
PSH1005 | Allocations | Warning | Psh1005ValueTypeEqualityBoxesAnalyzer
PSH1006 | Allocations | Warning | Psh1006ConcurrentDictionaryClosureCaptureAnalyzer
PSH1007 | Allocations | Warning | Psh1007PassLargeReadonlyStructByInAnalyzer
PSH1008 | Allocations | Warning | Psh1008UselessSuppressFinalizeAnalyzer
PSH1009 | Allocations | Warning | Psh1009UnboundedStackallocAnalyzer
PSH1010 | Allocations | Warning | Psh1010ClearPooledReferenceArraysAnalyzer
PSH1100 | Collections | Disabled | LinqUsageAnalyzer (moved from StyleSharp.Analyzers SST2233)
PSH1101 | Collections | Warning | LinqUsageAnalyzer (moved from StyleSharp.Analyzers SST2229)
PSH1102 | Collections | Warning | LinqUsageAnalyzer (moved from StyleSharp.Analyzers SST2230)
PSH1103 | Collections | Warning | Psh1103UseCountPropertyAnalyzer
PSH1104 | Collections | Warning | Psh1104UseTryGetValueAnalyzer
PSH1105 | Collections | Warning | Psh1105AvoidDoubleLookupAnalyzer
PSH1106 | Collections | Warning | Psh1106UseIndexerForElementAccessAnalyzer
PSH1107 | Collections | Warning | LinqChainAnalyzer
PSH1108 | Collections | Warning | LinqChainAnalyzer
PSH1109 | Collections | Warning | LinqChainAnalyzer
PSH1110 | Collections | Warning | CollectionNativeMethodAnalyzer
PSH1111 | Collections | Warning | CollectionNativeMethodAnalyzer
PSH1112 | Collections | Warning | Psh1112SeedCollectionFromSourceAnalyzer
PSH1113 | Collections | Warning | Psh1113UseNaturalOrderAnalyzer
PSH1114 | Collections | Disabled | Psh1114FreezeStaticLookupsAnalyzer
PSH1200 | Strings | Warning | Psh1200AvoidCaseConversionComparisonAnalyzer
PSH1201 | Strings | Warning | Psh1201UseCharOverloadAnalyzer
PSH1202 | Strings | Warning | Psh1202StringBuilderAppendCharAnalyzer
PSH1203 | Strings | Warning | Psh1203StringBuilderInnerAllocationAnalyzer
PSH1204 | Strings | Warning | Psh1204EmptyStringComparisonAnalyzer
PSH1205 | Strings | Warning | Psh1205RedundantInterpolatedStringAnalyzer
PSH1206 | Strings | Warning | Psh1206StringConcatenationInLoopAnalyzer
PSH1207 | Strings | Warning | Psh1207SpecifyStringComparisonAnalyzer
PSH1300 | Concurrency | Warning | Psh1300PreferLockTypeAnalyzer (moved from StyleSharp.Analyzers SST1900)
PSH1301 | Concurrency | Warning | Psh1301AwaitSingleTaskDirectlyAnalyzer
PSH1302 | Concurrency | Warning | Psh1302RunContinuationsAsynchronouslyAnalyzer
PSH1303 | Concurrency | Warning | Psh1303NoThreadSleepInAsyncAnalyzer
PSH1304 | Concurrency | Warning | Psh1304UsePeriodicTimerAnalyzer
PSH1305 | Concurrency | Warning | Psh1305NoConcurrentSnapshotEnumerationAnalyzer
PSH1400 | ApiSelection | Warning | Psh1400PreferStaticHashDataAnalyzer
PSH1401 | ApiSelection | Warning | Psh1401SealAttributeTypesAnalyzer
PSH1402 | ApiSelection | Warning | Psh1402PreferConstOverStaticReadonlyAnalyzer
PSH1403 | ApiSelection | Warning | Psh1403RemoveRedundantDefaultInitializationAnalyzer
PSH1404 | ApiSelection | Warning | Psh1404PreferTypeofAssemblyAnalyzer
PSH1405 | ApiSelection | Warning | Psh1405UseEnvironmentPropertiesAnalyzer
PSH1406 | ApiSelection | Warning | Psh1406UseDirectRegexQueriesAnalyzer
PSH1407 | ApiSelection | Warning | Psh1407UseContainsKeyOverKeysContainsAnalyzer
PSH1408 | ApiSelection | Warning | Psh1408UseStopwatchTimestampsAnalyzer
