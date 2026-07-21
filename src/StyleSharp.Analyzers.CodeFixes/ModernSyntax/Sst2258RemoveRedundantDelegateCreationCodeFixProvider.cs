// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Drops a redundant delegate wrapper around a method group (SST2258): <c>Changed += new EventHandler(OnChanged)</c>
/// becomes <c>Changed += OnChanged</c>. The method-group expression carries the creation's trivia, and the rewrite
/// is re-derived and re-bound so it only replaces the creation when the bare method group binds the same way.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2258RemoveRedundantDelegateCreationCodeFixProvider))]
[Shared]
public sealed class Sst2258RemoveRedundantDelegateCreationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.RemoveRedundantDelegateCreation.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the delegate wrapper", nameof(Sst2258RemoveRedundantDelegateCreationCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported delegate creation and rewrites it to the bare method group.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no safe rewrite exists.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        var creation = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
        if (creation is null
            || !Sst2258RemoveRedundantDelegateCreationAnalyzer.TryGetUnwrapped(creation, model, CancellationToken.None, out var methodGroup, out _))
        {
            return null;
        }

        return new NodeReplacement(creation, methodGroup.WithTriviaFrom(creation));
    }
}
