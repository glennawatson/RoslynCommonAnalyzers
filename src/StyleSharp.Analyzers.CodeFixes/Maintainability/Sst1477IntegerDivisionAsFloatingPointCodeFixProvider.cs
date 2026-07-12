// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Promotes an integer division to floating point by casting its left operand (SST1477): <c>a / b</c>
/// becomes <c>(double)a / b</c>. Casting one operand is enough — the other is promoted to match it, so the
/// division runs in floating point and keeps the remainder.
/// </summary>
/// <remarks>
/// When the division was already wrapped in an explicit cast to the same type — <c>(double)(a / b)</c>,
/// which truncates before it widens and is exactly the mistake this rule is about — the fix replaces that
/// cast rather than nesting a second one inside it, and parenthesizes the promoted division whenever the
/// cast sat where an operator could bind to it more tightly than <c>/</c> does.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1477IntegerDivisionAsFloatingPointCodeFixProvider))]
[Shared]
public sealed class Sst1477IntegerDivisionAsFloatingPointCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(MaintainabilityRules.IntegerDivisionAsFloatingPoint.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Divide in floating point",
            nameof(Sst1477IntegerDivisionAsFloatingPointCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Applies one SST1477 promotion for the reported division.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => TryRewrite(root, diagnostic) is { } edit
            ? document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))
            : document;

    /// <summary>Resolves the reported division and builds its floating-point form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(Sst1477IntegerDivisionAsFloatingPointAnalyzer.TargetTypeKey, out var target)
            || GetKeyword(target) is not { } keyword)
        {
            return null;
        }

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.DivideExpression } division)
        {
            return null;
        }

        var promoted = division.WithLeft(BuildCast(keyword, division.Left));
        if (TryGetOuterCast(division, keyword) is not { } cast)
        {
            return new NodeReplacement(division, promoted);
        }

        var replacement = NeedsParenthesesUnder(cast.Parent)
            ? SyntaxFactory.ParenthesizedExpression(promoted.WithoutTrivia())
            : (ExpressionSyntax)promoted;
        return new NodeReplacement(cast, replacement.WithTriviaFrom(cast));
    }

    /// <summary>Builds the cast that promotes the division's left operand.</summary>
    /// <param name="keyword">The floating-point type's keyword.</param>
    /// <param name="left">The division's left operand.</param>
    /// <returns>The cast expression, carrying the operand's own trivia.</returns>
    /// <remarks>
    /// A cast binds tighter than any binary operator, so an operand that is itself an operation keeps its
    /// grouping: <c>a * b / c</c> promotes to <c>(double)(a * b) / c</c>, which still multiplies in
    /// integers, and not to <c>(double)a * b / c</c>, which does not.
    /// </remarks>
    private static CastExpressionSyntax BuildCast(SyntaxKind keyword, ExpressionSyntax left)
    {
        ExpressionSyntax operand = left.WithoutTrivia();
        if (NeedsParenthesesUnderCast(left))
        {
            operand = SyntaxFactory.ParenthesizedExpression(operand);
        }

        return SyntaxFactory.CastExpression(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(keyword)),
                operand)
            .WithTriviaFrom(left);
    }

    /// <summary>Gets the explicit cast the division only exists to feed, when it targets the same type.</summary>
    /// <param name="division">The reported division.</param>
    /// <param name="keyword">The floating-point type's keyword.</param>
    /// <returns>The cast to replace, or <see langword="null"/> when the widening was implicit.</returns>
    private static CastExpressionSyntax? TryGetOuterCast(BinaryExpressionSyntax division, SyntaxKind keyword)
    {
        SyntaxNode node = division;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        return node.Parent is CastExpressionSyntax { Type: PredefinedTypeSyntax predefined } cast
            && predefined.Keyword.RawKind == (int)keyword
                ? cast
                : null;
    }

    /// <summary>Returns whether a cast's operand must keep its grouping.</summary>
    /// <param name="operand">The operand being cast.</param>
    /// <returns><see langword="true"/> for anything that binds looser than a cast.</returns>
    private static bool NeedsParenthesesUnderCast(ExpressionSyntax operand)
        => operand is BinaryExpressionSyntax
            or ConditionalExpressionSyntax
            or AssignmentExpressionSyntax
            or IsPatternExpressionSyntax
            or SwitchExpressionSyntax;

    /// <summary>Returns whether a division put where a cast used to sit must be parenthesized.</summary>
    /// <param name="parent">The replaced cast's parent.</param>
    /// <returns><see langword="true"/> when an operator in the parent could bind tighter than <c>/</c>.</returns>
    /// <remarks>
    /// A cast is a unary expression and binds tighter than almost everything, so anything that accepted one
    /// as an operand may bind more tightly than the division that replaces it. Only the positions that take
    /// a whole value — an argument, an initializer, a return, the right side of an assignment, an existing
    /// pair of parentheses — are safe to drop into unparenthesized.
    /// </remarks>
    private static bool NeedsParenthesesUnder(SyntaxNode? parent)
        => parent is ExpressionSyntax
            and not ParenthesizedExpressionSyntax
            and not AssignmentExpressionSyntax
            and not InitializerExpressionSyntax;

    /// <summary>Maps the diagnostic's target-type property to the keyword the cast is written with.</summary>
    /// <param name="target">The reported floating-point type.</param>
    /// <returns>The keyword kind, or <see langword="null"/> when the property is missing or unknown.</returns>
    private static SyntaxKind? GetKeyword(string? target) => target switch
    {
        Sst1477IntegerDivisionAsFloatingPointAnalyzer.FloatName => SyntaxKind.FloatKeyword,
        Sst1477IntegerDivisionAsFloatingPointAnalyzer.DoubleName => SyntaxKind.DoubleKeyword,
        Sst1477IntegerDivisionAsFloatingPointAnalyzer.DecimalName => SyntaxKind.DecimalKeyword,
        _ => null,
    };
}
