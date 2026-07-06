// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes the materialization call reported by PSH1120: <c>foreach (var x in source.ToList())</c>
/// becomes <c>foreach (var x in source)</c>. The receiver keeps its own trivia, inherits the removed
/// call's trailing trivia, and carries the formatter annotation so surrounding whitespace settles.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1120DoNotMaterializeToEnumerateCodeFixProvider))]
[Shared]
public sealed class Psh1120DoNotMaterializeToEnumerateCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.DoNotMaterializeToEnumerate.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Enumerate the source directly", nameof(Psh1120DoNotMaterializeToEnumerateCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Removes the reported materialization call, leaving its receiver as the loop source.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The ToList/ToArray invocation to remove.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Resolves the reported materialization call and builds its receiver-only replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => TryGetMaterializeInvocation(root, diagnostic) is { } invocation
            ? new NodeReplacement(invocation, Rewrite(invocation), RewriteCurrent)
            : null;

    /// <summary>Returns the reported materialization invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetMaterializeInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return Psh1120DoNotMaterializeToEnumerateAnalyzer.IsMaterializeInvocationShape(invocation) ? invocation : null;
            }
        }

        return null;
    }

    /// <summary>Rewrites the current materialization invocation during batch FixAll composition.</summary>
    /// <param name="current">The current invocation node.</param>
    /// <returns>The rewritten expression.</returns>
    private static ExpressionSyntax RewriteCurrent(SyntaxNode current)
        => Rewrite((InvocationExpressionSyntax)current);

    /// <summary>Builds the receiver-only replacement for a materialization invocation.</summary>
    /// <param name="invocation">The invocation to rewrite; callers must have validated the shape.</param>
    /// <returns>The receiver expression carrying the invocation's trailing trivia.</returns>
    private static ExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
        => ((MemberAccessExpressionSyntax)invocation.Expression).Expression
            .WithTrailingTrivia(invocation.GetTrailingTrivia())
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
}
