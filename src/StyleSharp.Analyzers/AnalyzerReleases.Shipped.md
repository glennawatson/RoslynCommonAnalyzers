; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1150 | Readability | Warning | SST1150ConstructorDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1151 | Readability | Warning | SST1151MethodDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1152 | Readability | Warning | SST1152DelegateDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1153 | Readability | Warning | SST1153IndexerDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1154 | Readability | Warning | SST1154InvocationExpressionArgumentMustBeOnUniqueLinesAnalyzer
SST1155 | Readability | Warning | SST1155ObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer
SST1156 | Readability | Warning | SST1156ElementAccessExpressionArgumentMustBeOnUniqueLinesAnalyzer
SST1157 | Readability | Warning | SST1157AttributeArgumentMustBeOnUniqueLinesAnalyzer
SST1158 | Readability | Warning | SST1158AnonymousMethodExpressionParameterMustBeOnUniqueLinesAnalyzer
SST1159 | Readability | Warning | SST1159ParenthesizedLambdaExpressionParameterMustBeOnUniqueLinesAnalyzer
SST1160 | Readability | Warning | SST1160RecordDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1161 | Readability | Warning | SST1161ClassDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1162 | Readability | Warning | SST1162StructDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1163 | Readability | Warning | SST1163ImplicitObjectCreationExpressionArgumentMustBeOnUniqueLinesAnalyzer
SST1164 | Readability | Warning | SST1164ConstructorInitializerArgumentMustBeOnUniqueLinesAnalyzer
SST1165 | Readability | Warning | SST1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer
SST1166 | Readability | Warning | SST1166LocalFunctionStatementParameterMustBeOnUniqueLinesAnalyzer
SST1167 | Readability | Warning | SST1167OperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1168 | Readability | Warning | SST1168ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer
SST1169 | Readability | Warning | SST1169TypeParameterListMustBeOnUniqueLinesAnalyzer
SST1170 | Readability | Warning | SST1170TypeArgumentListMustBeOnUniqueLinesAnalyzer
SST1171 | Readability | Warning | SST1171FunctionPointerParameterListMustBeOnUniqueLinesAnalyzer
SST1300 | Naming | Warning | ElementNamingAnalyzer
SST1302 | Naming | Warning | SST1302InterfaceNamesMustBeginWithIAnalyzer
SST1303 | Naming | Warning | FieldNamingAnalyzer
SST1304 | Naming | Warning | FieldNamingAnalyzer
SST1307 | Naming | Warning | FieldNamingAnalyzer
SST1309 | Naming | Warning | FieldNamingAnalyzer
SST1311 | Naming | Warning | FieldNamingAnalyzer
SST1312 | Naming | Warning | LocalVariableNamingAnalyzer
SST1313 | Naming | Warning | ParameterNamingAnalyzer
SST1314 | Naming | Warning | TypeParameterNamingAnalyzer
SST1315 | Naming | Warning | UnionMemberNamingAnalyzer
SST1316 | Naming | Warning | TupleElementNamingAnalyzer
SST1400 | Maintainability | Warning | AccessModifierAnalyzer
SST1401 | Maintainability | Warning | FieldVisibilityAnalyzer
SST1402 | Maintainability | Warning | FileTypeNamespaceAnalyzer
SST1403 | Maintainability | Warning | FileTypeNamespaceAnalyzer
SST1404 | Maintainability | Warning | SuppressionJustificationAnalyzer
SST1405 | Maintainability | Warning | DebugMessageAnalyzer
SST1406 | Maintainability | Warning | DebugMessageAnalyzer
SST1407 | Maintainability | Warning | PrecedenceAnalyzer
SST1408 | Maintainability | Warning | PrecedenceAnalyzer
SST1410 | Maintainability | Warning | RedundantParenthesesAnalyzer
SST1411 | Maintainability | Warning | RedundantParenthesesAnalyzer
SST1201 | Ordering | Warning | MemberOrderingAnalyzer
SST1202 | Ordering | Warning | MemberOrderingAnalyzer
SST1203 | Ordering | Warning | MemberOrderingAnalyzer
SST1204 | Ordering | Warning | MemberOrderingAnalyzer
SST1214 | Ordering | Warning | MemberOrderingAnalyzer
SST1215 | Ordering | Warning | MemberOrderingAnalyzer
SST1600 | Documentation | Warning | MemberDocumentationAnalyzer
SST1602 | Documentation | Warning | MemberDocumentationAnalyzer
SST1604 | Documentation | Warning | MemberDocumentationAnalyzer
SST1606 | Documentation | Warning | MemberDocumentationAnalyzer
SST1608 | Documentation | Warning | MemberDocumentationAnalyzer
SST1611 | Documentation | Warning | MemberDocumentationAnalyzer
SST1612 | Documentation | Warning | MemberDocumentationAnalyzer
SST1613 | Documentation | Warning | MemberDocumentationAnalyzer
SST1614 | Documentation | Warning | MemberDocumentationAnalyzer
SST1615 | Documentation | Warning | MemberDocumentationAnalyzer
SST1616 | Documentation | Warning | MemberDocumentationAnalyzer
SST1617 | Documentation | Warning | MemberDocumentationAnalyzer
SST1618 | Documentation | Warning | MemberDocumentationAnalyzer
SST1620 | Documentation | Warning | MemberDocumentationAnalyzer
SST1621 | Documentation | Warning | MemberDocumentationAnalyzer
SST1622 | Documentation | Warning | MemberDocumentationAnalyzer
SST1623 | Documentation | Warning | MemberDocumentationAnalyzer
SST1624 | Documentation | Disabled | MemberDocumentationAnalyzer
SST1627 | Documentation | Disabled | DocumentationTextAnalyzer
SST1644 | Documentation | Disabled | DocumentationHeaderBlankLineAnalyzer
SST1629 | Documentation | Warning | MemberDocumentationAnalyzer
SST1633 | Documentation | Warning | FileHeaderAnalyzer
SST1642 | Documentation | Warning | MemberDocumentationAnalyzer
SST1643 | Documentation | Warning | MemberDocumentationAnalyzer
SST1649 | Documentation | Warning | FileNameAnalyzer
SST1653 | Documentation | Warning | SingleLineSummaryAnalyzer
SST1500 | Layout | Warning | BracePlacementAnalyzer
SST1501 | Layout | Warning | SingleLineStatementAnalyzer
SST1502 | Layout | Warning | SingleLineElementAnalyzer
SST1504 | Layout | Warning | AccessorConsistencyAnalyzer
SST1505 | Layout | Warning | BracePlacementAnalyzer
SST1506 | Layout | Warning | DocumentationHeaderSpacingAnalyzer
SST1507 | Layout | Warning | MultipleBlankLinesAnalyzer
SST1508 | Layout | Warning | BracePlacementAnalyzer
SST1509 | Layout | Warning | BracePlacementAnalyzer
SST1510 | Layout | Warning | ChainedBlockSpacingAnalyzer
SST1511 | Layout | Warning | ChainedBlockSpacingAnalyzer
SST1512 | Layout | Warning | SingleLineCommentSpacingAnalyzer
SST1513 | Layout | Warning | ClosingBraceSpacingAnalyzer
SST1514 | Layout | Warning | DocumentationHeaderSpacingAnalyzer
SST1515 | Layout | Warning | SingleLineCommentSpacingAnalyzer
SST1516 | Layout | Warning | ElementSpacingAnalyzer
SST1517 | Layout | Warning | FileStartBlankLinesAnalyzer
SST1518 | Layout | Warning | FileEndingAnalyzer
SST1519 | Layout | Warning | BraceRequirementAnalyzer
SST1520 | Layout | Warning | BraceRequirementAnalyzer
SST1200 | Ordering | Warning | UsingOrderingAnalyzer
SST1205 | Ordering | Warning | PartialElementAccessAnalyzer
SST1206 | Ordering | Warning | ModifierOrderAnalyzer
SST1207 | Ordering | Warning | ModifierOrderAnalyzer
SST1208 | Ordering | Warning | UsingOrderingAnalyzer
SST1209 | Ordering | Warning | UsingOrderingAnalyzer
SST1210 | Ordering | Warning | UsingOrderingAnalyzer
SST1211 | Ordering | Warning | UsingOrderingAnalyzer
SST1212 | Ordering | Warning | AccessorOrderAnalyzer
SST1213 | Ordering | Warning | AccessorOrderAnalyzer
SST1216 | Ordering | Warning | UsingOrderingAnalyzer
SST1217 | Ordering | Warning | UsingOrderingAnalyzer
SST1413 | Maintainability | Warning | TrailingCommaAnalyzer
SST1503 | Layout | Warning | RequireBracesAnalyzer
SST1305 | Naming | Disabled | HungarianNotationAnalyzer
SST1306 | Naming | Disabled | FieldNameStyleAnalyzer
SST1308 | Naming | Disabled | FieldNameStyleAnalyzer
SST1310 | Naming | Disabled | FieldNameStyleAnalyzer
SST1626 | Documentation | Warning | DocumentationCommentStyleAnalyzer
SST1651 | Documentation | Warning | DocumentationCommentStyleAnalyzer
SST1601 | Documentation | Warning | PartialDocumentationAnalyzer
SST1605 | Documentation | Disabled | PartialDocumentationAnalyzer
SST1607 | Documentation | Disabled | PartialDocumentationAnalyzer
SST1619 | Documentation | Disabled | PartialDocumentationAnalyzer
SST1609 | Documentation | Disabled | PropertyValueDocumentationAnalyzer
SST1610 | Documentation | Disabled | PropertyValueDocumentationAnalyzer
SST1625 | Documentation | Warning | DuplicateDocumentationAnalyzer
SST1628 | Documentation | Disabled | DocumentationTextAnalyzer
SST1630 | Documentation | Disabled | DocumentationTextAnalyzer
SST1631 | Documentation | Disabled | DocumentationTextAnalyzer
SST1632 | Documentation | Disabled | DocumentationTextAnalyzer
SST1648 | Documentation | Warning | InheritDocAnalyzer
SST1412 | Maintainability | Disabled | FileEncodingAnalyzer
SST1450 | Maintainability | Disabled | FileEncodingAnalyzer
SST1005 | Spacing | Warning | SpacingAnalyzer
SST1001 | Spacing | Warning | SpacingAnalyzer
SST1002 | Spacing | Warning | SpacingAnalyzer
SST1025 | Spacing | Warning | SpacingAnalyzer
SST1027 | Spacing | Warning | SpacingAnalyzer
SST1028 | Spacing | Warning | SpacingAnalyzer
SST1014 | Spacing | Warning | SpacingAnalyzer
SST1015 | Spacing | Warning | SpacingAnalyzer
SST1016 | Spacing | Warning | SpacingAnalyzer
SST1017 | Spacing | Warning | SpacingAnalyzer
SST1018 | Spacing | Warning | SpacingAnalyzer
SST1019 | Spacing | Warning | SpacingAnalyzer
SST1026 | Spacing | Warning | SpacingAnalyzer
SST1007 | Spacing | Warning | SpacingAnalyzer
SST1011 | Spacing | Warning | SpacingAnalyzer
SST1020 | Spacing | Warning | SpacingAnalyzer
SST1021 | Spacing | Warning | SpacingAnalyzer
SST1022 | Spacing | Warning | SpacingAnalyzer
SST1000 | Spacing | Warning | SpacingAnalyzer
SST1003 | Spacing | Warning | SpacingAnalyzer
SST1008 | Spacing | Warning | SpacingAnalyzer
SST1009 | Spacing | Warning | SpacingAnalyzer
SST1012 | Spacing | Warning | SpacingAnalyzer
SST1013 | Spacing | Warning | SpacingAnalyzer
SST1024 | Spacing | Warning | SpacingAnalyzer
SST1004 | Spacing | Warning | SpacingAnalyzer
SST1006 | Spacing | Warning | SpacingAnalyzer
SST1010 | Spacing | Disabled | SpacingAnalyzer
SST1023 | Spacing | Disabled | SpacingAnalyzer
SST1106 | Readability | Warning | EmptyStatementAnalyzer
SST1107 | Readability | Warning | MultipleStatementsOnLineAnalyzer
SST1120 | Readability | Warning | CommentContentAnalyzer
SST1122 | Readability | Warning | UseStringEmptyAnalyzer
SST1121 | Readability | Disabled | BuiltInTypeAliasAnalyzer
SST1125 | Readability | Warning | UseNullableShorthandAnalyzer
SST1129 | Readability | Warning | DefaultValueTypeConstructorAnalyzer
SST1130 | Readability | Warning | UseLambdaSyntaxAnalyzer
SST1131 | Readability | Warning | UseReadableConditionsAnalyzer
SST1123 | Readability | Warning | RegionAnalyzer
SST1124 | Readability | Warning | RegionAnalyzer
SST1132 | Readability | Warning | DoNotCombineFieldsAnalyzer
SST1133 | Readability | Warning | DoNotCombineAttributesAnalyzer
SST1134 | Readability | Warning | AttributesOnSeparateLinesAnalyzer
SST1136 | Readability | Warning | EnumValuesOnSeparateLinesAnalyzer
SST1100 | Readability | Warning | DoNotPrefixWithBaseAnalyzer
SST1127 | Readability | Warning | ConstraintOnOwnLineAnalyzer
SST1128 | Readability | Warning | ConstructorInitializerOnOwnLineAnalyzer
SST1135 | Readability | Warning | UsingDirectiveQualifiedAnalyzer
SST1139 | Readability | Warning | UseLiteralSuffixAnalyzer
SST1102 | Readability | Warning | QueryClauseAnalyzer
SST1103 | Readability | Warning | QueryClauseAnalyzer
SST1104 | Readability | Warning | QueryClauseAnalyzer
SST1105 | Readability | Warning | QueryClauseAnalyzer
SST1110 | Readability | Warning | ParameterListLayoutAnalyzer
SST1111 | Readability | Warning | ParameterListLayoutAnalyzer
SST1112 | Readability | Warning | ParameterListLayoutAnalyzer
SST1113 | Readability | Warning | ParameterListLayoutAnalyzer
SST1114 | Readability | Warning | ParameterListLayoutAnalyzer
SST1115 | Readability | Warning | ParameterListLayoutAnalyzer
SST1118 | Readability | Disabled | ParameterListLayoutAnalyzer
SST1101 | Readability | Disabled | PrefixLocalCallsWithThisAnalyzer
SST1137 | Readability | Warning | ElementIndentationAnalyzer
SST1140 | Readability | Warning | ConditionalOperatorPlacementAnalyzer
SST1141 | Readability | Warning | UseTupleSyntaxAnalyzer
SST1142 | Readability | Warning | TupleElementNameAnalyzer
SST1414 | Maintainability | Warning | TupleSignatureNamingAnalyzer
SST1143 | Readability | Warning | BooleanLiteralComparisonAnalyzer
SST1144 | Readability | Disabled | PreferOrPatternAnalyzer
SST1145 | Readability | Warning | ConditionalOperatorPlacementAnalyzer
SST1146 | Readability | Warning | ConditionalOnNewLineAnalyzer
SST1147 | Readability | Disabled | NestedTernaryAnalyzer
SST1148 | Readability | Disabled | CommentedOutCodeAnalyzer
SST1149 | Readability | Warning | PreferIsNullPatternAnalyzer
SST1416 | Maintainability | Disabled | NoPublicOnInternalTypeAnalyzer
SST1418 | Maintainability | Warning | NullCoalescingPrecedenceAnalyzer
SST1417 | Maintainability | Disabled | NamespaceFolderAnalyzer
SST1415 | Maintainability | Warning | UseNameofAnalyzer
SST1419 | Maintainability | Warning | RedundantModifierAnalyzer
SST1420 | Maintainability | Warning | TrivialAutoPropertyAnalyzer
SST1422 | Maintainability | Disabled | PrivateFieldUsedAsLocalAnalyzer
SST1424 | Maintainability | Disabled | FieldShouldBeReadonlyAnalyzer
SST1425 | Maintainability | Warning | PrimaryConstructorParameterMutationAnalyzer
SST1421 | Maintainability | Warning | WriteOnlyPropertyAnalyzer
SST1423 | Maintainability | Warning | TooManySwitchLabelsAnalyzer
SST1700 | Extensions | Warning | ExtensionBlockAnalyzer
SST1701 | Extensions | Warning | ExtensionBlockAnalyzer
SST1702 | Extensions | Warning | ExtensionBlockAnalyzer
SST1703 | Extensions | Disabled | PreferExtensionBlockAnalyzer
SST1704 | Extensions | Warning | ExtensionBlockAnalyzer
SST1705 | Extensions | Warning | ExtensionBlockAnalyzer
SST1706 | Extensions | Warning | ExtensionBlockAnalyzer
SST1707 | Extensions | Disabled | ExtensionBlockAnalyzer
SST1800 | Records | Disabled | RecordAnalyzer
SST1801 | Records | Warning | RecordAnalyzer
SST1802 | Records | Warning | RecordAnalyzer
SST1803 | Records | Warning | RecordAnalyzer
SST1900 | Concurrency | Warning | PreferLockTypeAnalyzer
SST1901 | Concurrency | Warning | LockTargetAnalyzer
SST1902 | Concurrency | Disabled | LockTargetAnalyzer
SST1903 | Concurrency | Warning | LockTargetAnalyzer
SST2000 | Modernization | Warning | ArgumentGuardAnalyzer
SST2001 | Modernization | Disabled | ArgumentGuardAnalyzer
SST2002 | Modernization | Disabled | ArgumentGuardAnalyzer
SST2003 | Modernization | Warning | ArgumentGuardAnalyzer
SST2004 | Modernization | Warning | ArgumentGuardAnalyzer
SST2100 | CollectionExpressions | Warning | EmptyCollectionExpressionAnalyzer
SST2101 | CollectionExpressions | Disabled | ExplicitCollectionExpressionAnalyzer
SST2102 | CollectionExpressions | Warning | CollectionExpressionAdvancedAnalyzer
SST2103 | CollectionExpressions | Warning | CollectionExpressionAdvancedAnalyzer
SST2104 | CollectionExpressions | Warning | CollectionExpressionAdvancedAnalyzer
SST2105 | CollectionExpressions | Warning | CollectionExpressionAdvancedAnalyzer
SST2200 | ModernSyntax | Disabled | PreferFieldKeywordAnalyzer
SST2201 | ModernSyntax | Warning | Sst2201PreferSwitchExpressionAnalyzer
SST2202 | ModernSyntax | Warning | ModernSyntaxStyleAnalyzer
SST2203 | ModernSyntax | Warning | ModernSyntaxStyleAnalyzer
SST2204 | ModernSyntax | Warning | ModernSyntaxStyleAnalyzer
SST2205 | ModernSyntax | Warning | EnumSwitchCoverageAnalyzer
SST2206 | ModernSyntax | Warning | EnumSwitchCoverageAnalyzer
SST2207 | ModernSyntax | Warning | ModernSyntaxFlowAnalyzer
SST2208 | ModernSyntax | Warning | ModernSyntaxFlowAnalyzer
SST2209 | ModernSyntax | Warning | NullableSyntaxCleanupAnalyzer
SST2210 | ModernSyntax | Warning | NullableSyntaxCleanupAnalyzer
SST2211 | ModernSyntax | Warning | NullableSyntaxCleanupAnalyzer
SST2212 | ModernSyntax | Warning | ModernSyntaxReadabilityAnalyzer
SST2213 | ModernSyntax | Warning | ModernSyntaxReadabilityAnalyzer
SST2214 | ModernSyntax | Warning | ModernSyntaxReadabilityAnalyzer
SST2215 | ModernSyntax | Warning | ModernSyntaxReadabilityAnalyzer
SST2216 | ModernSyntax | Warning | ModernSyntaxReadabilityAnalyzer
SST2217 | ModernSyntax | Warning | ModernSyntaxReadabilityAnalyzer
SST2218 | ModernSyntax | Warning | ModernSyntaxPreferenceAnalyzer
SST2219 | ModernSyntax | Warning | ModernSyntaxPreferenceAnalyzer
SST2220 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2221 | ModernSyntax | Disabled | ModernSyntaxValueAnalyzer
SST2222 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2223 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2224 | ModernSyntax | Disabled | ModernSyntaxValueAnalyzer
SST2225 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2226 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2227 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2228 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2229 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2230 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2231 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2232 | ModernSyntax | Warning | ModernSyntaxValueAnalyzer
SST2233 | ModernSyntax | Disabled | ModernSyntaxValueAnalyzer
SST1426 | Maintainability | Warning | Sst1426PragmaWarningDisableAnalyzer
SST1654 | Documentation | Warning | ExtensionBlockDocumentationAnalyzer
SST1655 | Documentation | Warning | ExtensionBlockDocumentationAnalyzer
SST1656 | Documentation | Warning | ExtensionBlockDocumentationAnalyzer
SST1657 | Documentation | Warning | ExtensionBlockDocumentationAnalyzer
SST1172 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1173 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1174 | Readability | Warning | RedundantCodeAnalyzer
SST1175 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1176 | Readability | Disabled | RedundantCodeAnalyzer
SST1177 | Readability | Warning | RedundantCodeAnalyzer
SST1427 | Maintainability | Warning | RedundantModifierAnalyzer
SST1178 | Readability | Warning | RedundantCodeAnalyzer
SST1179 | Readability | Warning | RedundantCodeAnalyzer
SST1180 | Readability | Warning | RedundantCodeAnalyzer
SST1181 | Readability | Warning | RedundantCodeAnalyzer
SST1182 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1183 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1184 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1185 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1186 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1187 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1188 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1428 | Maintainability | Warning | TypeDesignAnalyzer
SST1429 | Maintainability | Warning | ExceptionHandlingAnalyzer
SST1430 | Maintainability | Warning | ExceptionHandlingAnalyzer
SST1431 | Maintainability | Warning | TypeDesignAnalyzer
SST1432 | Maintainability | Disabled | TypeDesignAnalyzer
SST1317 | Naming | Warning | MethodNamingAnalyzer
SST1318 | Naming | Warning | MethodNamingAnalyzer
SST2005 | Modernization | Warning | PatternMatchingAnalyzer
SST2006 | Modernization | Warning | PatternMatchingAnalyzer
SST2007 | Modernization | Warning | PatternMatchingAnalyzer
SST1189 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1190 | Readability | Warning | ExpressionSimplificationAnalyzer
SST1191 | Readability | Disabled | LiteralFormattingAnalyzer
SST1192 | Readability | Disabled | LiteralFormattingAnalyzer
SST1116 | Readability | Warning | NameSimplificationAnalyzer
SST1117 | Readability | Warning | NameSimplificationAnalyzer
SST1193 | Readability | Warning | LanguageStyleAnalyzer
SST1194 | Readability | Warning | LanguageStyleAnalyzer
SST1195 | Readability | Warning | LanguageStyleAnalyzer
SST1196 | Readability | Warning | LanguageStyleAnalyzer
SST1197 | Readability | Warning | LanguageStyleAnalyzer
SST1198 | Readability | Warning | LanguageStyleAnalyzer
SST1199 | Readability | Warning | LanguageStyleAnalyzer
SST1433 | Maintainability | Warning | EmptyCodeAnalyzer
SST1434 | Maintainability | Warning | EmptyCodeAnalyzer
SST1435 | Maintainability | Warning | EmptyCodeAnalyzer
SST1436 | Maintainability | Disabled | EmptyCodeAnalyzer
SST1437 | Maintainability | Disabled | EmptyCodeAnalyzer
SST1438 | Maintainability | Disabled | EmptyCodeAnalyzer
SST1439 | Maintainability | Warning | EmptyCodeAnalyzer
SST1440 | Maintainability | Warning | Sst1440PrivateMemberUsageAnalyzer
SST1441 | Maintainability | Warning | Sst1440PrivateMemberUsageAnalyzer

## Release 3.28.0

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

## Release 3.29.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST2246 | ModernSyntax | Warning | Sst2246ChainedConditionalToSwitchAnalyzer
SST2247 | ModernSyntax | Warning | Sst2247MemberCopyDeconstructionAnalyzer
SST2248 | ModernSyntax | Warning | Sst2248UseComparisonPatternAnalyzer
SST2249 | ModernSyntax | Warning | Sst2249UseInterpolatedStringAnalyzer
SST2250 | ModernSyntax | Warning | Sst2250JoinDeclarationAndAssignmentAnalyzer
SST2251 | ModernSyntax | Warning | Sst2251InferableTypeArgumentsAnalyzer
SST2322 | Design | Warning | Sst2322ReadonlyMutableCollectionFieldAnalyzer
SST2323 | Design | Warning | Sst2323PreferInterfaceOverAbstractClassAnalyzer
SST2324 | Design | Warning | Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer
SST2325 | Design | Warning | Sst2325AsyncValidatesAfterAwaitAnalyzer
SST2448 | Correctness | Warning | Sst2448DelegateSubtractionAnalyzer
SST2449 | Correctness | Warning | Sst2449LambdaUnsubscriptionAnalyzer
SST2450 | Correctness | Warning | Sst2450DebugAssertSideEffectAnalyzer
SST2451 | Correctness | Warning | Sst2451UncreatableClassAnalyzer
SST2452 | Correctness | Warning | Sst2452PureVoidMethodAnalyzer
SST2456 | Correctness | Warning | Sst2456RedeclaredFieldLikeEventAnalyzer
SST2457 | Correctness | Warning | Sst2457UncheckedSequenceSumAnalyzer
SST2458 | Correctness | Warning | Sst2458NonFlagsEnumBitwiseAnalyzer
SST2459 | Correctness | Warning | Sst2459OptionalByRefParameterAnalyzer
SST2460 | Correctness | Warning | Sst2460DefaultValueOnParameterAnalyzer
SST2462 | Correctness | Warning | Sst2462NewMemberReducesAccessibilityAnalyzer
SST2463 | Correctness | Warning | Sst2463InheritedFieldCaseClashAnalyzer
SST2464 | Correctness | Warning | Sst2464EqualityOperatorOnMutableClassAnalyzer
SST2465 | Correctness | Warning | Sst2465LoopConditionVariableReassignedAnalyzer
SST2467 | Correctness | Warning | Sst2467BypassedParamsOverloadAnalyzer
SST2468 | Correctness | Warning | Sst2468UnimplementedPartialMethodAnalyzer
SST2470 | Correctness | Warning | Sst2470FusedSqlKeywordAnalyzer
SST2472 | Correctness | Warning | MefContractAnalyzer
SST2473 | Correctness | Warning | MefContractAnalyzer
SST2474 | Correctness | Warning | MefContractAnalyzer
SST2475 | Correctness | Warning | Sst2475TemporalPrimaryKeyAnalyzer
SST2479 | Correctness | Warning | Sst2479CapturedLoopVariableAnalyzer
SST2481 | Correctness | Warning | Sst2481IdentityHashInValueHashAnalyzer
SST2500 | Testing | Warning | Sst2500TestWithoutAssertionAnalyzer
SST2501 | Testing | Warning | Sst2501SelfComparisonAssertionAnalyzer
SST2502 | Testing | Warning | Sst2502ReversedEqualityAssertionAnalyzer
SST2503 | Testing | Warning | Sst2503BooleanLiteralAssertionAnalyzer
SST2504 | Testing | Warning | Sst2504EmptyTestClassAnalyzer
SST2505 | Testing | Warning | Sst2505ParameterizedTestWithoutDataSourceAnalyzer
SST2506 | Testing | Warning | Sst2506ThreadSleepInTestAnalyzer
SST2507 | Testing | Warning | Sst2507ExpectedExceptionAnalyzer
SST2600 | Logging | Warning | Sst2600LegacyTracingAnalyzer

## Release 3.32.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST2326 | Design | Info | Sst2326InterfaceToConcreteCastAnalyzer
SST2327 | Design | Warning | Sst2327SelfTypeCheckAnalyzer

## Release 3.33.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1320 | Naming | Warning | Sst1320ParameterNameMatchesMethodAnalyzer
SST2252 | ModernSyntax | Warning | Sst2252NestedSwitchAnalyzer
SST2328 | Design | Warning | Sst2328ExposedNativePointerAnalyzer
SST2484 | Correctness | Warning | Sst2484DangerousGetHandleAnalyzer
SST2485 | Correctness | Warning | Sst2485NotImplementedExceptionAnalyzer
SST2486 | Correctness | Warning | Sst2486PreferAssemblyLoadAnalyzer
SST2487 | Correctness | Warning | Sst2487ConstructorArgumentMismatchAnalyzer
SST2488 | Correctness | Warning | Sst2488LogAndRethrowAnalyzer
SST2489 | Correctness | Warning | Sst2489TypeDecidedComparisonAnalyzer
SST2490 | Correctness | Warning | Sst2490MergeableAdjacentTryAnalyzer
SST2508 | Testing | Warning | Sst2508IncompleteAssertionAnalyzer
SST2509 | Testing | Warning | Sst2509InvalidTestMethodShapeAnalyzer
SST2601 | Logging | Warning | Sst2601LoggerMemberNamingAnalyzer

## Release 3.34.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST2254 | ModernSyntax | Disabled | Sst2254ExplicitObjectCreationTypeAnalyzer
SST2700 | Frameworks | Warning | Sst2700RouteTemplateBackslashAnalyzer
SST2701 | Frameworks | Warning | Sst2701JSInvokableMustBePublicAnalyzer
SST2702 | Frameworks | Warning | Sst2702SupplyParameterFromQueryTypeAnalyzer
SST2703 | Frameworks | Warning | Sst2703RouteConstraintTypeMismatchAnalyzer
SST2704 | Frameworks | Warning | Sst2704ApiActionMissingHttpVerbAnalyzer
SST2705 | Frameworks | Disabled | Sst2705BoundModelUnderpostingAnalyzer
SST2706 | Frameworks | Warning | Sst2706StaThreadEntryPointAnalyzer
SST2707 | Frameworks | Disabled | Sst2707FireAndForgetHttpContextAnalyzer
SST2708 | Frameworks | Warning | Sst2708LifecycleEventSubscriptionAnalyzer
SST2709 | Frameworks | Warning | Sst2709StateHasChangedInDisposeAnalyzer
SST2710 | Frameworks | Warning | Sst2710TimerStateHasChangedAnalyzer
SST2711 | Frameworks | Warning | Sst2711AsyncVoidLifecycleOverrideAnalyzer
SST2712 | Frameworks | Warning | Sst2712SetterlessInjectedPropertyAnalyzer
SST2713 | Frameworks | Warning | Sst2713UnstoredDotNetObjectReferenceAnalyzer

## Release 3.36.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST1220 | Ordering | Info | Sst1220NamedArgumentOrderAnalyzer
SST1221 | Ordering | Info | Sst1221ConstraintClauseOrderAnalyzer
SST1321 | Naming | Warning | MethodNamingAnalyzer
SST1525 | Layout | Warning | Sst1525SwitchSectionBracesAnalyzer
SST1526 | Layout | Disabled | Sst1526BinaryOperatorNewLineAnalyzer
SST1527 | Layout | Disabled | Sst1527ArrowTokenNewLineAnalyzer
SST1528 | Layout | Disabled | Sst1528EqualsTokenNewLineAnalyzer
SST1529 | Layout | Disabled | Sst1529NullConditionalNewLineAnalyzer
SST1530 | Layout | Disabled | Sst1530BaseListOnDeclarationLineAnalyzer
SST1531 | Layout | Disabled | Sst1531InitializerOnSingleLineAnalyzer
SST1532 | Layout | Disabled | Sst1532ConsistentLineEndingsAnalyzer
SST1533 | Layout | Disabled | Sst1533FileWithoutCodeAnalyzer
SST1660 | Documentation | Info | Sst1660ParameterDocumentationOrderAnalyzer
SST1661 | Documentation | Info | Sst1661CodeTagContentAnalyzer
SST1662 | Documentation | Disabled | Sst1662ThrownExceptionDocumentationAnalyzer
SST1663 | Documentation | Disabled | Sst1663SummaryCommentAnalyzer
SST1664 | Documentation | Disabled | Sst1664SummaryParagraphAnalyzer
SST1708 | Extensions | Warning | Sst1708UnusedExtensionReceiverAnalyzer
SST1709 | Extensions | Disabled | Sst1709AlmostExtensionMethodAnalyzer
SST1804 | Records | Info | RecordAnalyzer
SST2255 | ModernSyntax | Warning | Sst2255UseIsNullOrEmptyAnalyzer
SST2256 | ModernSyntax | Info | Sst2256UseInstanceExtensionInvocationAnalyzer
SST2257 | ModernSyntax | Info | Sst2257SimplifyLambdaBodyAnalyzer
SST2258 | ModernSyntax | Info | Sst2258RemoveRedundantDelegateCreationAnalyzer
SST2259 | ModernSyntax | Info | Sst2259RemoveStrayEmptyStatementAnalyzer
SST2260 | ModernSyntax | Info | Sst2260RemoveRedundantAsCastAnalyzer
SST2261 | ModernSyntax | Info | Sst2261UseExclusiveOrAnalyzer
SST2262 | ModernSyntax | Info | Sst2262UseRegularStringLiteralAnalyzer
SST2263 | ModernSyntax | Info | Sst2263HoistLoopConditionAnalyzer
SST2264 | ModernSyntax | Warning | Sst2264UseNamedEnumMemberAnalyzer
SST2265 | ModernSyntax | Disabled | Sst2265FoldFluentCallChainAnalyzer
SST2266 | ModernSyntax | Disabled | Sst2266InlineSingleUseLocalAnalyzer
SST2267 | ModernSyntax | Disabled | Sst2267InfiniteLoopStyleAnalyzer
SST2268 | ModernSyntax | Disabled | Sst2268ObjectCreationParenthesesAnalyzer
SST2269 | ModernSyntax | Disabled | Sst2269ConditionalConditionParenthesesAnalyzer
SST2270 | ModernSyntax | Disabled | Sst2270ArrayCreationTypeStyleAnalyzer
SST2271 | ModernSyntax | Disabled | Sst2271VarStyleAnalyzer
SST2272 | ModernSyntax | Disabled | Sst2272EnumFlagValueStyleAnalyzer
SST2329 | Design | Warning | Sst2329FlagsEnumMissingZeroValueAnalyzer
SST2330 | Design | Info | Sst2330FlagsCombinationLiteralAnalyzer
SST2331 | Design | Disabled | Sst2331ImplicitEnumValueAnalyzer
SST2332 | Design | Warning | Sst2332PrivateSetterOnlyWrittenDuringConstructionAnalyzer
SST2333 | Design | Disabled | Sst2333NonGenericContractAnalyzer
SST2334 | Design | Disabled | Sst2334MissingDebuggerDisplayAnalyzer
SST2335 | Design | Disabled | Sst2335PartialStaticMismatchAnalyzer
SST2491 | Correctness | Warning | Sst2491AwaitableReturnedFromTeardownAnalyzer
SST2492 | Correctness | Warning | Sst2492GuardOnNullableParameterAnalyzer
SST2493 | Correctness | Warning | Sst2493NullComparisonOnUnconstrainedGenericAnalyzer
SST2494 | Correctness | Warning | Sst2494ConstantNullCoalesceAnalyzer
SST2495 | Correctness | Warning | Sst2495RedundantFlagsOperandAnalyzer
SST2496 | Correctness | Info | Sst2496RedundantDisposeAnalyzer

## Release 3.37.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST2273 | ModernSyntax | Disabled | Sst2273PreferGuardClauseAnalyzer

## Release 3.38.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SST2274 | ModernSyntax | Warning | Sst2274AsAssignmentToIsPatternAnalyzer
SST2275 | ModernSyntax | Info | ExpressionBodyAnalyzer
SST2276 | ModernSyntax | Disabled | ExpressionBodyAnalyzer
SST2277 | ModernSyntax | Disabled | ExpressionBodyAnalyzer
SST2278 | ModernSyntax | Disabled | ExpressionBodyAnalyzer
SST2279 | ModernSyntax | Info | ExpressionBodyAnalyzer
SST2280 | ModernSyntax | Info | ExpressionBodyAnalyzer
SST2281 | ModernSyntax | Info | ExpressionBodyAnalyzer
SST2282 | ModernSyntax | Warning | Sst2282ReferenceEqualsNullPatternAnalyzer
SST2283 | ModernSyntax | Warning | Sst2283FoldGuardIntoAssignedValueAnalyzer
