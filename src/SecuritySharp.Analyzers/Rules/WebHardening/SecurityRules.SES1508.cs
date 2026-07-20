// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1508 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1508 — a security-check method swallows an exception and returns success (fail-open).</summary>
    public static readonly DiagnosticDescriptor FailOpenValidation = Create(
        "SES1508",
        "A validation method must not fail open by returning success from a catch",
        "'{0}' catches an exception and returns success, so an attacker who forces the exception passes validation",
        WebHardening,
        FailOpenValidationDescription);

    /// <summary>The SES1508 rule description.</summary>
    private const string FailOpenValidationDescription =
        "A method whose name and 'bool' (or 'Task<bool>'/'ValueTask<bool>') return type mark it as a security check -- "
        + "one beginning with 'Validate', 'Verify', 'Authenticate', 'Authorize', 'Check', 'IsValid', 'IsAuthentic', or "
        + "'Ensure' -- must treat an exception as a failed check, not a passed one. When such a method catches a broad "
        + "exception ('catch' or 'catch (Exception)') or a security-relevant one (a cryptographic, authentication, or "
        + "'*SecurityTokenException' type) and the catch does nothing but return success -- a lone 'return true;' (or "
        + "'FromResult(true)'), or an empty body that falls through to a trailing 'return true;' -- the check fails open: an "
        + "attacker who can force the exception (a malformed token, a signature that throws, a provider that is momentarily "
        + "unavailable) is treated as validated or authenticated. Return 'false' from the catch so a check that cannot "
        + "complete is a check that did not pass.";
}
