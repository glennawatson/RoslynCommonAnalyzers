// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Base descriptor factory shared by every SecuritySharp rule group. Each rule is declared in a
/// per-id partial (<c>Rules/&lt;Group&gt;/SecurityRules.SESxxxx.cs</c>) and calls <see cref="Create"/>
/// with its group's category, so a descriptor declaration stays a single call and the category names
/// live in one place. The category maps 1:1 to the id's hundreds digit, matching the folder layout.
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

    /// <summary>Creates an enabled-by-default Warning descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="category">The rule category (one of the group constants on this class).</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string category, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, category, description);
}
