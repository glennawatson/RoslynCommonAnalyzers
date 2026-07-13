// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported array range indexer (PSH1019) into the slice extension the analyzer already
/// bound — <c>data[1..5]</c> becomes <c>data.AsSpan(1..5)</c>, or <c>data.AsMemory(1..5)</c> when the
/// use site wanted a memory. The range expression carries over untouched.
/// </summary>
/// <remarks>
/// Which of the two extensions to emit is not re-derived here: the analyzer decided it from the
/// converted type at the use site and recorded it on the diagnostic, so the fix cannot drift from what
/// was actually bound. The rewrite is bound again before it is offered, because a diagnostic can
/// outlive the edit that made it true.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1019UseAsSpanOverRangeIndexerCodeFixProvider))]
[Shared]
public sealed class Psh1019UseAsSpanOverRangeIndexerCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.UseAsSpanOverRangeIndexer.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Slice in place instead of copying",
            nameof(Psh1019UseAsSpanOverRangeIndexerCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported range indexer and builds its slice rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ElementAccessExpressionSyntax access
            || !Psh1019UseAsSpanOverRangeIndexerAnalyzer.IsRangeIndexerShape(access)
            || !diagnostic.Properties.TryGetValue(Psh1019UseAsSpanOverRangeIndexerAnalyzer.SliceMethodKey, out var sliceMethod)
            || sliceMethod is null)
        {
            return null;
        }

        var slice = Psh1019UseAsSpanOverRangeIndexerAnalyzer.BuildSlice(access, sliceMethod);
        return BindsToSlice(model, access.SpanStart, slice, sliceMethod)
            ? new NodeReplacement(access, slice.WithTriviaFrom(access))
            : null;
    }

    /// <summary>Confirms the rewritten slice still resolves to the extension before the fix is offered.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The original expression's position.</param>
    /// <param name="slice">The rewritten slice call.</param>
    /// <param name="sliceMethod">The slice method name the analyzer chose.</param>
    /// <returns><see langword="true"/> when the rewrite binds to <c>MemoryExtensions</c>.</returns>
    private static bool BindsToSlice(SemanticModel model, int position, InvocationExpressionSyntax slice, string sliceMethod)
        => model.GetSpeculativeSymbolInfo(position, slice, SpeculativeBindingOption.BindAsExpression).Symbol is IMethodSymbol resolved
            && resolved.Name == sliceMethod
            && resolved.ContainingType is
            {
                Name: Psh1019UseAsSpanOverRangeIndexerAnalyzer.MemoryExtensionsTypeName,
                ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
            };
}
