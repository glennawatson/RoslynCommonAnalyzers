// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a sorted set's LINQ extreme scan with its own property (PSH1122): <c>set.Min()</c>
/// becomes <c>set.Min</c> and <c>set.Max()</c> becomes <c>set.Max</c>. An explicit type argument on
/// the extension call is dropped, because the property does not take one.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1122UseSortedSetExtremePropertyCodeFixProvider))]
[Shared]
public sealed class Psh1122UseSortedSetExtremePropertyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseSortedSetExtremeProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Read the sorted set's property", nameof(Psh1122UseSortedSetExtremePropertyCodeFixProvider), CanRewrite, TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetExtremeInvocation(root, diagnostic)is { } invocation;

    /// <summary>Resolves the reported call and builds its property-read replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetExtremeInvocation(root, diagnostic) is { } invocation
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Returns the reported invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? TryGetExtremeInvocation(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation)
            {
                return Psh1122UseSortedSetExtremePropertyAnalyzer.IsExtremeExtensionShape(invocation) ? invocation : null;
            }
        }

        return null;
    }

    /// <summary>Drops the call, leaving the member access that reads the property.</summary>
    /// <param name="invocation">The extreme-extension call; callers must have validated the shape.</param>
    /// <returns>The property read.</returns>
    private static MemberAccessExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var propertyName = SyntaxFactory.IdentifierName(memberAccess.Name.Identifier.WithTrailingTrivia(invocation.GetTrailingTrivia()));

        return memberAccess.Update(memberAccess.Expression, memberAccess.OperatorToken, propertyName)
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }
}
