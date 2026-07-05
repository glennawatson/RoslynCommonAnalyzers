// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds the <c>static</c> modifier to a capture-free anonymous function (PSH1000),
/// moving the function's leading trivia onto the inserted keyword.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1000StaticAnonymousFunctionCodeFixProvider))]
[Shared]
public sealed class Psh1000StaticAnonymousFunctionCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.MakeAnonymousFunctionStatic.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Make the anonymous function static", nameof(Psh1000StaticAnonymousFunctionCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Adds the <c>static</c> modifier to the reported anonymous function.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="function">The anonymous function to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, AnonymousFunctionExpressionSyntax function)
        => document.WithSyntaxRoot(root.ReplaceNode(function, Rewrite(function)));

    /// <summary>Resolves the reported anonymous function and builds its static replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is { } function
            ? new NodeReplacement(function, Rewrite(function), RewriteCurrent)
            : null;

    /// <summary>Rewrites the current anonymous function during batch FixAll composition.</summary>
    /// <param name="current">The current anonymous function node.</param>
    /// <returns>The rewritten anonymous function.</returns>
    private static AnonymousFunctionExpressionSyntax RewriteCurrent(SyntaxNode current)
        => Rewrite((AnonymousFunctionExpressionSyntax)current);

    /// <summary>Inserts a leading <c>static</c> modifier, keeping the function's leading trivia on it.</summary>
    /// <param name="function">The anonymous function to rewrite.</param>
    /// <returns>The rewritten anonymous function.</returns>
    private static AnonymousFunctionExpressionSyntax Rewrite(AnonymousFunctionExpressionSyntax function)
    {
        var staticKeyword = SyntaxFactory.Token(SyntaxKind.StaticKeyword)
            .WithLeadingTrivia(function.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.Space);
        var stripped = function.WithLeadingTrivia(SyntaxFactory.TriviaList());
        return stripped.WithModifiers(stripped.Modifiers.Insert(0, staticKeyword));
    }
}
