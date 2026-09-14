// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
public sealed class Sst1467UseForeachOverManualEnumeratorCodeFixProvider : CodeFixProvider
{
    /// <summary>The iteration variable name used when the body has no reusable declaration.</summary>
    private const string FallbackItemName = "item";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UseForeachOverManualEnumerator.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Enumerate with 'foreach'",
            nameof(Sst1467UseForeachOverManualEnumeratorCodeFixProvider),
            static (root, diagnostic) => TryFindFixableLoop(root, diagnostic, out _, out _),
            Apply);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryFindFixableLoop(editor.OriginalRoot, diagnostic, out var whileStatement, out var declaration))
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
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic) => !TryFindLoop(root, diagnostic, out var whileStatement, out var declaration)
            || whileStatement is null
            || declaration is null
            || !TryCreateForeach(declaration, whileStatement, out var replacement)
            || replacement is null
        ? document
        : document.WithSyntaxRoot(ReplaceLoop(root, declaration, whileStatement, replacement));

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

        // The declaration is deleted and the loop rewritten in its place, so a directive between the two
        // would lose the half that travels with the statement that goes.
        return whileStatement is not null
            && Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorName(whileStatement, out var name)
            && Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorDeclaration(whileStatement, name, out declaration, out _)
            && !DirectiveBoundaries.Separate(declaration!, whileStatement);
    }

    /// <summary>Resolves the reported loop and confirms a foreach can replace it, without building the foreach.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="whileStatement">The reported while statement.</param>
    /// <param name="declaration">The enumerator declaration immediately before the loop.</param>
    /// <returns><see langword="true"/> when the pattern still resolves and either iteration-variable choice is safe.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryFindFixableLoop(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out WhileStatementSyntax? whileStatement,
        [NotNullWhen(true)] out LocalDeclarationStatementSyntax? declaration) =>
        TryFindLoop(root, diagnostic, out whileStatement, out declaration)
            && whileStatement is not null
            && declaration is not null
            && CanCreateForeach(declaration, whileStatement);

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
        return variables.Count != 1
            || variables[0].Initializer is not { Value: InvocationExpressionSyntax invocation }
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            ? null
            : memberAccess.Expression;
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
        if (GetIterationVariable(whileStatement, accesses.Count == 1 ? accesses[0] : null) is not { } first)
        {
            return false;
        }

        var block = (BlockSyntax)whileStatement.Statement;
        var body = block.Update(block.AttributeLists, block.OpenBraceToken, block.Statements.RemoveAt(0), block.CloseBraceToken);
        replacement = CreateForeach(first.Declaration.Type, first.Declaration.Variables[0].Identifier, source, body, declaration);
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
            body = body.ReplaceNodes(accesses, static (original, _) => SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                original.GetLeadingTrivia(),
                FallbackItemName,
                original.GetTrailingTrivia())));
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ForEachStatementSyntax CreateForeach(
        TypeSyntax type,
        SyntaxToken identifier,
        ExpressionSyntax source,
        StatementSyntax body,
        LocalDeclarationStatementSyntax declaration) =>
        SyntaxFactory.ForEachStatement(
                attributeLists: default,
                awaitKeyword: default,
                SyntaxFactory.Token(declaration.GetLeadingTrivia(), SyntaxKind.ForEachKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                type.WithoutTrivia(),
                identifier.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.InKeyword),
                source.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.CloseParenToken),
                body)
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
        ForEachStatementSyntax replacement) =>
        whileStatement.Parent switch
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
        const int InitialCurrentAccessCapacity = 4;

        var state = new CurrentAccessCollector(name, new List<MemberAccessExpressionSyntax>(InitialCurrentAccessCapacity));
        _ = DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, CurrentAccessCollector>(body, ref state, CollectCurrentAccess);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsItemIdentifier(SyntaxNode node) => IdentifierReferences.ContainsIdentifierToken(node, FallbackItemName);

    /// <summary>Checks the iteration-variable choices without building a foreach or collecting accesses.</summary>
    /// <param name="declaration">The enumerator declaration.</param>
    /// <param name="whileStatement">The manual loop.</param>
    /// <returns>Whether either iteration-variable choice is safe.</returns>
    private static bool CanCreateForeach(LocalDeclarationStatementSyntax declaration, WhileStatementSyntax whileStatement)
    {
        if (!Sst1467UseForeachOverManualEnumeratorAnalyzer.TryGetEnumeratorName(whileStatement, out var name)
            || !Sst1467UseForeachOverManualEnumeratorAnalyzer.HasForeachCompatibleBody(whileStatement, name)
            || GetSourceExpression(declaration) is null)
        {
            return false;
        }

        var state = new CurrentAccessSearch(name, Access: null, Count: 0);
        _ = DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, CurrentAccessSearch>(whileStatement.Statement, ref state, FindCurrentAccess);
        return GetIterationVariable(whileStatement, state.Count == 1 ? state.Access : null) is not null
            || (!ContainsItemIdentifier(declaration) && !ContainsItemIdentifier(whileStatement));
    }

    /// <summary>Finds the body's reusable declaration when its initializer is the only enumerator access.</summary>
    /// <param name="whileStatement">The manual loop.</param>
    /// <param name="access">The sole enumerator access, or null when there is not exactly one.</param>
    /// <returns>The reusable declaration, or null when the fallback variable is required.</returns>
    private static LocalDeclarationStatementSyntax? GetIterationVariable(WhileStatementSyntax whileStatement, MemberAccessExpressionSyntax? access)
    {
        if (access is null
            || whileStatement.Statement is not BlockSyntax block
            || block.Statements.Count == 0
            || block.Statements[0] is not LocalDeclarationStatementSyntax first
            || !first.UsingKeyword.IsKind(SyntaxKind.None)
            || first.Modifiers.Count != 0
            || first.Declaration.Variables.Count != 1)
        {
            return null;
        }

        return first.Declaration.Variables[0].Initializer is { } initializer && initializer.Value == access
            ? first
            : null;
    }

    /// <summary>Stops once a second access rules out reusing the body's declaration.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The enumerator access search.</param>
    /// <returns>Whether another access could affect the result.</returns>
    private static bool FindCurrentAccess(IdentifierNameSyntax identifier, ref CurrentAccessSearch state)
    {
        if (!string.Equals(identifier.Identifier.ValueText, state.Name, StringComparison.Ordinal)
            || identifier.Parent is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Expression != identifier)
        {
            return true;
        }

        state = new(state.Name, memberAccess, state.Count + 1);
        return state.Count < 2;
    }

    /// <summary>Collects <c>Current</c> member accesses for one enumerator name.</summary>
    /// <param name="Name">The enumerator local's name.</param>
    /// <param name="Accesses">The collected member accesses.</param>
    private readonly record struct CurrentAccessCollector(string Name, List<MemberAccessExpressionSyntax> Accesses);

    /// <summary>Tracks up to two accesses without allocating a collection.</summary>
    /// <param name="Name">The enumerator name.</param>
    /// <param name="Access">The most recent access.</param>
    /// <param name="Count">The number of accesses seen, capped at two.</param>
    private readonly record struct CurrentAccessSearch(string Name, MemberAccessExpressionSyntax? Access, int Count);
}
