// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
public sealed class Psh1124UseLinkedListEndPropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryGetEndInvocation, static (current, _) => Rewrite((InvocationExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseLinkedListEndProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(context, "Read the linked list's end node", nameof(Psh1124UseLinkedListEndPropertyCodeFixProvider), TryGetEndInvocation, Rewrite);

    /// <summary>Drops the call and reads the node's value through the list's own property.</summary>
    /// <param name="invocation">The end-extension call; callers must have validated the shape.</param>
    /// <returns>The property read followed by the node's value.</returns>
    internal static MemberAccessExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var propertyName = SyntaxFactory.IdentifierName(memberAccess.Name.Identifier.WithTrailingTrivia(memberAccess.Name.GetTrailingTrivia()));
        var nodeRead = memberAccess.WithName(propertyName);

        return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                nodeRead,
                SyntaxFactory.Token(SyntaxKind.DotToken),
                SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                    SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker),
                    Psh1124UseLinkedListEndPropertyAnalyzer.ValueMemberName,
                    invocation.GetTrailingTrivia())))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Returns the reported invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static InvocationExpressionSyntax? TryGetEndInvocation(SyntaxNode root, Diagnostic diagnostic) =>
        EnclosingInvocation.Find(root, diagnostic) is { } invocation && Psh1124UseLinkedListEndPropertyAnalyzer.IsEndExtensionShape(invocation)
            ? invocation
            : null;
}
