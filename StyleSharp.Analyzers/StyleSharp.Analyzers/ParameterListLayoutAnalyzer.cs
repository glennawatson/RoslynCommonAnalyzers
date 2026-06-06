// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the line-placement rules for parameter and argument lists (SST1110–SST1115, SST1118):
/// the opening bracket stays on the declaration line, the closing bracket follows the last item (or
/// the opening bracket when empty), commas stay on the previous item's line, no blank line splits
/// the list, and a single item does not span multiple lines. Each list is walked once, tracking the
/// previous item and comma, so the whole family shares a single pass with no intermediate collection.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParameterListLayoutAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The list node kinds the rules inspect.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ParameterList,
        SyntaxKind.BracketedParameterList,
        SyntaxKind.ArgumentList,
        SyntaxKind.BracketedArgumentList,
        SyntaxKind.AttributeArgumentList);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.OpeningParenOnDeclarationLine,
        ReadabilityRules.ClosingParenOnLastParameterLine,
        ReadabilityRules.ClosingParenOnOpeningLineWhenEmpty,
        ReadabilityRules.CommaOnPreviousParameterLine,
        ReadabilityRules.ParameterListFollowsDeclaration,
        ReadabilityRules.ParameterFollowsComma,
        ReadabilityRules.ParameterMustNotSpanMultipleLines);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports the layout violations for one parameter or argument list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        if (!TryGetBrackets(node, out var open, out var close))
        {
            return;
        }

        var text = node.SyntaxTree.GetText(context.CancellationToken);
        var openLine = LineOf(text, open.SpanStart);
        var closeLine = LineOf(text, close.SpanStart);

        CheckOpening(context, text, open, openLine);

        SyntaxNode? firstItem = null;
        SyntaxNode? lastItem = null;
        SyntaxToken lastComma = default;
        var sawComma = false;
        foreach (var child in node.ChildNodesAndTokens())
        {
            if (child.IsNode)
            {
                var item = child.AsNode()!;
                CheckItem(context, text, item, firstItem is null, openLine, lastComma, sawComma);
                firstItem ??= item;
                lastItem = item;
            }
            else if (child.AsToken() is { RawKind: (int)SyntaxKind.CommaToken } comma)
            {
                CheckComma(context, text, comma, lastItem);
                lastComma = comma;
                sawComma = true;
            }
        }

        CheckClosing(context, text, close, openLine, closeLine, lastItem);
    }

    /// <summary>Reports SST1110 when the opening bracket leaves the line of the preceding token.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    private static void CheckOpening(SyntaxNodeAnalysisContext context, SourceText text, SyntaxToken open, int openLine)
    {
        var before = open.GetPreviousToken();
        if (before.IsKind(SyntaxKind.None) || LineOf(text, before.Span.End) == openLine)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.OpeningParenOnDeclarationLine, open.GetLocation()));
    }

    /// <summary>Reports the per-item rules: blank line before the item (SST1114/SST1115) and multi-line item (SST1118).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="item">The parameter or argument node.</param>
    /// <param name="isFirst">Whether this is the first item in the list.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    /// <param name="lastComma">The preceding comma (valid only when <paramref name="sawComma"/> is true).</param>
    /// <param name="sawComma">Whether a comma preceded this item.</param>
    private static void CheckItem(SyntaxNodeAnalysisContext context, SourceText text, SyntaxNode item, bool isFirst, int openLine, SyntaxToken lastComma, bool sawComma)
    {
        var startLine = LineOf(text, item.SpanStart);
        if (isFirst && startLine > openLine + 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ParameterListFollowsDeclaration, item.GetLocation()));
        }
        else if (!isFirst && sawComma && startLine > LineOf(text, lastComma.SpanStart) + 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ParameterFollowsComma, item.GetLocation()));
        }

        ReportIf(context, LineOf(text, item.Span.End) != startLine && !SpansMultipleLinesByDesign(item), ReadabilityRules.ParameterMustNotSpanMultipleLines, item);
    }

    /// <summary>Reports SST1113 when a comma leaves the previous item's line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="comma">The comma token.</param>
    /// <param name="previousItem">The item before the comma.</param>
    private static void CheckComma(SyntaxNodeAnalysisContext context, SourceText text, SyntaxToken comma, SyntaxNode? previousItem)
    {
        if (previousItem is null || LineOf(text, previousItem.Span.End) == LineOf(text, comma.SpanStart))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.CommaOnPreviousParameterLine, comma.GetLocation()));
    }

    /// <summary>Reports SST1111/SST1112 for the closing bracket's placement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="close">The closing bracket token.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    /// <param name="closeLine">The closing bracket's line.</param>
    /// <param name="lastItem">The final item, or <see langword="null"/> for an empty list.</param>
    private static void CheckClosing(SyntaxNodeAnalysisContext context, SourceText text, SyntaxToken close, int openLine, int closeLine, SyntaxNode? lastItem)
    {
        if (lastItem is null)
        {
            ReportIf(context, openLine != closeLine, ReadabilityRules.ClosingParenOnOpeningLineWhenEmpty, close);
            return;
        }

        ReportIf(context, LineOf(text, lastItem.Span.End) != closeLine, ReadabilityRules.ClosingParenOnLastParameterLine, close);
    }

    /// <summary>Reports a diagnostic on a token when the condition holds.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="condition">Whether to report.</param>
    /// <param name="rule">The descriptor to report.</param>
    /// <param name="token">The token to flag.</param>
    private static void ReportIf(SyntaxNodeAnalysisContext context, bool condition, DiagnosticDescriptor rule, SyntaxToken token)
    {
        if (!condition)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, token.GetLocation()));
    }

    /// <summary>Reports a diagnostic on a node when the condition holds.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="condition">Whether to report.</param>
    /// <param name="rule">The descriptor to report.</param>
    /// <param name="node">The node to flag.</param>
    private static void ReportIf(SyntaxNodeAnalysisContext context, bool condition, DiagnosticDescriptor rule, SyntaxNode node)
    {
        if (!condition)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, node.GetLocation()));
    }

    /// <summary>Returns whether an item legitimately spans multiple lines (a multi-line callback, initializer, or similar).</summary>
    /// <param name="item">The parameter or argument node.</param>
    /// <returns><see langword="true"/> when the item is exempt from the single-line requirement.</returns>
    private static bool SpansMultipleLinesByDesign(SyntaxNode item)
    {
        var expression = item switch
        {
            ArgumentSyntax argument => argument.Expression,
            AttributeArgumentSyntax attributeArgument => attributeArgument.Expression,
            _ => null,
        };

        return expression is null || IsMultiLineFriendlyKind(expression.Kind());
    }

    /// <summary>Returns whether an expression kind is conventionally allowed to span multiple lines in an argument list.</summary>
    /// <param name="kind">The expression kind.</param>
    /// <returns><see langword="true"/> when the kind is exempt from SST1118.</returns>
    private static bool IsMultiLineFriendlyKind(SyntaxKind kind)
        => IsLambdaOrAnonymousKind(kind)
            || IsCreationOrInitializerKind(kind)
            || IsOtherMultiLineFriendlyKind(kind);

    /// <summary>Returns whether the kind is a lambda or anonymous-form expression that commonly spans multiple lines.</summary>
    /// <param name="kind">The expression kind.</param>
    /// <returns><see langword="true"/> when exempt.</returns>
    private static bool IsLambdaOrAnonymousKind(SyntaxKind kind)
        => kind is SyntaxKind.SimpleLambdaExpression
            or SyntaxKind.ParenthesizedLambdaExpression
            or SyntaxKind.AnonymousMethodExpression
            or SyntaxKind.AnonymousObjectCreationExpression;

    /// <summary>Returns whether the kind is an object/array creation or initializer that commonly spans multiple lines.</summary>
    /// <param name="kind">The expression kind.</param>
    /// <returns><see langword="true"/> when exempt.</returns>
    private static bool IsCreationOrInitializerKind(SyntaxKind kind)
        => kind is SyntaxKind.ObjectCreationExpression
            or SyntaxKind.ImplicitObjectCreationExpression
            or SyntaxKind.ArrayCreationExpression
            or SyntaxKind.ImplicitArrayCreationExpression
            or SyntaxKind.ComplexElementInitializerExpression
            or SyntaxKind.ObjectInitializerExpression
            or SyntaxKind.CollectionInitializerExpression
            or SyntaxKind.ArrayInitializerExpression;

    /// <summary>Returns whether the kind is another multi-line-friendly expression form.</summary>
    /// <param name="kind">The expression kind.</param>
    /// <returns><see langword="true"/> when exempt.</returns>
    private static bool IsOtherMultiLineFriendlyKind(SyntaxKind kind)
        => kind is SyntaxKind.SwitchExpression
            or SyntaxKind.QueryExpression
            or SyntaxKind.InterpolatedStringExpression;

    /// <summary>Extracts the opening and closing bracket tokens of a parameter or argument list.</summary>
    /// <param name="node">The list node.</param>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="close">The closing bracket token.</param>
    /// <returns><see langword="true"/> when the node is a supported list.</returns>
    private static bool TryGetBrackets(SyntaxNode node, out SyntaxToken open, out SyntaxToken close)
    {
        var brackets = node switch
        {
            ParameterListSyntax list => (list.OpenParenToken, list.CloseParenToken),
            BracketedParameterListSyntax list => (list.OpenBracketToken, list.CloseBracketToken),
            ArgumentListSyntax list => (list.OpenParenToken, list.CloseParenToken),
            BracketedArgumentListSyntax list => (list.OpenBracketToken, list.CloseBracketToken),
            AttributeArgumentListSyntax list => (list.OpenParenToken, list.CloseParenToken),
            _ => default((SyntaxToken Open, SyntaxToken Close)?),
        };

        (open, close) = brackets ?? default;
        return brackets is not null;
    }

    /// <summary>Returns the zero-based line number for a position.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="position">The position to look up.</param>
    /// <returns>The line number.</returns>
    private static int LineOf(SourceText text, int position) => text.Lines.GetLineFromPosition(position).LineNumber;
}
