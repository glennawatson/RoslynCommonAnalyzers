// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Drops the redundant length argument from a reported slice (PSH1220), leaving the overload that
/// already means "to the end". The start argument carries over untouched.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1220RedundantLengthArgumentCodeFixProvider))]
[Shared]
public sealed class Psh1220RedundantLengthArgumentCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.RedundantLengthArgument.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Drop the length that reaches the end",
            nameof(Psh1220RedundantLengthArgumentCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported length argument and builds the shortened slice.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    /// <remarks>
    /// The diagnostic is reported on the length argument, so the slice it belongs to is found by
    /// walking up to the invocation. The shortened call is bound again before the fix is offered: the
    /// one-argument overload has to exist, on the same type, returning the same thing.
    /// </remarks>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                ?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || !Psh1220RedundantLengthArgumentAnalyzer.IsSliceShape(invocation)
            || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol slice
            || !Psh1220RedundantLengthArgumentAnalyzer.ShortenedSliceBinds(model, invocation, slice))
        {
            return null;
        }

        var shortened = Psh1220RedundantLengthArgumentAnalyzer.BuildShortenedSlice(invocation);
        return new NodeReplacement(invocation, shortened.WithTriviaFrom(invocation));
    }
}
