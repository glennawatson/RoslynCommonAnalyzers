// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads whether a diagnostic id is switched off for one syntax tree.</summary>
internal static class DiagnosticSeverityConfiguration
{
    /// <summary>Returns whether the active configuration disables a diagnostic id.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <param name="tree">The syntax tree.</param>
    /// <param name="options">The analyzer options.</param>
    /// <param name="compilation">The active compilation.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when severity is configured to none or silent.</returns>
    /// <remarks>
    /// The severity has to be read through the <see cref="SyntaxTreeOptionsProvider"/>, because that is
    /// where the compiler puts it. A <c>dotnet_diagnostic.&lt;id&gt;.severity</c> entry is a severity
    /// configuration rather than an analyzer option, so it is routed to the per-tree diagnostic options and
    /// is <b>not</b> handed back by <c>AnalyzerConfigOptionsProvider.GetOptions</c> — asking there finds
    /// nothing and silently reports nothing. The command-line and ruleset path
    /// (<see cref="CompilationOptions.SpecificDiagnosticOptions"/>) is still checked, since a <c>NoWarn</c>
    /// disables a diagnostic just as completely.
    /// </remarks>
    public static bool IsOff(
        string diagnosticId,
        SyntaxTree tree,
        AnalyzerOptions options,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        if (compilation.Options.SpecificDiagnosticOptions.TryGetValue(diagnosticId, out var reportDiagnostic)
            && IsOff(reportDiagnostic))
        {
            return true;
        }

        if (compilation.Options.SyntaxTreeOptionsProvider is { } treeOptions
            && treeOptions.TryGetDiagnosticValue(tree, diagnosticId, cancellationToken, out var configured)
            && IsOff(configured))
        {
            return true;
        }

        var config = options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        return config.TryGetValue("dotnet_diagnostic." + diagnosticId + ".severity", out var severity)
            && (StringComparer.OrdinalIgnoreCase.Equals(severity, "none")
                || StringComparer.OrdinalIgnoreCase.Equals(severity, "silent"));
    }

    /// <summary>Returns whether a configured severity means the diagnostic can never be reported.</summary>
    /// <param name="report">The configured severity.</param>
    /// <returns><see langword="true"/> for <c>none</c> and <c>silent</c>.</returns>
    private static bool IsOff(ReportDiagnostic report)
        => report is ReportDiagnostic.Suppress or ReportDiagnostic.Hidden;
}
