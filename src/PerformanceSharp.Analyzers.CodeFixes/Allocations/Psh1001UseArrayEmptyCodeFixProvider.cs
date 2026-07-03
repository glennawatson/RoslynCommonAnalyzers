// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a zero-length array allocation (PSH1001) with an empty collection
/// expression when the analyzer marked the position as array-target-typed on C# 12+
/// (via the diagnostic's properties), and with <c>System.Array.Empty&lt;T&gt;()</c>
/// otherwise, reusing the creation's element type syntax as the type argument.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1001UseArrayEmptyCodeFixProvider))]
[Shared]
public sealed class Psh1001UseArrayEmptyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UseArrayEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArrayCreationExpressionSyntax>() is not { } creation)
            {
                continue;
            }

            var useCollectionExpression = UsesCollectionExpression(diagnostic);
            context.RegisterCodeFix(
                CodeAction.Create(
                    useCollectionExpression ? "Use an empty collection expression" : "Use Array.Empty<T>()",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, creation, useCollectionExpression)),
                    equivalenceKey: nameof(Psh1001UseArrayEmptyCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArrayCreationExpressionSyntax>() is not { } creation)
        {
            return;
        }

        editor.ReplaceNode(creation, Rewrite(creation, UsesCollectionExpression(diagnostic)));
    }

    /// <summary>Replaces the reported array creation with its shared-empty-array form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="creation">The array creation to rewrite.</param>
    /// <param name="useCollectionExpression">Whether to emit <c>[]</c> instead of <c>System.Array.Empty&lt;T&gt;()</c>.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ArrayCreationExpressionSyntax creation, bool useCollectionExpression)
        => document.WithSyntaxRoot(root.ReplaceNode(creation, Rewrite(creation, useCollectionExpression)));

    /// <summary>Returns whether the analyzer marked this diagnostic for a collection-expression replacement.</summary>
    /// <param name="diagnostic">The reported diagnostic.</param>
    /// <returns><see langword="true"/> when the fix should emit <c>[]</c>.</returns>
    private static bool UsesCollectionExpression(Diagnostic diagnostic)
        => diagnostic.Properties.ContainsKey(Psh1001UseArrayEmptyAnalyzer.UseCollectionExpressionKey);

    /// <summary>Rewrites the creation to <c>[]</c> or a fully-qualified <c>System.Array.Empty&lt;T&gt;()</c> invocation.</summary>
    /// <param name="creation">The array creation to rewrite.</param>
    /// <param name="useCollectionExpression">Whether to emit <c>[]</c> instead of <c>System.Array.Empty&lt;T&gt;()</c>.</param>
    /// <returns>The replacement expression, carrying the creation's surrounding trivia.</returns>
    private static ExpressionSyntax Rewrite(ArrayCreationExpressionSyntax creation, bool useCollectionExpression)
        => useCollectionExpression
            ? SyntaxFactory.CollectionExpression().WithTriviaFrom(creation)
            : CreateArrayEmptyInvocation(creation);

    /// <summary>Rewrites the creation to a fully-qualified <c>System.Array.Empty&lt;T&gt;()</c> invocation.</summary>
    /// <param name="creation">The array creation to rewrite.</param>
    /// <returns>The replacement invocation, carrying the creation's surrounding trivia.</returns>
    private static InvocationExpressionSyntax CreateArrayEmptyInvocation(ArrayCreationExpressionSyntax creation)
        => SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Array")),
                    SyntaxFactory.GenericName(
                        SyntaxFactory.Identifier("Empty"),
                        SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(GetElementTypeSyntax(creation.Type))))))
            .WithTriviaFrom(creation);

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
