// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Transfers ownership of a returned disposable to the caller (SST2423) by dropping the <c>using</c>
/// from its declaration, so it is no longer disposed on the way out of the method. Offered only for a
/// <c>using</c> declaration; unwrapping a <c>using</c> statement block is left to the author.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2423DisposableReturnedFromUsingCodeFixProvider))]
[Shared]
public sealed class Sst2423DisposableReturnedFromUsingCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.DisposableReturnedFromUsing.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Transfer ownership to the caller", nameof(Sst2423DisposableReturnedFromUsingCodeFixProvider), CanRewrite, TryRewrite);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic) =>
        ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan)is IdentifierNameSyntax identifier
            && model.GetSymbolInfo(identifier).Symbol is ILocalSymbol local
            && local.DeclaringSyntaxReferences is [var reference]
            && reference.GetSyntax()is VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax statement }
            && !statement.UsingKeyword.IsKind(SyntaxKind.None)
            && statement.Declaration.Variables.Count == 1;

    /// <summary>Resolves the returned local's <c>using</c> declaration and builds its plain replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the declaration cannot be safely rewritten.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not IdentifierNameSyntax identifier
            || model.GetSymbolInfo(identifier).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences is not [var reference]
            || reference.GetSyntax() is not VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax statement }
            || statement.UsingKeyword.IsKind(SyntaxKind.None)
            || statement.Declaration.Variables.Count != 1)
        {
            return null;
        }

        var replacement = Rewrite(statement);
        return new NodeReplacement(statement, replacement, static current => Rewrite((LocalDeclarationStatementSyntax)current));
    }

    /// <summary>Rewrites a <c>using</c> (or <c>await using</c>) declaration as a plain declaration.</summary>
    /// <param name="statement">The using declaration.</param>
    /// <returns>The rewritten declaration.</returns>
    private static LocalDeclarationStatementSyntax Rewrite(LocalDeclarationStatementSyntax statement)
    {
        var leading = statement.AwaitKeyword.IsKind(SyntaxKind.None)
            ? statement.UsingKeyword.LeadingTrivia
            : statement.AwaitKeyword.LeadingTrivia;

        return statement.Update(
                statement.AttributeLists,
                awaitKeyword: default,
                usingKeyword: default,
                statement.Modifiers,
                statement.Declaration.WithLeadingTrivia(leading),
                statement.SemicolonToken)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }
}
