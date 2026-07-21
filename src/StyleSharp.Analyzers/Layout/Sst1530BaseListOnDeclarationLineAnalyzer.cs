// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type declaration whose base list starts on its own line (SST1530), for example a
/// <c>class Foo</c> followed on the next line by <c>: Bar</c>. The rule fires only when the base list is
/// itself single-line and the declaration and its bases would fit within the configured maximum line
/// length once joined, so a base list that only fits by wrapping keeps its own line.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1530BaseListOnDeclarationLineAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.BaseListOnDeclarationLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.BaseList);
    }

    /// <summary>Reports a base list pushed onto its own line that could be joined to the declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var baseList = (BaseListSyntax)context.Node;
        var colon = baseList.ColonToken;
        if (colon.IsMissing || !LayoutHelpers.HasLineBreakBefore(colon))
        {
            return;
        }

        var text = baseList.SyntaxTree.GetText(context.CancellationToken);
        var lastToken = baseList.GetLastToken();
        if (LayoutHelpers.LineOf(text, colon.SpanStart) != LayoutHelpers.EndLine(text, lastToken))
        {
            return;
        }

        var previous = colon.GetPreviousToken();
        var prefixLength = previous.Span.End - text.Lines.GetLineFromPosition(previous.Span.End).Start;
        var joinedLength = prefixLength + 1 + (lastToken.Span.End - colon.SpanStart);
        var maximum = SizeLimitOptions.ReadMaxLineLength(context.Options.AnalyzerConfigOptionsProvider.GetOptions(baseList.SyntaxTree));
        if (joinedLength > maximum)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BaseListOnDeclarationLine, baseList.GetLocation()));
    }
}
