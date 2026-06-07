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

    /// <summary>Returns whether an opening bracket remains on the declaration line of the preceding token.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    /// <returns><see langword="true"/> when the opening token is on the declaration line.</returns>
    internal static bool IsOpeningOnDeclarationLine(SourceText text, SyntaxToken open, int openLine)
    {
        var before = open.GetPreviousToken();
        return before.IsKind(SyntaxKind.None) || LayoutHelpers.EndLine(text, before) == openLine;
    }

    /// <summary>Reports the layout violations for one parameter or argument list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        switch (context.Node)
        {
            case ParameterListSyntax list:
            {
                AnalyzeList(context, text, list.OpenParenToken, list.CloseParenToken, list.Parameters);
                break;
            }

            case BracketedParameterListSyntax list:
            {
                AnalyzeList(context, text, list.OpenBracketToken, list.CloseBracketToken, list.Parameters);
                break;
            }

            case ArgumentListSyntax list:
            {
                AnalyzeList(context, text, list.OpenParenToken, list.CloseParenToken, list.Arguments);
                break;
            }

            case BracketedArgumentListSyntax list:
            {
                AnalyzeList(context, text, list.OpenBracketToken, list.CloseBracketToken, list.Arguments);
                break;
            }

            case AttributeArgumentListSyntax list:
            {
                AnalyzeList(context, text, list.OpenParenToken, list.CloseParenToken, list.Arguments);
                break;
            }
        }
    }

    /// <summary>Analyzes the layout of one separated parameter or argument list.</summary>
    /// <typeparam name="TNode">The item syntax type.</typeparam>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="close">The closing bracket token.</param>
    /// <param name="items">The separated list items.</param>
    private static void AnalyzeList<TNode>(
        SyntaxNodeAnalysisContext context,
        SourceText text,
        SyntaxToken open,
        SyntaxToken close,
        SeparatedSyntaxList<TNode> items)
        where TNode : SyntaxNode
    {
        var openLine = text.Lines.GetLineFromPosition(open.SpanStart).LineNumber;

        CheckOpening(context, text, open, openLine);
        if (items.Count == 0)
        {
            var emptyCloseLine = text.Lines.GetLineFromPosition(close.SpanStart).LineNumber;
            CheckClosing(context, close, openLine, emptyCloseLine, hasItems: false, lastItemEndLine: -1);
            return;
        }

        var lineNumber = openLine;
        var line = text.Lines[lineNumber];
        var firstItem = items[0];
        LayoutHelpers.GetLineSpanOfOrLater(text, firstItem.SpanStart, firstItem.Span.End, ref lineNumber, ref line, out var firstStartLine, out var lastItemEndLine);
        CheckFirstItem(context, firstItem, openLine, firstStartLine, lastItemEndLine);

        for (var index = 1; index < items.Count; index++)
        {
            var comma = items.GetSeparator(index - 1);
            var commaLine = LayoutHelpers.LineOfOrLater(text, comma.SpanStart, ref lineNumber, ref line);
            CheckComma(context, comma, true, lastItemEndLine, commaLine);

            var item = items[index];
            LayoutHelpers.GetLineSpanOfOrLater(text, item.SpanStart, item.Span.End, ref lineNumber, ref line, out var startLine, out var endLine);
            CheckTrailingItem(context, item, startLine, endLine, commaLine);
            lastItemEndLine = endLine;
        }

        var closeLine = LayoutHelpers.LineOfOrLater(text, close.SpanStart, ref lineNumber, ref line);
        CheckClosing(context, close, openLine, closeLine, hasItems: true, lastItemEndLine);
    }

    /// <summary>Reports SST1110 when the opening bracket leaves the line of the preceding token.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="text">The source text.</param>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    private static void CheckOpening(SyntaxNodeAnalysisContext context, SourceText text, SyntaxToken open, int openLine)
    {
        if (IsOpeningOnDeclarationLine(text, open, openLine))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ReadabilityRules.OpeningParenOnDeclarationLine, open.SyntaxTree!, open.Span));
    }

    /// <summary>Reports the first-item rules: spacing after the opening bracket and single-line layout.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="item">The parameter or argument node.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    /// <param name="startLine">The item's starting line.</param>
    /// <param name="endLine">The item's ending line.</param>
    private static void CheckFirstItem(SyntaxNodeAnalysisContext context, SyntaxNode item, int openLine, int startLine, int endLine)
    {
        if (startLine > openLine + 1)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ReadabilityRules.ParameterListFollowsDeclaration, item.SyntaxTree, item.Span));
        }

        ReportIf(context, endLine != startLine && !SpansMultipleLinesByDesign(item), ReadabilityRules.ParameterMustNotSpanMultipleLines, item);
    }

    /// <summary>Reports the later-item rules: spacing after a comma and single-line layout.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="item">The parameter or argument node.</param>
    /// <param name="startLine">The item's starting line.</param>
    /// <param name="endLine">The item's ending line.</param>
    /// <param name="lastCommaLine">The preceding comma's line.</param>
    private static void CheckTrailingItem(SyntaxNodeAnalysisContext context, SyntaxNode item, int startLine, int endLine, int lastCommaLine)
    {
        if (startLine > lastCommaLine + 1)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(ReadabilityRules.ParameterFollowsComma, item.SyntaxTree, item.Span));
        }

        ReportIf(context, endLine != startLine && !SpansMultipleLinesByDesign(item), ReadabilityRules.ParameterMustNotSpanMultipleLines, item);
    }

    /// <summary>Reports SST1113 when a comma leaves the previous item's line.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="comma">The comma token.</param>
    /// <param name="hasPreviousItem">Whether an item precedes the comma.</param>
    /// <param name="previousItemEndLine">The previous item's ending line.</param>
    /// <param name="commaLine">The comma's line.</param>
    private static void CheckComma(
        SyntaxNodeAnalysisContext context,
        SyntaxToken comma,
        bool hasPreviousItem,
        int previousItemEndLine,
        int commaLine)
    {
        if (!hasPreviousItem || previousItemEndLine == commaLine)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ReadabilityRules.CommaOnPreviousParameterLine, comma.SyntaxTree!, comma.Span));
    }

    /// <summary>Reports SST1111/SST1112 for the closing bracket's placement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="close">The closing bracket token.</param>
    /// <param name="openLine">The opening bracket's line.</param>
    /// <param name="closeLine">The closing bracket's line.</param>
    /// <param name="hasItems">Whether the list contains any items.</param>
    /// <param name="lastItemEndLine">The final item's ending line.</param>
    private static void CheckClosing(SyntaxNodeAnalysisContext context, SyntaxToken close, int openLine, int closeLine, bool hasItems, int lastItemEndLine)
    {
        if (!hasItems)
        {
            ReportIf(context, openLine != closeLine, ReadabilityRules.ClosingParenOnOpeningLineWhenEmpty, close);
            return;
        }

        ReportIf(context, lastItemEndLine != closeLine, ReadabilityRules.ClosingParenOnLastParameterLine, close);
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

        context.ReportDiagnostic(DiagnosticHelper.Create(rule, token.SyntaxTree!, token.Span));
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

        context.ReportDiagnostic(DiagnosticHelper.Create(rule, node.SyntaxTree, node.Span));
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
}
