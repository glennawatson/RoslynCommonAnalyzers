// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1503 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1503 — JWT signature verification is disabled on <c>TokenValidationParameters</c>.</summary>
    public static readonly DiagnosticDescriptor JwtSignatureValidationDisabled = Create(
        "SES1503",
        "JWT signature verification must not be disabled on TokenValidationParameters",
        JwtSignatureValidationDisabledMessage,
        WebHardening,
        JwtSignatureValidationDisabledDescription);

    /// <summary>The SES1503 message format.</summary>
    private const string JwtSignatureValidationDisabledMessage =
        "'TokenValidationParameters.{0}' is set to false, which turns off JWT signature verification; a forged or unsigned token "
        + "then passes validation";

    /// <summary>The SES1503 rule description.</summary>
    private const string JwtSignatureValidationDisabledDescription =
        "A JSON Web Token is trusted only because its signature proves it was minted by the identity provider. On "
        + "'TokenValidationParameters', 'RequireSignedTokens' rejects a token that carries no signature at all, and "
        + "'ValidateIssuerSigningKey' checks that the key which signed it is one the application accepts; both default to true. "
        + "Setting either to false lets the validation pipeline accept a token whose signature is missing or was produced by a "
        + "key the attacker chose, so anyone can mint a token with whatever claims they like and have it treated as authentic -- "
        + "a complete authentication bypass and the most dangerous way to misconfigure token validation. Leave both flags at "
        + "their secure default and supply the issuer signing keys the token must be verified against. The issuer, audience, and "
        + "lifetime flags are a separate concern and are not reported here.";
}
