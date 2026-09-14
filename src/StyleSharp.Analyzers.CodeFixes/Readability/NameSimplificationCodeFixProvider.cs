// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Applies name and member-access simplifications (SST1116/SST1117).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NameSimplificationCodeFixProvider))]
[Shared]
public sealed class NameSimplificationCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ReadabilityRules.SimplifyName.Id,
        ReadabilityRules.SimplifyMemberAccess.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static diagnostic => diagnostic.Id,
            TryRewrite);

    /// <summary>Resolves the reported name and builds its simplified replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var oldNode = FindReported(root, diagnostic);
        return CreateReplacement(oldNode, diagnostic.Id) is { } replacement
            ? new NodeReplacement(oldNode, replacement)
            : null;
    }

    /// <summary>Creates the simplified replacement for the reported node.</summary>
    /// <param name="oldNode">The reported node.</param>
    /// <param name="diagnosticId">The diagnostic id naming the simplification.</param>
    /// <returns>The replacement node, or <see langword="null"/> when the node has no simplified form.</returns>
    private static ExpressionSyntax? CreateReplacement(SyntaxNode oldNode, string diagnosticId) =>
        diagnosticId switch
        {
            "SST1116" => CreateNameReplacement(oldNode),
            "SST1117" => CreateMemberAccessReplacement(oldNode),
            _ => null
        };

    /// <summary>Finds the node a diagnostic reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported node.</returns>
    /// <remarks>
    /// A name written in a documentation reference lives in trivia, so the search has to descend into it.
    /// Without that the search stops at the member the comment is attached to, which nothing here shortens.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode FindReported(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true);

    /// <summary>Words the action for the reported name, when its shape can still be rewritten for the diagnostic's rule.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the reported node cannot be rewritten.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic)
    {
        var oldNode = FindReported(root, diagnostic);
        var canRewrite = diagnostic.Id switch
        {
            "SST1116" => oldNode is QualifiedNameSyntax or AliasQualifiedNameSyntax,
            "SST1117" => oldNode is MemberAccessExpressionSyntax or IdentifierNameSyntax,
            _ => false,
        };
        return canRewrite ? CreateTitle(diagnostic, oldNode) : null;
    }

    /// <summary>Creates the simplified name replacement.</summary>
    /// <param name="node">The reported name node.</param>
    /// <returns>The shortened name, or <see langword="null"/>.</returns>
    private static SimpleNameSyntax? CreateNameReplacement(SyntaxNode? node) =>
        node switch
        {
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.WithTriviaFrom(qualifiedName),
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.WithTriviaFrom(aliasQualifiedName),
            _ => null
        };

    /// <summary>Creates the code action title for the reported syntax shape.</summary>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <param name="oldNode">The node being replaced.</param>
    /// <returns>The code action title.</returns>
    private static string CreateTitle(Diagnostic diagnostic, SyntaxNode oldNode)
    {
        if (diagnostic.Id == ReadabilityRules.SimplifyName.Id)
        {
            return "Shorten equivalent name";
        }

        return oldNode is IdentifierNameSyntax
            ? "Add this qualification"
            : "Remove this qualification";
    }

    /// <summary>Creates the unqualified member-access replacement.</summary>
    /// <param name="node">The reported member-access node.</param>
    /// <returns>The configured member-access replacement, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? CreateMemberAccessReplacement(SyntaxNode? node) =>
        node switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.WithTriviaFrom(memberAccess),
            IdentifierNameSyntax identifier => SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(SyntaxFactory.Token(
                        identifier.GetLeadingTrivia(),
                        SyntaxKind.ThisKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker))),
                    SyntaxFactory.Token(SyntaxKind.DotToken),
                    identifier.WithoutLeadingTrivia()),
            _ => null
        };
}
