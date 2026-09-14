// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an unnecessary cast (SST1175), keeping the expression it applied to. The rule reports three
/// shapes — a cast expression, an <c>as</c> test, and a sequence re-typing call — and each collapses to
/// the operand or receiver it was wrapping.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantCastCodeFixProvider))]
[Shared]
public sealed class RedundantCastCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Batches this fix's edits across a document, rewriting each conversion from the form nested edits left it in
    /// so an outer cast wrapping an inner one resolves in one pass.
    /// </summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindConversion, static (current, _) => RemoveConversion(current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantCast.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Remove the unnecessary cast",
            nameof(RedundantCastCodeFixProvider),
            FindConversion,
            RemoveConversion);

    /// <summary>Returns the expression a conversion was wrapping, carrying the conversion's trivia.</summary>
    /// <param name="conversion">The conversion node.</param>
    /// <returns>The operand or receiver that survives removing the conversion.</returns>
    internal static ExpressionSyntax RemoveConversion(SyntaxNode conversion)
    {
        var kept = conversion switch
        {
            CastExpressionSyntax cast => cast.Expression,
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) => binary.Left,
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Expression,
            _ => (ExpressionSyntax)conversion,
        };
        return kept.WithTriviaFrom(conversion);
    }

    /// <summary>Resolves the reported conversion: a cast expression, an <c>as</c> test, or a sequence re-typing call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The conversion node, or <see langword="null"/> when the shape no longer matches.</returns>
    private static SyntaxNode? FindConversion(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node.FirstAncestorOrSelf<CastExpressionSyntax>() is { } cast)
        {
            return cast;
        }

        if (node.FirstAncestorOrSelf<BinaryExpressionSyntax>() is { } asExpression && asExpression.IsKind(SyntaxKind.AsExpression))
        {
            return asExpression;
        }

        return node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { Expression: MemberAccessExpressionSyntax } invocation
            ? invocation
            : null;
    }
}
