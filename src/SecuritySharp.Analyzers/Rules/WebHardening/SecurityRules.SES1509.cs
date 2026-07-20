// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1509 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1509 — a backtracking-prone regular expression runs with no match-timeout bound.</summary>
    public static readonly DiagnosticDescriptor BacktrackingRegexWithoutTimeout = Create(
        "SES1509",
        "Backtracking-prone regular expression has no match timeout",
        "The regular expression passed to '{0}' has a nested or overlapping quantifier and no match timeout, so a crafted input can force catastrophic backtracking and hang the thread (ReDoS)",
        WebHardening,
        BacktrackingRegexWithoutTimeoutDescription);

    /// <summary>The SES1509 rule description.</summary>
    private const string BacktrackingRegexWithoutTimeoutDescription =
        "The backtracking regex engine can take time exponential in the input length when a pattern nests or overlaps "
        + "quantifiers -- a group repeated by an unbounded quantifier ('*', '+', or '{n,}') whose body itself repeats or "
        + "offers alternatives, as in '(a+)+', '(a*)*', '([a-z]+)*', '(.*)*', '(a|aa)+', or '(\\d+)*'. A single crafted "
        + "input string then makes one match run for seconds, minutes, or longer, pinning a CPU and denying service. When "
        + "the pattern is a fixed literal but is run with no match-timeout and without the non-backtracking engine, nothing "
        + "bounds that cost. Supply a 'System.TimeSpan' match timeout so a runaway match is aborted, or pass "
        + "'RegexOptions.NonBacktracking' to use the linear-time engine, or rewrite the pattern so no repeated group "
        + "contains its own repetition or alternation.";
}
