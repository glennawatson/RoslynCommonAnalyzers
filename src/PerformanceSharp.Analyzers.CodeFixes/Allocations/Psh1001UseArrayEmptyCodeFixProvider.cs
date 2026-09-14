// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a zero-length array allocation (PSH1001) with an empty collection
/// expression when the analyzer marked the position as array-target-typed on C# 12+
/// (via the diagnostic's properties), and with <c>System.Array.Empty&lt;T&gt;()</c>
/// otherwise, reusing the creation's element type syntax as the type argument.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1001UseArrayEmptyCodeFixProvider))]
[Shared]
public sealed class Psh1001UseArrayEmptyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UseArrayEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Psh1001UseArrayEmptyCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported array creation and builds its empty replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the creation no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        FindCreation(root, diagnostic) is { } creation
            ? new NodeReplacement(creation, Rewrite(creation, UsesCollectionExpression(diagnostic)))
            : null;

    /// <summary>Resolves the array creation the diagnostic reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported creation, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArrayCreationExpressionSyntax? FindCreation(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArrayCreationExpressionSyntax>();

    /// <summary>Words the action for the replacement the analyzer chose for the reported creation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the creation no longer matches.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        FindCreation(root, diagnostic) switch
        {
            null => null,
            _ when UsesCollectionExpression(diagnostic) => "Use an empty collection expression",
            _ => "Use Array.Empty<T>()",
        };

    /// <summary>Returns whether the analyzer marked this diagnostic for a collection-expression replacement.</summary>
    /// <param name="diagnostic">The reported diagnostic.</param>
    /// <returns><see langword="true"/> when the fix should emit <c>[]</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UsesCollectionExpression(Diagnostic diagnostic) =>
        diagnostic.Properties.ContainsKey(Psh1001UseArrayEmptyAnalyzer.UseCollectionExpressionKey);

    /// <summary>Rewrites the creation to <c>[]</c> or a fully-qualified <c>System.Array.Empty&lt;T&gt;()</c> invocation.</summary>
    /// <param name="creation">The array creation to rewrite.</param>
    /// <param name="useCollectionExpression">Whether to emit <c>[]</c> instead of <c>System.Array.Empty&lt;T&gt;()</c>.</param>
    /// <returns>The replacement expression, carrying the creation's surrounding trivia.</returns>
    private static ExpressionSyntax Rewrite(ArrayCreationExpressionSyntax creation, bool useCollectionExpression) =>
        useCollectionExpression
            ? SyntaxFactory.CollectionExpression(
                SyntaxFactory.Token(creation.GetLeadingTrivia(), SyntaxKind.OpenBracketToken, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
                default,
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseBracketToken, creation.GetTrailingTrivia()))
            : CreateArrayEmptyInvocation(creation);

    /// <summary>Rewrites the creation to a fully-qualified <c>System.Array.Empty&lt;T&gt;()</c> invocation.</summary>
    /// <param name="creation">The array creation to rewrite.</param>
    /// <returns>The replacement invocation, carrying the creation's surrounding trivia.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static InvocationExpressionSyntax CreateArrayEmptyInvocation(ArrayCreationExpressionSyntax creation) =>
        SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                        creation.GetLeadingTrivia(),
                        "System",
                        SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker))),
                    SyntaxFactory.IdentifierName(nameof(Array))),
                SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("Empty"),
                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(GetElementTypeSyntax(creation.Type))))),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                default,
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, creation.GetTrailingTrivia())));

    /// <summary>Builds the <c>Array.Empty</c> type argument from the creation's array type syntax.</summary>
    /// <param name="arrayType">The created array type.</param>
    /// <returns>The element type for a rank-1 creation, or the inner array type for a jagged creation.</returns>
    private static TypeSyntax GetElementTypeSyntax(ArrayTypeSyntax arrayType)
    {
        var elementType = arrayType.ElementType.WithoutTrivia();
        if (arrayType.RankSpecifiers.Count <= 1)
        {
            return elementType;
        }

        var innerRankSpecifiers = new ArrayRankSpecifierSyntax[arrayType.RankSpecifiers.Count - 1];
        for (var i = 1; i < arrayType.RankSpecifiers.Count; i++)
        {
            innerRankSpecifiers[i - 1] = arrayType.RankSpecifiers[i].WithoutTrivia();
        }

        return SyntaxFactory.ArrayType(elementType, SyntaxFactory.List(innerRankSpecifiers));
    }
}
