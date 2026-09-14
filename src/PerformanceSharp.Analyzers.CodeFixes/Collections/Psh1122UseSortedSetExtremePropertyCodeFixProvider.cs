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
public sealed class Psh1122UseSortedSetExtremePropertyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryGetExtremeInvocation, static (current, _) => Rewrite((InvocationExpressionSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseSortedSetExtremeProperty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(context, "Read the sorted set's property", nameof(Psh1122UseSortedSetExtremePropertyCodeFixProvider), TryGetExtremeInvocation, Rewrite);

    /// <summary>Returns the reported invocation when the diagnostic location still covers one.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static InvocationExpressionSyntax? TryGetExtremeInvocation(SyntaxNode root, Diagnostic diagnostic) =>
        EnclosingInvocation.Find(root, diagnostic) is { } invocation && Psh1122UseSortedSetExtremePropertyAnalyzer.IsExtremeExtensionShape(invocation)
            ? invocation
            : null;

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
