// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The line-length ceiling a suggested rewrite has to respect, so no rule demands an edit that the
/// layout rules then reject.
/// </summary>
/// <remarks>
/// A rule that says "put this on one line" and a rule that says "no line past this column" can
/// contradict each other, leaving code that satisfies neither. The rewrite rule yields: it stays
/// silent when its own result would not fit. The ceiling is whatever SST1521 is configured to
/// enforce for that tree, and there is no ceiling at all when SST1521 is switched off — a project
/// that does not police line length should not have its other suggestions withheld.
/// </remarks>
internal static class LineLengthBudget
{
    /// <summary>The layout rule that owns the maximum-line-length setting.</summary>
    private const string LineLengthRuleId = "SST1521";

    /// <summary>Reads the ceiling a rewritten line has to stay within.</summary>
    /// <param name="tree">The syntax tree holding the candidate.</param>
    /// <param name="options">The analyzer options.</param>
    /// <param name="compilation">The active compilation.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The maximum line length, or <see langword="null"/> when no ceiling is in force.</returns>
    public static int? Read(SyntaxTree tree, AnalyzerOptions options, Compilation compilation, CancellationToken cancellationToken)
        => DiagnosticSeverityConfiguration.IsOff(LineLengthRuleId, tree, options, compilation, cancellationToken)
            ? null
            : SizeLimitOptions.ReadMaxLineLength(options.AnalyzerConfigOptionsProvider.GetOptions(tree));

    /// <summary>Returns whether a rewritten line of the given length is allowed.</summary>
    /// <param name="lineLength">The length the line would have after the rewrite.</param>
    /// <param name="tree">The syntax tree holding the candidate.</param>
    /// <param name="options">The analyzer options.</param>
    /// <param name="compilation">The active compilation.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the rewrite fits, or when no ceiling is in force.</returns>
    public static bool Fits(
        int lineLength,
        SyntaxTree tree,
        AnalyzerOptions options,
        Compilation compilation,
        CancellationToken cancellationToken)
        => Read(tree, options, compilation, cancellationToken) is not { } maximum || lineLength <= maximum;
}
