// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Constant-folds a null-coalescing whose left operand is always null (SST2494): <c>a ?? b</c> becomes
/// <c>b</c>, the operand it always evaluated to. The right operand keeps the whole expression's trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2494ConstantNullCoalesceCodeFixProvider))]
[Shared]
public sealed class Sst2494ConstantNullCoalesceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArrays.Of(CorrectnessRules.ConstantNullCoalesce.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Replace with the right operand",
            nameof(Sst2494ConstantNullCoalesceCodeFixProvider),
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
        root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>()is { } coalesce
            && coalesce.IsKind(SyntaxKind.CoalesceExpression);

    /// <summary>Resolves the reported coalescing and replaces it with its right operand.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<BinaryExpressionSyntax>() is not { } coalesce
            || !coalesce.IsKind(SyntaxKind.CoalesceExpression))
        {
            return null;
        }

        var replacement = coalesce.Right.WithTriviaFrom(coalesce);
        return new NodeReplacement(coalesce, replacement);
    }
}
