// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a linked list's LINQ end scan with its own property (PSH1124): <c>list.First()</c>
/// becomes <c>list.First.Value</c> and <c>list.Last()</c> becomes <c>list.Last.Value</c>. The
/// property returns the <c>LinkedListNode&lt;T&gt;</c>, so <c>.Value</c> is what makes the
/// replacement the same expression type as the call it replaces. An explicit type argument on the
/// extension call is dropped, because the property does not take one.
/// </summary>
/// <remarks>
/// On an empty list the two shapes throw different exceptions: the extension throws
/// <c>InvalidOperationException</c>, while the property returns <see langword="null"/> and
/// <c>.Value</c> then throws <c>NullReferenceException</c>. Both still throw — the fix cannot turn
/// an empty list into a silently wrong answer — but code that catches the exception by type will
/// notice. The rule's docs page states this before the fix is offered.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1124UseLinkedListEndPropertyCodeFixProvider))]
[Shared]
public sealed class Psh1124UseLinkedListEndPropertyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseLinkedListEndProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Read the linked list's end node", nameof(Psh1124UseLinkedListEndPropertyCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces one reported call with the list's own node property.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported call.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)));

    /// <summary>Resolves the reported call and builds its node-property replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => TryGetEndInvocation(root, diagnostic) is { } invocation
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Returns the reported invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetEndInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return Psh1124UseLinkedListEndPropertyAnalyzer.IsEndExtensionShape(invocation) ? invocation : null;
            }
        }

        return null;
    }

    /// <summary>Drops the call and reads the node's value through the list's own property.</summary>
    /// <param name="invocation">The end-extension call; callers must have validated the shape.</param>
    /// <returns>The property read followed by the node's value.</returns>
    private static MemberAccessExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var propertyName = SyntaxFactory.IdentifierName(memberAccess.Name.Identifier).WithTriviaFrom(memberAccess.Name);
        var nodeRead = memberAccess.WithName(propertyName);

        return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                nodeRead,
                SyntaxFactory.IdentifierName(Psh1124UseLinkedListEndPropertyAnalyzer.ValueMemberName))
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }
}
