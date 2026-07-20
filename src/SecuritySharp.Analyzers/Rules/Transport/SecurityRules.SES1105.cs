// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1105 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1105 — bearer/OpenID metadata retrieval is allowed over plain HTTP without a development-only guard.</summary>
    public static readonly DiagnosticDescriptor PlainHttpMetadataRetrieval = Create(
        "SES1105",
        "Bearer and OpenID Connect metadata must not be retrieved over plain HTTP outside development",
        PlainHttpMetadataRetrievalMessage,
        Transport,
        PlainHttpMetadataRetrievalDescription);

    /// <summary>The SES1105 message format.</summary>
    private const string PlainHttpMetadataRetrievalMessage =
        "'{0}.RequireHttpsMetadata' is set to false outside a development guard; the identity provider's discovery document "
        + "and signing keys are then fetched over plain HTTP, where a network attacker can substitute them";

    /// <summary>The SES1105 rule description.</summary>
    private const string PlainHttpMetadataRetrievalDescription =
        "The JWT-bearer and OpenID Connect handlers download the authority's discovery document and its JWKS signing keys to "
        + "validate incoming tokens. 'RequireHttpsMetadata' defaults to true so that download uses HTTPS. Setting it to false "
        + "lets the handler fetch that metadata over plain HTTP: anyone on the network path can rewrite the discovery document "
        + "and swap in signing keys they control, after which the application accepts tokens the attacker minted -- a full "
        + "authentication bypass. Disabling it is only ever acceptable against a loopback authority during local development, so "
        + "the assignment is reported unless it is lexically enclosed by a development-environment guard (an 'if' or conditional "
        + "whose condition calls 'IsDevelopment'). Leave 'RequireHttpsMetadata' true in production and serve the authority over "
        + "HTTPS.";
}
