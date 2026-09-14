// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// The category names shared by every SecuritySharp rule group. Each rule is declared in a per-id
/// partial (<c>Rules/&lt;Group&gt;/SecurityRules.SESxxxx.cs</c>) that builds its descriptor through
/// <see cref="DescriptorFactory"/> with its group's category, so a descriptor declaration stays a single
/// call and the category names live in one place. The category maps 1:1 to the id's hundreds digit,
/// matching the folder layout.
/// </summary>
internal static partial class SecurityRules
{
    /// <summary>Cryptography rules (SES10xx): algorithm choice, key handling, and nonce/IV misuse.</summary>
    public const string Cryptography = "Cryptography";

    /// <summary>Transport rules (SES11xx): TLS configuration and certificate validation.</summary>
    public const string Transport = "Transport";

    /// <summary>Secrets rules (SES12xx): hard-coded credentials, keys, and tokens.</summary>
    public const string Secrets = "Secrets";

    /// <summary>Injection rules (SES13xx): SQL, command, path, and format-string injection sinks.</summary>
    public const string Injection = "Injection";

    /// <summary>Serialization rules (SES14xx): unsafe deserialization and type binding.</summary>
    public const string Serialization = "Serialization";

    /// <summary>Web-hardening rules (SES15xx): cookies, security headers, CORS, and redirects.</summary>
    public const string WebHardening = "WebHardening";

    /// <summary>AI rules (SES16xx): prompt construction and model-input trust boundaries.</summary>
    public const string Ai = "Ai";

    /// <summary>Blazor rules (SES17xx): server-rendered markup trust boundaries and JavaScript interop safety.</summary>
    public const string Blazor = "Blazor";
}
