// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>Reports one or more blank lines at the very start of the file (SST1517).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1517FileStartBlankLinesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.NoBlankLinesAtStartOfFile);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports the leading run of blank lines when the file begins with one.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        if (text.Lines.Count == 0 || !LayoutHelpers.IsBlankLine(text, 0))
        {
            return;
        }

        var last = 0;
        while (last + 1 < text.Lines.Count && LayoutHelpers.IsBlankLine(text, last + 1))
        {
            last++;
        }

        var span = TextSpan.FromBounds(text.Lines[0].Start, text.Lines[last].EndIncludingLineBreak);
        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.NoBlankLinesAtStartOfFile, Location.Create(context.Tree, span)));
    }
}
