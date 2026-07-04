// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Removes a field initializer that restates the type's default value (PSH1403).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1403RemoveRedundantDefaultInitializationCodeFixProvider))]
[Shared]
public sealed class Psh1403RemoveRedundantDefaultInitializationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.RemoveRedundantDefaultInitialization.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the redundant initializer", nameof(Psh1403RemoveRedundantDefaultInitializationCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported declarator and builds its initializer-free replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<VariableDeclaratorSyntax>() is { } declarator
            ? new NodeReplacement(declarator, Rewrite(declarator))
            : null;

    /// <summary>Drops the initializer while keeping the declarator's trailing trivia.</summary>
    /// <param name="declarator">The variable declarator to rewrite.</param>
    /// <returns>The rewritten declarator.</returns>
    private static VariableDeclaratorSyntax Rewrite(VariableDeclaratorSyntax declarator)
        => declarator.WithInitializer(null).WithTrailingTrivia(declarator.GetTrailingTrivia());
}
