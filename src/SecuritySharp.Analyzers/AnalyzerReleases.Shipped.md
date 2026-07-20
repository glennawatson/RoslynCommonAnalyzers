; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 3.33.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SES1009 | Cryptography | Warning | Ses1009FastPasswordHashAnalyzer
SES1310 | Injection | Warning | Ses1310AnonymousLdapBindAnalyzer
SES1515 | WebHardening | Warning | Ses1515PermissiveContentSecurityPolicyAnalyzer

## Release 3.32.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SES1007 | Cryptography | Warning | Ses1007HomeRolledCryptographyAnalyzer
SES1008 | Cryptography | Warning | Ses1008UntrustedXmlSignatureKeyAnalyzer
SES1203 | Secrets | Warning | Ses1203EmptyConnectionStringPasswordAnalyzer

## Release 3.31.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SES1108 | Transport | Warning | Ses1108AlwaysTrueServerCertificateValidationAnalyzer

## Release 3.30.0

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

## Release 3.29.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SES1001 | Cryptography | Warning | Ses1001ConstantAeadNonceAnalyzer
SES1002 | Cryptography | Warning | Ses1002ConstantKdfSaltAnalyzer
SES1003 | Cryptography | Warning | Ses1003Pbkdf2IterationCountAnalyzer
SES1004 | Cryptography | Warning | Ses1004GuidAsSecretAnalyzer
SES1005 | Cryptography | Warning | Ses1005NonConstantTimeSecretComparisonAnalyzer
SES1102 | Transport | Warning | Ses1102AcceptAnyServerCertificateAnalyzer
SES1104 | Transport | Warning | Ses1104WeakenedCertificateChainValidationAnalyzer
SES1105 | Transport | Warning | Ses1105PlainHttpMetadataRetrievalAnalyzer
SES1106 | Transport | Warning | Ses1106CleartextHttpUrlAnalyzer
SES1201 | Secrets | Warning | Ses1201HardcodedSecretAnalyzer
SES1202 | Secrets | Warning | Ses1202HardcodedCredentialArgumentAnalyzer
SES1301 | Injection | Warning | Ses1301ProcessArgumentsCompositionAnalyzer
SES1302 | Injection | Warning | Ses1302ShellExecuteFileNameAnalyzer
SES1303 | Injection | Warning | Ses1303RegexInjectionAnalyzer
SES1304 | Injection | Warning | Ses1304ArchiveEntryPathTraversalAnalyzer
SES1305 | Injection | Warning | Ses1305UploadFilenameInPathAnalyzer
SES1306 | Injection | Warning | Ses1306DynamicScriptCompilationAnalyzer
SES1401 | Serialization | Warning | Ses1401NonConstantTypeActivationAnalyzer
SES1402 | Serialization | Warning | Ses1402UnsafeAssemblyLoadAnalyzer
SES1403 | Serialization | Info | Ses1403JsonMaxDepthAnalyzer
SES1501 | WebHardening | Warning | Ses1501CorsAnyOriginWithCredentialsAnalyzer
SES1502 | WebHardening | Warning | Ses1502AlwaysAllowedCorsOriginAnalyzer
SES1503 | WebHardening | Warning | Ses1503JwtSignatureValidationDisabledAnalyzer
SES1504 | WebHardening | Warning | Ses1504SameSiteNoneWithoutSecureAnalyzer
SES1505 | WebHardening | Warning | Ses1505RequestBodySizeLimitRemovalAnalyzer
SES1506 | WebHardening | Info | Ses1506UnguardedDeveloperExceptionPageAnalyzer
SES1507 | WebHardening | Warning | Ses1507ConflictingAnonymousAuthorizationAnalyzer
SES1508 | WebHardening | Warning | Ses1508FailOpenValidationAnalyzer
SES1601 | Ai | Warning | Ses1601NonConstantSystemPromptAnalyzer
SES1602 | Ai | Warning | Ses1602ModelOutputToDangerousSinkAnalyzer
SES1603 | Ai | Warning | Ses1603NonDestructiveToolMutationAnalyzer
SES1604 | Ai | Warning | Ses1604PromptTemplateContentEncodingDisabledAnalyzer
SES1605 | Ai | Info | Ses1605SensitiveAiTelemetryAnalyzer
SES1606 | Ai | Warning | Ses1606CleartextModelWeightsUrlAnalyzer
