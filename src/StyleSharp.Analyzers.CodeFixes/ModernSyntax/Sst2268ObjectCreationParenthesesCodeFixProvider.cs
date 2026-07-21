// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds or removes the empty argument parentheses on an object creation with an initializer (SST2268),
/// following the reported node's current shape — the analyzer only reports the form that does not match the
/// configured style, so a creation that still has empty parentheses gets them removed and one that has none
/// gets them added. The type, the initializer, and the surrounding trivia are preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2268ObjectCreationParenthesesCodeFixProvider))]
[Shared]
public sealed class Sst2268ObjectCreationParenthesesCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeObjectCreationParentheses.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Normalize the object-creation parentheses", nameof(Sst2268ObjectCreationParenthesesCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported creation and flips its parentheses.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ObjectCreationExpressionSyntax>() is not { } creation
            || !Sst2268ObjectCreationParenthesesAnalyzer.IsCandidate(creation))
        {
            return null;
        }

        return new NodeReplacement(creation, Flip(creation), RewriteCurrent);
    }

    /// <summary>Flips the current creation's parentheses during batch FixAll composition.</summary>
    /// <param name="current">The current creation node, possibly carrying nested edits.</param>
    /// <returns>The flipped creation, or the node unchanged when the shape no longer matches.</returns>
    private static SyntaxNode RewriteCurrent(SyntaxNode current)
        => current is ObjectCreationExpressionSyntax creation && Sst2268ObjectCreationParenthesesAnalyzer.IsCandidate(creation)
            ? Flip(creation)
            : current;

    /// <summary>Builds the creation with its empty parentheses added or removed.</summary>
    /// <param name="creation">The reported creation; callers must have validated the shape.</param>
    /// <returns>The rewritten creation.</returns>
    private static ObjectCreationExpressionSyntax Flip(ObjectCreationExpressionSyntax creation)
    {
        if (creation.ArgumentList is { } argumentList)
        {
            var typeWithSpacing = creation.Type.WithTrailingTrivia(argumentList.CloseParenToken.TrailingTrivia);
            return creation.WithType(typeWithSpacing).WithArgumentList(null);
        }

        var separator = creation.Type.GetTrailingTrivia();
        return creation
            .WithType(creation.Type.WithTrailingTrivia())
            .WithArgumentList(SyntaxFactory.ArgumentList().WithTrailingTrivia(separator));
    }
}
