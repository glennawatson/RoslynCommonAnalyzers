// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces an argument-free <c>new Random()</c> with <c>Random.Shared</c> (PSH1412). The type is
/// written back exactly as the author wrote it — <c>new System.Random()</c> becomes
/// <c>System.Random.Shared</c> — and a target-typed <c>new()</c>, which named nothing, becomes
/// <c>Random.Shared</c>, which the analyzer has already confirmed resolves at that position.
/// </summary>
/// <remarks>
/// The replacement is an expression, so it fits wherever the allocation did: a field initializer, a
/// local, an argument, a returned value. A variable that is later reassigned is no obstacle either —
/// <c>Random.Shared</c> is a <c>Random</c> like any other, and assigning something else over it
/// afterwards compiles exactly as before.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1412UseSharedRandomCodeFixProvider))]
[Shared]
public sealed class Psh1412UseSharedRandomCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseSharedRandom.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use Random.Shared",
            nameof(Psh1412UseSharedRandomCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported allocation with the shared instance.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="creation">The reported allocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BaseObjectCreationExpressionSyntax creation)
        => Psh1412UseSharedRandomAnalyzer.IsParameterlessCreationShape(creation)
            ? document.WithSyntaxRoot(root.ReplaceNode(creation, Rewrite(creation)))
            : document;

    /// <summary>Resolves the reported allocation and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BaseObjectCreationExpressionSyntax creation
            && Psh1412UseSharedRandomAnalyzer.IsParameterlessCreationShape(creation)
            ? new NodeReplacement(creation, Rewrite(creation))
            : null;

    /// <summary>Builds the <c>Random.Shared</c> access, reusing the type name the author wrote.</summary>
    /// <param name="creation">The reported allocation.</param>
    /// <returns>The replacement expression.</returns>
    private static MemberAccessExpressionSyntax Rewrite(BaseObjectCreationExpressionSyntax creation)
    {
        var type = creation is ObjectCreationExpressionSyntax { Type: NameSyntax name }
            ? TypeNameExpression.From(name.WithoutTrivia())
            : SyntaxFactory.IdentifierName(Psh1412UseSharedRandomAnalyzer.RandomTypeName);

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            type,
            SyntaxFactory.IdentifierName(Psh1412UseSharedRandomAnalyzer.SharedPropertyName))
            .WithTriviaFrom(creation);
    }
}
