// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an array creation between the explicit and implicit element-type forms (SST2270): an implicit
/// <c>new[] { ... }</c> gains its element type, and a convertible explicit <c>new T[] { ... }</c> drops it. The
/// direction follows the reported node's kind, and each rewrite is re-bound before it is offered, so a form
/// that would infer a different array type is never produced.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2270ArrayCreationTypeStyleCodeFixProvider))]
[Shared]
public sealed class Sst2270ArrayCreationTypeStyleCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.NormalizeArrayCreationTypeStyle.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Normalize the array element-type style", nameof(Sst2270ArrayCreationTypeStyleCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported array creation and flips its element-type form.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no bind-safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        return ToExplicit(model, node) ?? ToImplicit(model, node);
    }

    /// <summary>Builds the explicit rewrite when the reported node is an implicit array creation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="node">The node resolved from the diagnostic span.</param>
    /// <returns>The rewrite, or <see langword="null"/> when it does not apply or is not bind-safe.</returns>
    private static NodeReplacement? ToExplicit(SemanticModel model, SyntaxNode node)
    {
        if (node.FirstAncestorOrSelf<ImplicitArrayCreationExpressionSyntax>() is not { } creation)
        {
            return null;
        }

        return Sst2270ArrayCreationTypeStyleAnalyzer.TryConvertToExplicit(model, creation) is { } form
            ? new NodeReplacement(creation, form)
            : null;
    }

    /// <summary>Builds the implicit rewrite when the reported node is a convertible explicit array creation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="node">The node resolved from the diagnostic span.</param>
    /// <returns>The rewrite, or <see langword="null"/> when it does not apply or is not bind-safe.</returns>
    private static NodeReplacement? ToImplicit(SemanticModel model, SyntaxNode node)
    {
        if (node.FirstAncestorOrSelf<ArrayCreationExpressionSyntax>() is not { } creation
            || !Sst2270ArrayCreationTypeStyleAnalyzer.IsConvertibleExplicit(creation))
        {
            return null;
        }

        return Sst2270ArrayCreationTypeStyleAnalyzer.TryConvertToImplicit(model, creation) is { } form
            ? new NodeReplacement(creation, form)
            : null;
    }
}
