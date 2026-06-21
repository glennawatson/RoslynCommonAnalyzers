// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an <c>is</c> check followed by a cast local as a declaration pattern (SST2007).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DeclarationPatternCodeFixProvider))]
[Shared]
public sealed class DeclarationPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseDeclarationPatternOverIsCheckAndCast.Id);

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
            if (FindIf(root, diagnostic.Location.SourceSpan) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Declare the checked value in the pattern",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(DeclarationPatternCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!CreateReplacement(editor.OriginalRoot, diagnostic, out var ifStatement, out var replacement)
            || ifStatement is null
            || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(ifStatement, replacement);
    }

    /// <summary>Applies one declaration-pattern fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!CreateReplacement(root, diagnostic, out var ifStatement, out var replacement)
            || ifStatement is null
            || replacement is null)
        {
            return document;
        }

        var tracked = root.TrackNodes(ifStatement);
        if (tracked.GetCurrentNode(ifStatement) is not { } trackedIf)
        {
            return document;
        }

        var updated = tracked.ReplaceNode(trackedIf, replacement);
        return document.WithSyntaxRoot(updated);
    }

    /// <summary>Builds the updated if statement and local declaration to remove.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="ifStatement">The if statement to replace.</param>
    /// <param name="replacement">The replacement if statement.</param>
    /// <returns><see langword="true"/> when a replacement can be created.</returns>
    private static bool CreateReplacement(
        SyntaxNode root,
        Diagnostic diagnostic,
        out IfStatementSyntax? ifStatement,
        out IfStatementSyntax? replacement)
    {
        ifStatement = FindIf(root, diagnostic.Location.SourceSpan);
        replacement = null;
        if (ifStatement is null
            || PatternMatchingAnalyzer.Unwrap(ifStatement.Condition) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpression
            || isExpression.Right is not TypeSyntax type
            || ifStatement.Statement is not BlockSyntax { Statements.Count: > 0 } block
            || block.Statements[0] is not LocalDeclarationStatementSyntax local
            || local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: CastExpressionSyntax, Identifier: { } identifier })
        {
            return false;
        }

        var pattern = SyntaxFactory.DeclarationPattern(
            type.WithoutTrivia(),
            SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(identifier.ValueText)));
        var condition = SyntaxFactory.IsPatternExpression(
                isExpression.Left.WithoutTrivia(),
                pattern)
            .WithTriviaFrom(ifStatement.Condition);
        if (block.RemoveNode(local, SyntaxRemoveOptions.KeepNoTrivia) is not { } replacementBlock)
        {
            return false;
        }

        replacement = ifStatement
            .WithCondition(condition)
            .WithStatement(replacementBlock);
        return true;
    }

    /// <summary>Finds the containing if statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <returns>The containing if statement, or <see langword="null"/>.</returns>
    private static IfStatementSyntax? FindIf(SyntaxNode root, TextSpan span)
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is IfStatementSyntax ifStatement)
            {
                return ifStatement;
            }

            node = node.Parent;
        }

        return null;
    }
}
