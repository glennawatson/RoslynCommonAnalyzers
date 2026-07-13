// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a guard that tests a value against what it assigns with the bare assignment (SST1492). The
/// guarded assignment keeps its own text; only the <c>if</c> around it — and the empty branch of the
/// inverted shape — is dropped.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1492SelfAssignmentGuardCodeFixProvider))]
[Shared]
public sealed class Sst1492SelfAssignmentGuardCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.SelfAssignmentGuard.Id);

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
            if (TryGetGuard(root, diagnostic) is not { } ifStatement)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the guard and keep the assignment",
                    _ => Task.FromResult(Apply(context.Document, root, ifStatement)),
                    equivalenceKey: nameof(Sst1492SelfAssignmentGuardCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (TryGetGuard(editor.OriginalRoot, diagnostic) is not { } ifStatement
            || Sst1492SelfAssignmentGuardAnalyzer.TryGetGuardedAssignment(ifStatement) is not { } assignment)
        {
            return;
        }

        editor.ReplaceNode(ifStatement, Unwrap(ifStatement, assignment));
    }

    /// <summary>Applies the fix for one guarded self-assignment.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="ifStatement">The guard to unwrap.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, IfStatementSyntax ifStatement)
        => Sst1492SelfAssignmentGuardAnalyzer.TryGetGuardedAssignment(ifStatement) is { } assignment
            ? document.WithSyntaxRoot(root.ReplaceNode(ifStatement, Unwrap(ifStatement, assignment)))
            : document;

    /// <summary>Resolves the diagnostic's span to the guard it reported.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported guard, or <see langword="null"/> when the shape no longer matches.</returns>
    private static IfStatementSyntax? TryGetGuard(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan)?.Parent as IfStatementSyntax;

    /// <summary>Builds the assignment statement that takes the guard's place.</summary>
    /// <param name="ifStatement">The guard being removed.</param>
    /// <param name="assignment">The assignment the guard wrapped.</param>
    /// <returns>The replacement statement, carrying the guard's own trivia.</returns>
    private static ExpressionStatementSyntax Unwrap(IfStatementSyntax ifStatement, ExpressionStatementSyntax assignment)
        => assignment
            .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
            .WithTrailingTrivia(ifStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
}
