// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Merges a bulk add into the preceding empty creation (PSH1112). The default rewrite passes the
/// source to the constructor: <c>var x = new List&lt;T&gt;(src);</c>. When the analyzer stamped
/// the diagnostic with the collection-expression property (C# 12+, explicitly typed declaration,
/// preference enabled), the initializer becomes a spread instead: <c>List&lt;T&gt; x = [.. src];</c>.
/// The bulk-add statement is removed either way.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1112SeedCollectionFromSourceCodeFixProvider))]
[Shared]
public sealed class Psh1112SeedCollectionFromSourceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.SeedCollectionFromSource.Id);

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
            if (TryGetShape(root, diagnostic) is not { } shape)
            {
                continue;
            }

            var useCollectionExpression = UsesCollectionExpression(diagnostic);
            context.RegisterCodeFix(
                CodeAction.Create(
                    useCollectionExpression ? "Seed with a collection expression" : "Seed through the constructor",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, shape, useCollectionExpression)),
                    equivalenceKey: nameof(Psh1112SeedCollectionFromSourceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetShape(editor.OriginalRoot, diagnostic) is not { } shape)
        {
            return;
        }

        editor.ReplaceNode(shape.Creation, BuildInitializer(shape, UsesCollectionExpression(diagnostic)));
        editor.RemoveNode(shape.Statement, SyntaxRemoveOptions.KeepUnbalancedDirectives);
    }

    /// <summary>Returns whether the diagnostic asks for the collection-expression form.</summary>
    /// <param name="diagnostic">The diagnostic to inspect.</param>
    /// <returns><see langword="true"/> when the analyzer stamped the collection-expression property.</returns>
    private static bool UsesCollectionExpression(Diagnostic diagnostic)
        => diagnostic.Properties.ContainsKey(Psh1112SeedCollectionFromSourceAnalyzer.UseCollectionExpressionKey);

    /// <summary>Resolves the reported invocation back into the declaration/creation/statement triple.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The shape, or <see langword="null"/> when it no longer matches.</returns>
    private static SeedShape? TryGetShape(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || !Psh1112SeedCollectionFromSourceAnalyzer.TryGetSeedShape(invocation, out _, out var creation))
        {
            return null;
        }

        return new SeedShape(
            creation,
            (ExpressionStatementSyntax)invocation.Parent!,
            invocation.ArgumentList.Arguments[0].Expression);
    }

    /// <summary>Applies both edits — seed the initializer, drop the bulk-add statement — to the root.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="shape">The resolved seed shape.</param>
    /// <param name="useCollectionExpression">Whether to emit a spread collection expression.</param>
    /// <returns>The updated document.</returns>
    private static Document Apply(Document document, SyntaxNode root, SeedShape shape, bool useCollectionExpression)
    {
        var tracked = root.TrackNodes(shape.Creation, shape.Statement);
        var currentCreation = tracked.GetCurrentNode(shape.Creation)!;
        tracked = tracked.ReplaceNode(currentCreation, BuildInitializer(shape, useCollectionExpression));
        var currentStatement = tracked.GetCurrentNode(shape.Statement)!;
        tracked = tracked.RemoveNode(currentStatement, SyntaxRemoveOptions.KeepUnbalancedDirectives)!;
        return document.WithSyntaxRoot(tracked);
    }

    /// <summary>Builds the seeded initializer value.</summary>
    /// <param name="shape">The resolved seed shape.</param>
    /// <param name="useCollectionExpression">Whether to emit a spread collection expression.</param>
    /// <returns><c>new T(source)</c> or <c>[.. source]</c>.</returns>
    private static ExpressionSyntax BuildInitializer(SeedShape shape, bool useCollectionExpression)
    {
        var source = shape.Source.WithoutTrivia();
        if (useCollectionExpression)
        {
            var spread = SyntaxFactory.SpreadElement(
                SyntaxFactory.Token(SyntaxKind.DotDotToken).WithTrailingTrivia(SyntaxFactory.Space),
                source);
            return SyntaxFactory.CollectionExpression(SyntaxFactory.SingletonSeparatedList<CollectionElementSyntax>(spread))
                .WithTriviaFrom(shape.Creation);
        }

        return shape.Creation.WithArgumentList(
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(source))));
    }

    /// <summary>The syntax pieces a seed rewrite touches.</summary>
    /// <param name="Creation">The empty creation initializing the local.</param>
    /// <param name="Statement">The bulk-add statement to remove.</param>
    /// <param name="Source">The bulk-add source expression.</param>
    private readonly record struct SeedShape(
        BaseObjectCreationExpressionSyntax Creation,
        ExpressionStatementSyntax Statement,
        ExpressionSyntax Source);
}
