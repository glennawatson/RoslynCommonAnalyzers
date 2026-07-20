; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SES1006 | Cryptography | Warning | Ses1006UnprotectedDataProtectionKeysAnalyzer
SES1107 | Transport | Warning | Ses1107WeakenedSqlTransportSecurityAnalyzer
SES1307 | Injection | Warning | Ses1307InsecureTempFileAnalyzer
SES1308 | Injection | Warning | Ses1308OverPermissiveUnixFileModeAnalyzer
SES1309 | Injection | Warning | Ses1309XsltScriptExecutionAnalyzer
SES1404 | Serialization | Warning | Ses1404NonConstantActivatorTypeNameAnalyzer
SES1405 | Serialization | Warning | Ses1405TypelessDeserializationAnalyzer
SES1509 | WebHardening | Warning | Ses1509BacktrackingRegexWithoutTimeoutAnalyzer
SES1510 | WebHardening | Warning | Ses1510NonConstantControllerRedirectAnalyzer
SES1511 | WebHardening | Warning | Ses1511ForwardedHeadersTrustBoundaryRemovalAnalyzer
SES1512 | WebHardening | Warning | Ses1512SensitiveFrameworkDiagnosticsAnalyzer
SES1513 | WebHardening | Warning | Ses1513DiscardedAuthorizationResultAnalyzer
SES1514 | WebHardening | Warning | Ses1514OidcProtocolProtectionDisabledAnalyzer
