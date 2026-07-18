// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2470 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2470 — two concatenated string literals fuse a SQL keyword at the seam because a space is missing.</summary>
    public static readonly DiagnosticDescriptor FusedSqlKeyword = Create(
        "SST2470",
        "String literals concatenated without a space should not fuse a SQL keyword",
        "These two string literals join with no space between them, fusing a SQL keyword into the token '{0}'; add a space at the seam so the keyword stays a separate token",
        FusedSqlKeywordDescription);

    /// <summary>The FusedSqlKeyword rule description.</summary>
    private const string FusedSqlKeywordDescription =
        "A SQL statement built by concatenating string literals is one continuous string at runtime, so the seam between two "
        + "literals is invisible: if a literal ends in a word character and the next begins with a SQL keyword, the two collapse "
        + "into one token. \"SELECT * FROM t\" + \"WHERE id = 1\" runs as \"...tWHERE...\", and \"...WHERE a = 1\" + \"AND b = 2\" runs "
        + "as \"...1AND...\" — the keyword is gone, and the database either errors or, worse, silently reads a different query. The "
        + "rule reports a '+' of two string literals (regular, verbatim, or raw) where the seam fuses a keyword: the left ends in a "
        + "word character and the right starts with a curated SQL keyword, or the symmetric case where the left ends with a keyword "
        + "and the right starts with a word character. Both operands must be literals — no runtime value is ever bound — and to keep "
        + "prose concatenations quiet the left literal must itself read as SQL by containing a strong SQL keyword. A space, tab, or "
        + "newline at the seam, an interpolated or non-literal operand, or text that does not look like SQL is never reported.";
}
