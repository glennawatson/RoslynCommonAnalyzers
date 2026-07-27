// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites <c>throw ex;</c> as a bare <c>throw;</c> to keep the original stack trace (SST1430).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreserveStackTraceCodeFixProvider))]
[Shared]
public sealed class PreserveStackTraceCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.PreserveStackTraceOnRethrow.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use 'throw;' to preserve the stack trace", nameof(PreserveStackTraceCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported node and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not ThrowStatementSyntax throwStatement)
        {
            return null;
        }

        return new NodeReplacement(throwStatement, BuildBareThrow(throwStatement));
    }

    /// <summary>Builds the bare <c>throw;</c> statement that re-throws the caught exception in place.</summary>
    /// <param name="throwStatement">The <c>throw ex;</c> statement.</param>
    /// <returns>The rewritten bare throw statement.</returns>
    private static ThrowStatementSyntax BuildBareThrow(ThrowStatementSyntax throwStatement)
        => throwStatement
            .WithExpression(null)
            .WithThrowKeyword(throwStatement.ThrowKeyword.WithTrailingTrivia(SyntaxFactory.TriviaList()));
}
