// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1303 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1303 — a regular-expression pattern is built from non-constant data.</summary>
    public static readonly DiagnosticDescriptor RegexInjection = Create(
        "SES1303",
        "Regular-expression pattern must not be built from non-constant data",
        "The regular-expression pattern passed to '{0}' is built from non-constant data; wrap untrusted text in Regex.Escape and compose it into a fixed pattern",
        Injection,
        RegexInjectionDescription);

    /// <summary>The SES1303 rule description.</summary>
    private const string RegexInjectionDescription =
        "A regular expression whose pattern text comes from non-constant data lets whoever supplies that data control the "
        + "matching grammar, not just the text being searched. Regex metacharacters in the data change what the expression "
        + "means: alternation and anchors can broaden a match that was meant to be exact, nested quantifiers can trigger "
        + "catastrophic backtracking that hangs the thread, and injected groups can rewrite the captures the surrounding code "
        + "trusts. The rule reports the pattern argument of the Regex constructor and of the static 'Regex.IsMatch', 'Match', "
        + "'Matches', 'Replace', and 'Split' overloads when that argument is not a compile-time constant. Treat the untrusted "
        + "text as literal data: pass it through 'Regex.Escape' and build a fixed template around the escaped value, or use an "
        + "ordinary string comparison when a regular expression is not actually required. Reporting is limited to the pattern "
        + "argument; a regex run over non-constant input with a constant pattern is a separate matching-timeout concern and is "
        + "not flagged here.";
}
