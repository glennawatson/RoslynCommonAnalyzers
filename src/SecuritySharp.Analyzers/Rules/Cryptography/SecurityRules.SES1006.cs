// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1006 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1006 — persisted Data Protection keys are stored without at-rest encryption.</summary>
    public static readonly DiagnosticDescriptor UnprotectedDataProtectionKeys = Create(
        "SES1006",
        "Persisted Data Protection keys must be encrypted at rest",
        "'{0}' persists the Data Protection key ring to an explicit repository with no 'ProtectKeysWith...' call in the same chain, so the keys are stored unencrypted at rest",
        Cryptography,
        UnprotectedDataProtectionKeysDescription);

    /// <summary>The SES1006 rule description.</summary>
    private const string UnprotectedDataProtectionKeysDescription =
        "Data Protection protects its key ring at rest with a default key-encryption mechanism only while the keys live in the "
        + "default location. Selecting an explicit key repository -- 'PersistKeysToFileSystem', 'PersistKeysToDbContext', "
        + "'PersistKeysToAzureBlobStorage', 'PersistKeysToStackExchangeRedis', or 'PersistKeysToRegistry' -- turns that default "
        + "off, so the private key material is written to the store in plaintext. Anyone who can read the file share, database, "
        + "blob container, cache, or registry hive then holds the keys that sign and encrypt every protected payload -- "
        + "authentication cookies, anti-forgery tokens, and anything else guarded by 'IDataProtector' -- and can forge or "
        + "decrypt them at will. Pair the persistence call with a 'ProtectKeysWith...' call in the same configuration chain "
        + "('ProtectKeysWithCertificate', 'ProtectKeysWithDpapi', 'ProtectKeysWithDpapiNG', or 'ProtectKeysWithAzureKeyVault') "
        + "so the key ring is encrypted before it leaves the process.";
}
