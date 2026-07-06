// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites an SST1467 manual-enumerator loop into a foreach statement over the enumerated
/// expression. When the loop body starts with a declaration initialized from the only
/// <c>Current</c> read, that declaration becomes the iteration variable; otherwise the fix
/// introduces <c>item</c> and substitutes every <c>Current</c> read, declining when the name
/// <c>item</c> is already taken around the loop.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1467UseForeachOverManualEnumeratorCodeFixProvider))]
[Shared]
public sealed class Sst1467UseForeachOverManualEnumeratorCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The iteration variable name used when the body has no reusable declaration.</summary>
    private const string FallbackItemName = "item";

    /// <summary>Cached visitor that collects the <c>Current</c> member accesses for one enumerator name.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<IdentifierNameSyntax, CurrentAccessCollector> CurrentAccessVisitor = CollectCurrentAccess;

    /// <summary>Cached visitor that searches for an identifier token named <c>item</c>.</summary>
    private static readonly DescendantTraversalHelper.DescendantTokenVisitor<ItemTokenSearch> ItemTokenVisitor = VisitItemToken;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseForeachOverManualEnumerator.Id);

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
            if (!TryFindLoop(root, diagnostic, out var whileStatement, out var declaration)
                || whileStatement is null
                || declaration is null
                || !TryCreateForeach(declaration, whileStatement, out _))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Enumerate with 'foreach'",
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: nameof(Sst1467UseForeachOverManualEnumeratorCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryFindLoop(editor.OriginalRoot, diagnostic, out var whileStatement, out var declaration)
            || whileStatement is null
            || declaration is null
            || !TryCreateForeach(declaration, whileStatement, out _))
        {
            return;
        }

        editor.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);
        editor.ReplaceNode(whileStatement, (current, _) =>
            current is WhileStatementSyntax currentWhile
                && TryCreateForeach(declaration, currentWhile, out var replacement)
                && replacement is not null
                ? replacement
                : current);
    }

    /// <summary>Applies the foreach rewrite for one SST1467 diagnostic.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document, or the original document when the diagnostic no longer resolves.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryFindLoop(root, diagnostic, out var whileStatement, out var declaration)
            || whileStatement is null
            || declaration is null
            || !TryCreateForeach(declaration, whileStatement, out var replacement)
            || replacement is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(ReplaceLoop(root, declaration, whileStatement, replacement));
    }

    /// <summary>Resolves the while statement and enumerator declaration reported by an SST1467 diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="whileStatement">The reported while statement.</param>
    /// <param name="declaration">The enumerator declaration immediately before the loop.</param>
    /// <returns><see langword="true"/> when the reported pattern still resolves.</returns>
    private static bool TryFindLoop(
        SyntaxNode root,
        Diagnostic diagnostic,
        out WhileStatementSyntax? whileStatement,
        out LocalDeclarationStatementSyntax? declaration)
    {
        declaration = null;
        whileStatement = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<WhileStatementSyntax>();
        return whileStatement is not null
            && Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorName(whileStatement, out var name)
            && Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorDeclaration(whileStatement, name, out declaration, out _);
    }

    /// <summary>Builds the replacement foreach statement for one manual-enumerator loop.</summary>
    /// <param name="declaration">The enumerator declaration supplying the enumerated expression and leading trivia.</param>
    /// <param name="whileStatement">The while statement to rewrite.</param>
    /// <param name="replacement">The replacement foreach statement.</param>
    /// <returns><see langword="true"/> when a safe replacement was built.</returns>
    private static bool TryCreateForeach(
        LocalDeclarationStatementSyntax declaration,
        WhileStatementSyntax whileStatement,
        out ForEachStatementSyntax? replacement)
    {
        replacement = null;
        if (!Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorName(whileStatement, out var name)
            || !Sst1467UseForeachOverManualEnumeratorAnalyzer.HasForeachCompatibleBody(whileStatement, name)
            || GetSourceExpression(declaration) is not { } source)
        {
            return false;
        }

        var accesses = CollectCurrentAccesses(whileStatement.Statement, name);
        if (TryCreateNamedVariableForeach(whileStatement, declaration, source, accesses, out replacement))
        {
            return true;
        }

        if (ContainsItemIdentifier(declaration) || ContainsItemIdentifier(whileStatement))
        {
            return false;
        }

        replacement = CreateItemForeach(whileStatement, declaration, source, accesses);
        return true;
    }

    /// <summary>Extracts the enumerated expression from the enumerator declaration's <c>GetEnumerator()</c> initializer.</summary>
    /// <param name="declaration">The enumerator declaration.</param>
    /// <returns>The receiver of the <c>GetEnumerator()</c> call, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetSourceExpression(LocalDeclarationStatementSyntax declaration)
    {
        var variables = declaration.Declaration.Variables;
        if (variables.Count != 1
            || variables[0].Initializer is not { Value: InvocationExpressionSyntax invocation }
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        return memberAccess.Expression;
    }

    /// <summary>Builds a foreach that reuses the loop's own leading <c>var x = e.Current;</c> declaration as the iteration variable.</summary>
    /// <param name="whileStatement">The while statement to rewrite.</param>
    /// <param name="declaration">The enumerator declaration supplying the leading trivia.</param>
    /// <param name="source">The enumerated expression.</param>
    /// <param name="accesses">Every <c>Current</c> access in the loop body.</param>
    /// <param name="replacement">The replacement foreach statement.</param>
    /// <returns><see langword="true"/> when the body's first statement is the only <c>Current</c> read.</returns>
    private static bool TryCreateNamedVariableForeach(
        WhileStatementSyntax whileStatement,
        LocalDeclarationStatementSyntax declaration,
        ExpressionSyntax source,
        List<MemberAccessExpressionSyntax> accesses,
        out ForEachStatementSyntax? replacement)
    {
        replacement = null;
        if (whileStatement.Statement is not BlockSyntax block
            || block.Statements.Count == 0
            || block.Statements[0] is not LocalDeclarationStatementSyntax first
            || !first.UsingKeyword.IsKind(SyntaxKind.None)
            || first.Modifiers.Count != 0
            || first.Declaration.Variables.Count != 1)
        {
            return false;
        }

        var variable = first.Declaration.Variables[0];
        if (accesses.Count != 1 || variable.Initializer is not { } initializer || initializer.Value != accesses[0])
        {
            return false;
        }

        var body = block.WithStatements(block.Statements.RemoveAt(0));
        replacement = CreateForeach(first.Declaration.Type, variable.Identifier, source, body, declaration);
        return true;
    }

    /// <summary>Builds a foreach over the fallback <c>item</c> variable, substituting every <c>Current</c> read.</summary>
    /// <param name="whileStatement">The while statement to rewrite.</param>
    /// <param name="declaration">The enumerator declaration supplying the leading trivia.</param>
    /// <param name="source">The enumerated expression.</param>
    /// <param name="accesses">Every <c>Current</c> access in the loop body.</param>
    /// <returns>The replacement foreach statement.</returns>
    private static ForEachStatementSyntax CreateItemForeach(
        WhileStatementSyntax whileStatement,
        LocalDeclarationStatementSyntax declaration,
        ExpressionSyntax source,
        List<MemberAccessExpressionSyntax> accesses)
    {
        var body = whileStatement.Statement;
        if (accesses.Count > 0)
        {
            body = body.ReplaceNodes(accesses, (original, _) => SyntaxFactory.IdentifierName(FallbackItemName).WithTriviaFrom(original));
        }

        return CreateForeach(SyntaxFactory.IdentifierName("var"), SyntaxFactory.Identifier(FallbackItemName), source, body, declaration);
    }

    /// <summary>Assembles the annotated replacement foreach statement.</summary>
    /// <param name="type">The iteration variable type.</param>
    /// <param name="identifier">The iteration variable identifier.</param>
    /// <param name="source">The enumerated expression.</param>
    /// <param name="body">The rewritten loop body.</param>
    /// <param name="declaration">The enumerator declaration supplying the leading trivia.</param>
    /// <returns>The foreach statement.</returns>
    private static ForEachStatementSyntax CreateForeach(
        TypeSyntax type,
        SyntaxToken identifier,
        ExpressionSyntax source,
        StatementSyntax body,
        LocalDeclarationStatementSyntax declaration)
        => SyntaxFactory.ForEachStatement(
                type.WithoutTrivia(),
                identifier.WithLeadingTrivia(default(SyntaxTriviaList)).WithTrailingTrivia(default(SyntaxTriviaList)),
                source.WithoutTrivia(),
                body)
            .WithLeadingTrivia(declaration.GetLeadingTrivia())
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);

    /// <summary>Replaces the declaration and while statement with the foreach in their shared statement list.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="declaration">The enumerator declaration to remove.</param>
    /// <param name="whileStatement">The while statement to replace.</param>
    /// <param name="replacement">The replacement foreach statement.</param>
    /// <returns>The updated root.</returns>
    private static SyntaxNode ReplaceLoop(
        SyntaxNode root,
        LocalDeclarationStatementSyntax declaration,
        WhileStatementSyntax whileStatement,
        ForEachStatementSyntax replacement)
        => whileStatement.Parent switch
        {
            BlockSyntax block => root.ReplaceNode(block, block.WithStatements(BuildStatements(block.Statements, declaration, replacement))),
            SwitchSectionSyntax section => root.ReplaceNode(section, section.WithStatements(BuildStatements(section.Statements, declaration, replacement))),
            _ => root
        };

    /// <summary>Builds the statement list with the declaration removed and the loop replaced.</summary>
    /// <param name="statements">The original statement list.</param>
    /// <param name="declaration">The enumerator declaration; the while statement is its immediate successor.</param>
    /// <param name="replacement">The replacement foreach statement.</param>
    /// <returns>The updated statement list.</returns>
    private static SyntaxList<StatementSyntax> BuildStatements(
        SyntaxList<StatementSyntax> statements,
        LocalDeclarationStatementSyntax declaration,
        ForEachStatementSyntax replacement)
    {
        var index = statements.IndexOf(declaration);
        return statements.RemoveAt(index).RemoveAt(index).Insert(index, replacement);
    }

    /// <summary>Collects every <c>Current</c> member access on the enumerator inside the loop body.</summary>
    /// <param name="body">The loop body.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <returns>The collected member accesses in document order.</returns>
    private static List<MemberAccessExpressionSyntax> CollectCurrentAccesses(StatementSyntax body, string name)
    {
        var state = new CurrentAccessCollector(name, new List<MemberAccessExpressionSyntax>(4));
        DescendantTraversalHelper.VisitDescendants(body, ref state, CurrentAccessVisitor);
        return state.Accesses;
    }

    /// <summary>Records one enumerator member access.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The collector state.</param>
    /// <returns>Always <see langword="true"/> so the whole body is scanned.</returns>
    private static bool CollectCurrentAccess(IdentifierNameSyntax identifier, ref CurrentAccessCollector state)
    {
        if (!string.Equals(identifier.Identifier.ValueText, state.Name, StringComparison.Ordinal)
            || identifier.Parent is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Expression != identifier)
        {
            return true;
        }

        state.Accesses.Add(memberAccess);
        return true;
    }

    /// <summary>Returns whether a node contains an identifier token named <c>item</c>.</summary>
    /// <param name="node">The node to scan.</param>
    /// <returns><see langword="true"/> when the fallback name is already taken.</returns>
    private static bool ContainsItemIdentifier(SyntaxNode node)
    {
        var state = new ItemTokenSearch(Found: false);
        DescendantTraversalHelper.VisitDescendantTokens(node, ref state, ItemTokenVisitor);
        return state.Found;
    }

    /// <summary>Stops the token scan when an identifier token named <c>item</c> is found.</summary>
    /// <param name="token">The visited token.</param>
    /// <param name="state">The search state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once found.</returns>
    private static bool VisitItemToken(in SyntaxToken token, ref ItemTokenSearch state)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || !string.Equals(token.ValueText, FallbackItemName, StringComparison.Ordinal))
        {
            return true;
        }

        state = new ItemTokenSearch(Found: true);
        return false;
    }

    /// <summary>Collects <c>Current</c> member accesses for one enumerator name.</summary>
    /// <param name="Name">The enumerator local's name.</param>
    /// <param name="Accesses">The collected member accesses.</param>
    private readonly record struct CurrentAccessCollector(string Name, List<MemberAccessExpressionSyntax> Accesses);

    /// <summary>Tracks the search for an identifier token named <c>item</c>.</summary>
    /// <param name="Found">Whether the token was found.</param>
    private readonly record struct ItemTokenSearch(bool Found);
}
