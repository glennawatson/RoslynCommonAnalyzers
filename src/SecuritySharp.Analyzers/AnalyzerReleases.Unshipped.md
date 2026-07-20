; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SES1001 | Cryptography | Warning | Ses1001ConstantAeadNonceAnalyzer
SES1002 | Cryptography | Warning | Ses1002ConstantKdfSaltAnalyzer
SES1003 | Cryptography | Warning | Ses1003Pbkdf2IterationCountAnalyzer
SES1004 | Cryptography | Warning | Ses1004GuidAsSecretAnalyzer
SES1005 | Cryptography | Warning | Ses1005NonConstantTimeSecretComparisonAnalyzer
SES1201 | Secrets | Warning | Ses1201HardcodedSecretAnalyzer
SES1202 | Secrets | Warning | Ses1202HardcodedCredentialArgumentAnalyzer
