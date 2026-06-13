// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites <c>throw ex;</c> as a bare <c>throw;</c> to keep the original stack trace (SST1430).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreserveStackTraceCodeFixProvider))]
[Shared]
public sealed class PreserveStackTraceCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.PreserveStackTraceOnRethrow.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ThrowStatementSyntax throwStatement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use 'throw;' to preserve the stack trace",
                    _ => Task.FromResult(Apply(context.Document, root, throwStatement)),
                    equivalenceKey: nameof(PreserveStackTraceCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Drops the thrown expression so the caught exception is re-thrown in place.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="throwStatement">The <c>throw ex;</c> statement.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ThrowStatementSyntax throwStatement)
    {
        var bareThrow = throwStatement
            .WithExpression(null)
            .WithThrowKeyword(throwStatement.ThrowKeyword.WithTrailingTrivia(SyntaxFactory.TriviaList()));

        return document.WithSyntaxRoot(root.ReplaceNode(throwStatement, bareThrow));
    }
}
