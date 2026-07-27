// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes an empty <c>else</c> clause from its <c>if</c> statement (SST1180).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyElseClauseCodeFixProvider))]
[Shared]
public sealed class EmptyElseClauseCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoEmptyElseClause.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the empty 'else' clause", nameof(EmptyElseClauseCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported else clause and builds the if statement without it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<ElseClauseSyntax>() is not { Parent: IfStatementSyntax ifStatement })
        {
            return null;
        }

        var withoutElse = ifStatement
            .WithStatement(ifStatement.Statement.WithTrailingTrivia(SyntaxFactory.TriviaList()))
            .WithElse(null)
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());

        return new NodeReplacement(ifStatement, withoutElse);
    }
}
