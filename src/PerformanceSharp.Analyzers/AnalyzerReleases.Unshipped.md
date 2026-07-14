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
PSH1011 | Allocations | Warning | Psh1011UseStateOverloadAnalyzer
PSH1012 | Allocations | Warning | Psh1012EqualityComparerDefaultAnalyzer
PSH1013 | Allocations | Warning | Psh1013Utf8SpanPropertyAnalyzer
PSH1014 | Allocations | Warning | Psh1014ReadonlyStructAnalyzer
PSH1015 | Allocations | Warning | Psh1015BoxingRoundTripCastAnalyzer
PSH1016 | Allocations | Warning | Psh1016UseBitwiseFlagTestAnalyzer
PSH1017 | Allocations | Warning | Psh1017PropertyCopiesCollectionAnalyzer
PSH1018 | Allocations | Warning | Psh1018RedundantParamsArrayAnalyzer
PSH1019 | Allocations | Warning | Psh1019UseAsSpanOverRangeIndexerAnalyzer
PSH1020 | Allocations | Warning | Psh1020PreferJaggedArraysAnalyzer
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
PSH1115 | Collections | Warning | Psh1115SingleProbeInsertAnalyzer
PSH1116 | Collections | Warning | Psh1116AlternateLookupAnalyzer
PSH1117 | Collections | Warning | Psh1117UseIsEmptyAnalyzer
PSH1118 | Collections | Warning | Psh1118TakeExtremeWithoutSortingAnalyzer
PSH1119 | Collections | Warning | Psh1119UseAnyOverCountAnalyzer
PSH1120 | Collections | Warning | Psh1120DoNotMaterializeToEnumerateAnalyzer
PSH1122 | Collections | Warning | Psh1122UseSortedSetExtremePropertyAnalyzer
PSH1124 | Collections | Warning | Psh1124UseLinkedListEndPropertyAnalyzer
PSH1125 | Collections | Warning | Psh1125MultipleEnumerationAnalyzer
PSH1126 | Collections | Warning | Psh1126UseAnyAsyncOverCountAsyncAnalyzer
PSH1127 | Collections | Warning | Psh1127ClearOverFillDefaultAnalyzer
PSH1200 | Strings | Warning | Psh1200AvoidCaseConversionComparisonAnalyzer
PSH1201 | Strings | Warning | Psh1201UseCharOverloadAnalyzer
PSH1202 | Strings | Warning | Psh1202StringBuilderAppendCharAnalyzer
PSH1203 | Strings | Warning | Psh1203StringBuilderInnerAllocationAnalyzer
PSH1204 | Strings | Warning | Psh1204EmptyStringComparisonAnalyzer
PSH1205 | Strings | Warning | Psh1205RedundantInterpolatedStringAnalyzer
PSH1206 | Strings | Warning | Psh1206StringConcatenationInLoopAnalyzer
PSH1207 | Strings | Warning | Psh1207SpecifyStringComparisonAnalyzer
PSH1208 | Strings | Warning | Psh1208Utf8LiteralAnalyzer
PSH1209 | Strings | Warning | Psh1209StringCreateAnalyzer
PSH1210 | Strings | Warning | Psh1210Utf8SequenceEqualAnalyzer
PSH1211 | Strings | Warning | Psh1211RemoveIntermediateToStringAnalyzer
PSH1212 | Strings | Warning | Psh1212AsSpanOverSubstringAnalyzer
PSH1213 | Strings | Warning | Psh1213UseSearchValuesAnalyzer
PSH1214 | Strings | Warning | Psh1214SplitConcatenatedAppendAnalyzer
PSH1215 | Strings | Warning | Psh1215UseConcatOverEmptyJoinAnalyzer
PSH1216 | Strings | Warning | Psh1216UseEqualsOverCompareAnalyzer
PSH1217 | Strings | Warning | Psh1217RedundantSequenceCopyAnalyzer
PSH1218 | Strings | Warning | Psh1218SearchWithStartIndexAnalyzer
PSH1219 | Strings | Warning | Psh1219UseIsNullOrWhiteSpaceAnalyzer
PSH1220 | Strings | Warning | Psh1220RedundantLengthArgumentAnalyzer
PSH1221 | Strings | Warning | Psh1221UseStartsWithOverIndexOfAnalyzer
PSH1222 | Strings | Warning | Psh1222UseSpanBasedConcatAnalyzer
PSH1223 | Strings | Warning | Psh1223UseCompositeFormatAnalyzer
PSH1224 | Strings | Warning | Psh1224UseConvertToHexStringAnalyzer
PSH1225 | Strings | Warning | Psh1225UseEncodingGetStringAnalyzer
PSH1300 | Concurrency | Warning | Psh1300PreferLockTypeAnalyzer (moved from StyleSharp.Analyzers SST1900)
PSH1301 | Concurrency | Warning | Psh1301AwaitSingleTaskDirectlyAnalyzer
PSH1302 | Concurrency | Warning | Psh1302RunContinuationsAsynchronouslyAnalyzer
PSH1303 | Concurrency | Warning | Psh1303NoThreadSleepInAsyncAnalyzer
PSH1304 | Concurrency | Warning | Psh1304UsePeriodicTimerAnalyzer
PSH1305 | Concurrency | Warning | Psh1305NoConcurrentSnapshotEnumerationAnalyzer
PSH1306 | Concurrency | Disabled | Psh1306InterlockedOnceGuardAnalyzer
PSH1307 | Concurrency | Warning | Psh1307VolatileInterlockedFieldAnalyzer
PSH1308 | Concurrency | Warning | Psh1308CompletedTaskAnalyzer
PSH1309 | Concurrency | Disabled | Psh1309UnsafeRegisterAnalyzer
PSH1310 | Concurrency | Warning | Psh1310UseAwaitUsingAnalyzer
PSH1311 | Concurrency | Warning | Psh1311RemovePassThroughStateMachineAnalyzer
PSH1312 | Concurrency | Warning | Psh1312ReturnCompletedTaskOverNullAnalyzer
PSH1313 | Concurrency | Warning | Psh1313CallAsyncInAsyncContextAnalyzer
PSH1314 | Concurrency | Warning | Psh1314UseMemoryBasedStreamOverloadsAnalyzer
PSH1315 | Concurrency | Warning | Psh1315NoBlockingWaitAnalyzer
PSH1400 | ApiSelection | Warning | Psh1400PreferStaticHashDataAnalyzer
PSH1401 | ApiSelection | Warning | Psh1401SealAttributeTypesAnalyzer
PSH1402 | ApiSelection | Warning | Psh1402PreferConstOverStaticReadonlyAnalyzer
PSH1403 | ApiSelection | Warning | Psh1403RemoveRedundantDefaultInitializationAnalyzer
PSH1404 | ApiSelection | Warning | Psh1404PreferTypeofAssemblyAnalyzer
PSH1405 | ApiSelection | Warning | Psh1405UseEnvironmentPropertiesAnalyzer
PSH1406 | ApiSelection | Warning | Psh1406UseDirectRegexQueriesAnalyzer
PSH1407 | ApiSelection | Warning | Psh1407UseContainsKeyOverKeysContainsAnalyzer
PSH1408 | ApiSelection | Warning | Psh1408UseStopwatchTimestampsAnalyzer
PSH1409 | ApiSelection | Warning | Psh1409ThrowHelperAnalyzer
PSH1410 | ApiSelection | Disabled | Psh1410AggressiveInliningAnalyzer
PSH1411 | ApiSelection | Warning | Psh1411SealNonDerivedTypeAnalyzer
PSH1412 | ApiSelection | Warning | Psh1412UseSharedRandomAnalyzer
PSH1413 | ApiSelection | Warning | Psh1413UseUnixEpochFieldAnalyzer
PSH1414 | ApiSelection | Warning | Psh1414MarkMembersStaticAnalyzer
PSH1415 | ApiSelection | Warning | Psh1415UseConcreteTypeAnalyzer
PSH1416 | ApiSelection | Warning | Psh1416CacheSerializerOptionsAnalyzer
PSH1417 | ApiSelection | Warning | Psh1417ExpensiveDebugAssertArgumentAnalyzer
