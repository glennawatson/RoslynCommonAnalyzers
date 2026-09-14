// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty anonymous-method parameter list (SST1410) or attribute argument list (SST1411).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantParenthesesCodeFixProvider))]
[Shared]
public sealed class RedundantParenthesesCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        MaintainabilityRules.RemoveDelegateParentheses.Id,
        MaintainabilityRules.RemoveAttributeParentheses.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove empty parentheses",
            nameof(RedundantParenthesesCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves a diagnostic to the node replacement that drops its empty parentheses.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The original and replacement nodes, or <see langword="null"/> when nothing can be removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        Rewrite(root.FindNode(diagnostic.Location.SourceSpan));

    /// <summary>Returns whether the reported node still carries empty parentheses, without building the replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns><see langword="true"/> when an anonymous method's parameter list or an attribute's argument list can be removed.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        return node.FirstAncestorOrSelf<AnonymousMethodExpressionSyntax>()?.ParameterList is not null
            || node.FirstAncestorOrSelf<AttributeSyntax>()?.ArgumentList is not null;
    }

    /// <summary>Computes the node replacement that drops the empty parentheses, or <see langword="null"/>.</summary>
    /// <param name="node">The node at the diagnostic location.</param>
    /// <returns>The original and replacement nodes, or <see langword="null"/>.</returns>
    private static NodeReplacement? Rewrite(SyntaxNode node)
    {
        var anonymous = node.FirstAncestorOrSelf<AnonymousMethodExpressionSyntax>();
        if (anonymous?.ParameterList is not null)
        {
            var updated = anonymous.Update(
                anonymous.Modifiers,
                anonymous.DelegateKeyword.WithTrailingTrivia(SyntaxFactory.Space),
                parameterList: null,
                anonymous.Block,
                anonymous.ExpressionBody);
            return new NodeReplacement(anonymous, updated);
        }

        var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
        return attribute?.ArgumentList is null ? null : new NodeReplacement(attribute, attribute.WithArgumentList(null));
    }
}
