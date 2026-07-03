// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a redundant membership guard around a mutating call (PSH1105): the if
/// statement collapses to its body call for the <c>Remove</c> and bool-returning
/// <c>Add</c> pairings, and the guarded two-argument <c>Add</c> pairing is rewritten
/// to a single <c>TryAdd</c> call. The if statement's leading trivia is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1105AvoidDoubleLookupCodeFixProvider))]
[Shared]
public sealed class Psh1105AvoidDoubleLookupCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.AvoidDoubleLookup.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
                || !Psh1105AvoidDoubleLookupAnalyzer.TryGetShape(ifStatement, out var shape))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    shape.RequiresTryAdd ? "Use TryAdd" : "Remove the redundant lookup guard",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, ifStatement)),
                    equivalenceKey: nameof(Psh1105AvoidDoubleLookupCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
            || !Psh1105AvoidDoubleLookupAnalyzer.TryGetShape(ifStatement, out var shape))
        {
            return;
        }

        editor.ReplaceNode(ifStatement, CreateReplacement(ifStatement, shape));
    }

    /// <summary>Replaces the reported if statement with the unguarded mutating call.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="ifStatement">The reported if statement.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, IfStatementSyntax ifStatement)
        => !Psh1105AvoidDoubleLookupAnalyzer.TryGetShape(ifStatement, out var shape)
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(ifStatement, CreateReplacement(ifStatement, shape)));

    /// <summary>Builds the statement that replaces the guard, rewriting Add to TryAdd where required.</summary>
    /// <param name="ifStatement">The reported if statement.</param>
    /// <param name="shape">The validated shape.</param>
    /// <returns>The body call statement carrying the if statement's outer trivia.</returns>
    private static ExpressionStatementSyntax CreateReplacement(IfStatementSyntax ifStatement, in Psh1105AvoidDoubleLookupAnalyzer.DoubleLookupShape shape)
    {
        var statement = shape.Body;
        if (shape.RequiresTryAdd)
        {
            statement = statement.ReplaceNode(
                shape.MutationName,
                SyntaxFactory.IdentifierName(Psh1105AvoidDoubleLookupAnalyzer.TryAddMethodName).WithTriviaFrom(shape.MutationName));
        }

        return statement
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia());
    }
}
