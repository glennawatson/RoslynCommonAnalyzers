// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a file with more code lines than the configured maximum (SST1522), which defaults to 500 and is
/// configured with <c>stylesharp.SST1522.max_file_lines</c>.
/// </summary>
/// <remarks>
/// Blank lines, comments, documentation headers and directive-only lines do not count; a line counts when a
/// token sits on it. That keeps the limit a measure of code rather than a tax on documenting it — a file does
/// not become too long by being explained. The raw line count of the file is an upper bound on its code lines,
/// so a file inside the limit is rejected on one comparison and the token walk never runs.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1522FileTooLongAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.FileTooLong);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports a file whose code lines exceed the configured maximum.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var maximum = SizeLimitOptions.ReadMaxFileLines(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree));
        var text = context.Tree.GetText(context.CancellationToken);
        if (text.Lines.Count <= maximum)
        {
            return;
        }

        var root = context.Tree.GetRoot(context.CancellationToken);
        var codeLines = CodeLineCounter.Count(text, root);
        if (codeLines <= maximum)
        {
            return;
        }

        var first = root.GetFirstToken();
        if (first.IsKind(SyntaxKind.None))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.FileTooLong, first.GetLocation(), codeLines, maximum));
    }
}
