// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an object or collection initializer split across several lines that would fit within the
/// configured maximum line length once collapsed (SST1531). Only initializers are targeted — a collapsed
/// statement block is instead expanded by the single-line block rules. An initializer that carries a
/// comment between its elements, or that would still run past the line limit collapsed, is left alone.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1531InitializerOnSingleLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The initializer expression kinds whose multi-line layout is checked.</summary>
    private static readonly ImmutableArray<SyntaxKind> InitializerKinds = ImmutableArrays.Of(
        SyntaxKind.ObjectInitializerExpression,
        SyntaxKind.CollectionInitializerExpression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.InitializerOnSingleLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, InitializerKinds);
    }

    /// <summary>Reports a multi-line initializer that fits on one line once collapsed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var initializer = (InitializerExpressionSyntax)context.Node;
        var open = initializer.OpenBraceToken;
        var close = initializer.CloseBraceToken;
        if (initializer.Expressions.Count == 0 || open.IsMissing || close.IsMissing)
        {
            return;
        }

        var text = initializer.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.LineOf(text, open.SpanStart) == LayoutHelpers.LineOf(text, close.SpanStart))
        {
            return;
        }

        if (!TryMeasureCollapsedLength(text, open, close, out var collapsedLength))
        {
            return;
        }

        var maximum = SizeLimitOptions.ReadMaxLineLength(context.Options.AnalyzerConfigOptionsProvider.GetOptions(initializer.SyntaxTree));
        if (collapsedLength > maximum)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.InitializerOnSingleLine, open.GetLocation()));
    }

    /// <summary>Measures the line length the construct would occupy once the initializer is collapsed.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="open">The initializer's opening brace.</param>
    /// <param name="close">The initializer's closing brace.</param>
    /// <param name="collapsedLength">The collapsed line length.</param>
    /// <returns><see langword="false"/> when a comment on a wrapped gap blocks collapsing.</returns>
    private static bool TryMeasureCollapsedLength(SourceText text, SyntaxToken open, SyntaxToken close, out int collapsedLength)
    {
        collapsedLength = 0;
        var previous = open.GetPreviousToken();
        var length = previous.Span.End - text.Lines.GetLineFromPosition(previous.Span.End).Start;
        var token = previous;
        while (!token.IsKind(SyntaxKind.None))
        {
            var next = token.GetNextToken();
            LayoutHelpers.ClassifyGap(text, token.Span.End, next.SpanStart, out var hasLineBreak, out var isClean);
            if (hasLineBreak && !isClean)
            {
                return false;
            }

            int separator;
            if (hasLineBreak)
            {
                separator = next.IsKind(SyntaxKind.CommaToken) ? 0 : 1;
            }
            else
            {
                separator = next.SpanStart - token.Span.End;
            }

            length += separator + next.Span.Length;
            if (next.Equals(close))
            {
                break;
            }

            token = next;
        }

        collapsedLength = length;
        return true;
    }
}
