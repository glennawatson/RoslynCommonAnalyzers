// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a <c>ToString</c> call from a receiver that is already a string (SST2286):
/// <c>text.ToString()</c> becomes <c>text</c>, the value the call returned.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2286RedundantStringToStringCodeFixProvider))]
[Shared]
public sealed class Sst2286RedundantStringToStringCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(ModernSyntaxRules.RedundantStringToString.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the 'ToString' call",
            nameof(Sst2286RedundantStringToStringCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>()is { Expression: MemberAccessExpressionSyntax { Expression: { } receiver } } invocation;

    /// <summary>Resolves the reported call and replaces it with its receiver.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) => root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not
            { Expression: MemberAccessExpressionSyntax { Expression: { } receiver } } invocation
        ? null
        : new NodeReplacement(invocation, receiver.WithTriviaFrom(invocation));
}
