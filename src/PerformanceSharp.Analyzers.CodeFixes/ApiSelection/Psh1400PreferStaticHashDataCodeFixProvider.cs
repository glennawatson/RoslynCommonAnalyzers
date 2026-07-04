// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a chained <c>X.Create().ComputeHash(data)</c> invocation with the static
/// <c>X.HashData(data)</c> call (PSH1400), reusing the original algorithm type expression.
/// The using-scoped local shape reports on a variable declarator and is not fixed here.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1400PreferStaticHashDataCodeFixProvider))]
[Shared]
public sealed class Psh1400PreferStaticHashDataCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The name of the static one-shot hashing method the fix calls.</summary>
    private const string HashDataMethodName = "HashData";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.PreferStaticHashData.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use the static HashData method", nameof(Psh1400PreferStaticHashDataCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported chained invocation with its static HashData form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The chained invocation to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Resolves the reported chained invocation and builds its static HashData replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => TryGetChainedInvocation(root, diagnostic) is { } invocation
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Returns the reported chained invocation, or null for the fix-less using-scoped local shape.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The chained invocation when the diagnostic location covers one.</returns>
    private static InvocationExpressionSyntax? TryGetChainedInvocation(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan) is InvocationExpressionSyntax invocation
            && Psh1400PreferStaticHashDataAnalyzer.IsChainedComputeHashShape(invocation, out _)
            ? invocation
            : null;

    /// <summary>Rewrites <c>X.Create().ComputeHash(data)</c> to <c>X.HashData(data)</c>, reusing the original type expression.</summary>
    /// <param name="invocation">The chained invocation; callers must have validated the shape.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var computeAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var createInvocation = (InvocationExpressionSyntax)computeAccess.Expression;
        var createAccess = (MemberAccessExpressionSyntax)createInvocation.Expression;

        var hashDataAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            createAccess.Expression,
            SyntaxFactory.IdentifierName(HashDataMethodName));

        return SyntaxFactory.InvocationExpression(hashDataAccess, invocation.ArgumentList).WithTriviaFrom(invocation);
    }
}
