// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies name and member-access simplifications (SST1116/SST1117).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NameSimplificationCodeFixProvider))]
[Shared]
public sealed class NameSimplificationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ReadabilityRules.SimplifyName.Id,
        ReadabilityRules.SimplifyMemberAccess.Id);

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
            var replacement = CreateReplacement(root, diagnostic, out var oldNode, out _);
            if (oldNode is null || replacement is null)
            {
                continue;
            }

            var title = CreateTitle(diagnostic, oldNode);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(editor.OriginalRoot, diagnostic, out var oldNode, out _);
        if (oldNode is null || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
    }

    /// <summary>Applies one simplification fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(root, diagnostic, out var oldNode, out _);
        return oldNode is null || replacement is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(oldNode, replacement));
    }

    /// <summary>Creates the simplified replacement node.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The node being replaced.</param>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? CreateReplacement(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode, out string diagnosticId)
    {
        diagnosticId = diagnostic.Id;
        oldNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        return diagnostic.Id switch
        {
            "SST1116" => CreateNameReplacement(oldNode),
            "SST1117" => CreateMemberAccessReplacement(oldNode),
            _ => null
        };
    }

    /// <summary>Creates the simplified name replacement.</summary>
    /// <param name="node">The reported name node.</param>
    /// <returns>The shortened name, or <see langword="null"/>.</returns>
    private static SimpleNameSyntax? CreateNameReplacement(SyntaxNode? node)
        => node switch
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
    private static ExpressionSyntax? CreateMemberAccessReplacement(SyntaxNode? node)
        => node switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.WithTriviaFrom(memberAccess),
            IdentifierNameSyntax identifier => SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    identifier.WithoutTrivia())
                .WithTriviaFrom(identifier),
            _ => null
        };
}
