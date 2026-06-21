// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies mechanical fixes for modern readability rules (SST2212-SST2217).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxReadabilityCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxReadabilityCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The number of following statements rewritten by tuple deconstruction and swap fixes.</summary>
    private const int TwoFollowingStatements = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseUtf8StringLiteral.Id,
        ModernSyntaxRules.RemoveUnnecessaryDiscard.Id,
        ModernSyntaxRules.UseDeconstruction.Id,
        ModernSyntaxRules.UseTupleSwap.Id,
        ModernSyntaxRules.UseInferredTupleElementName.Id,
        ModernSyntaxRules.UseHashCodeCombine.Id);

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
            RegisterCodeFix(context, root, diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(editor.OriginalRoot, diagnostic, out var oldNode, out var removeFirst, out var removeSecond);
        if (oldNode is null || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
        RemoveNode(editor, removeFirst);
        RemoveNode(editor, removeSecond);
    }

    /// <summary>Applies one modern readability fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(root, diagnostic, out var oldNode, out var removeFirst, out var removeSecond);
        if (oldNode is null || replacement is null)
        {
            return document;
        }

        var updated = ReplaceAndRemove(root, oldNode, replacement, removeFirst, removeSecond);
        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Registers one code fix when the diagnostic still matches the current syntax root.</summary>
    /// <param name="context">The code-fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    private static void RegisterCodeFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        var title = GetTitle(diagnostic.Id);
        if (title is null || CreateEdit(root, diagnostic, out _, out _, out _) is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                equivalenceKey: diagnostic.Id),
            diagnostic);
    }

    /// <summary>Gets the user-facing title for one diagnostic id.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The code action title, or <see langword="null"/>.</returns>
    private static string? GetTitle(string diagnosticId)
        => diagnosticId switch
        {
            "SST2212" => "Use UTF-8 literal bytes",
            "SST2213" => "Remove discard designation",
            "SST2214" => "Deconstruct tuple directly",
            "SST2215" => "Swap with tuple assignment",
            "SST2216" => "Let the tuple name be inferred",
            "SST2217" => "Use System.HashCode.Combine",
            _ => null
        };

    /// <summary>Removes a node from an editor when one was supplied.</summary>
    /// <param name="editor">The document editor.</param>
    /// <param name="node">The node to remove.</param>
    private static void RemoveNode(DocumentEditor editor, SyntaxNode? node)
    {
        if (node is null)
        {
            return;
        }

        editor.RemoveNode(node);
    }

    /// <summary>Creates the syntax edit for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <param name="removeFirst">The first node to remove after replacement.</param>
    /// <param name="removeSecond">The second node to remove after replacement.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static SyntaxNode? CreateEdit(
        SyntaxNode root,
        Diagnostic diagnostic,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeFirst,
        out SyntaxNode? removeSecond)
    {
        removeFirst = null;
        removeSecond = null;
        oldNode = null;

        return diagnostic.Id switch
        {
            "SST2212" => CreateUtf8Fix(root, diagnostic, out oldNode),
            "SST2213" => CreateDiscardFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2214" => CreateDeconstructionFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeFirst, out removeSecond),
            "SST2215" => CreateTupleSwapFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeFirst, out removeSecond),
            "SST2216" => CreateTupleNameFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2217" => CreateHashCodeCombineFix(root, diagnostic.Location.SourceSpan, out oldNode),
            _ => null
        };
    }

    /// <summary>Creates a UTF-8 literal replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The expression to replace.</param>
    /// <returns>The UTF-8 literal expression.</returns>
    private static ExpressionSyntax? CreateUtf8Fix(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        oldNode = FindAncestor<ExpressionSyntax>(root, diagnostic.Location.SourceSpan);
        if (oldNode is not ExpressionSyntax expression
            || !diagnostic.Properties.TryGetValue(ModernSyntaxReadabilityAnalysis.Utf8TargetKey, out var target)
            || target is null
            || !ModernSyntaxReadabilityAnalysis.TryCreateUtf8Replacement(expression, target, out var replacement))
        {
            oldNode = null;
            return null;
        }

        return replacement;
    }

    /// <summary>Creates a declaration-pattern replacement without an explicit discard.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The pattern to replace.</param>
    /// <returns>The type pattern replacement.</returns>
    private static BinaryExpressionSyntax? CreateDiscardFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        var pattern = FindAncestor<DeclarationPatternSyntax>(root, span);
        if (pattern?.Parent is not IsPatternExpressionSyntax isPattern)
        {
            oldNode = null;
            return null;
        }

        oldNode = isPattern;
        return SyntaxFactory.BinaryExpression(
                SyntaxKind.IsExpression,
                isPattern.Expression.WithoutTrivia(),
                pattern.Type.WithTrailingTrivia())
            .WithTriviaFrom(isPattern);
    }

    /// <summary>Creates a tuple deconstruction declaration and removes copied element locals.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The tuple local to replace.</param>
    /// <param name="removeFirst">The first copied element declaration to remove.</param>
    /// <param name="removeSecond">The second copied element declaration to remove.</param>
    /// <returns>The deconstruction statement.</returns>
    private static StatementSyntax? CreateDeconstructionFix(
        SyntaxNode root,
        TextSpan span,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeFirst,
        out SyntaxNode? removeSecond)
    {
        oldNode = FindAncestor<LocalDeclarationStatementSyntax>(root, span);
        removeFirst = null;
        removeSecond = null;
        if (oldNode is not LocalDeclarationStatementSyntax local
            || !TryGetSingleInitializer(local, out var initializer)
            || local.Parent is not BlockSyntax block
            || !TryGetFollowingElementLocals(block, local, out var first, out var second, out var firstName, out var secondName))
        {
            oldNode = null;
            return null;
        }

        removeFirst = first;
        removeSecond = second;
        return SyntaxFactory.ParseStatement($"var ({firstName}, {secondName}) = {initializer.WithoutTrivia()};")
            .WithTriviaFrom(local);
    }

    /// <summary>Creates a tuple assignment for a three-statement local swap.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The temporary declaration to replace.</param>
    /// <param name="removeFirst">The first assignment to remove.</param>
    /// <param name="removeSecond">The second assignment to remove.</param>
    /// <returns>The tuple assignment statement.</returns>
    private static ExpressionStatementSyntax? CreateTupleSwapFix(
        SyntaxNode root,
        TextSpan span,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeFirst,
        out SyntaxNode? removeSecond)
    {
        oldNode = FindAncestor<LocalDeclarationStatementSyntax>(root, span);
        removeFirst = null;
        removeSecond = null;
        if (oldNode is not LocalDeclarationStatementSyntax local
            || !TryGetSingleIdentifierInitializer(local, out var left)
            || local.Parent is not BlockSyntax block
            || !TryGetFollowingSwap(block, local, out var first, out var second, out var right))
        {
            oldNode = null;
            return null;
        }

        removeFirst = first;
        removeSecond = second;
        var statement = SyntaxFactory.ParseStatement($"({left.Identifier.ValueText}, {right}) = ({right}, {left.Identifier.ValueText});");
        return statement.WithTriviaFrom(local) as ExpressionStatementSyntax;
    }

    /// <summary>Gets the initializer expression from a single-variable local declaration.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="initializer">The initializer expression.</param>
    /// <returns><see langword="true"/> when the declaration has one initialized variable.</returns>
    private static bool TryGetSingleInitializer(LocalDeclarationStatementSyntax local, out ExpressionSyntax initializer)
    {
        initializer = null!;
        if (local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0].Initializer?.Value is not { } value)
        {
            return false;
        }

        initializer = value;
        return true;
    }

    /// <summary>Gets an identifier initializer from a single-variable local declaration.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="initializer">The identifier initializer.</param>
    /// <returns><see langword="true"/> when the declaration has one identifier initializer.</returns>
    private static bool TryGetSingleIdentifierInitializer(LocalDeclarationStatementSyntax local, out IdentifierNameSyntax initializer)
    {
        initializer = null!;
        if (!TryGetSingleInitializer(local, out var value) || value is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        initializer = identifier;
        return true;
    }

    /// <summary>Creates a tuple argument replacement without the repeated name.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The tuple argument to replace.</param>
    /// <returns>The tuple argument without a name.</returns>
    private static ArgumentSyntax? CreateTupleNameFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = FindAncestor<ArgumentSyntax>(root, span);
        if (oldNode is not ArgumentSyntax argument || !ModernSyntaxReadabilityAnalysis.TryGetInferredTupleElementName(argument, out _))
        {
            oldNode = null;
            return null;
        }

        return argument.WithNameColon(null).WithTriviaFrom(argument);
    }

    /// <summary>Creates a <c>System.HashCode.Combine</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The hash expression to replace.</param>
    /// <returns>The combine invocation.</returns>
    private static InvocationExpressionSyntax? CreateHashCodeCombineFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = FindAncestor<ExpressionSyntax>(root, span);
        if (oldNode is not ExpressionSyntax expression
            || !ModernSyntaxReadabilityAnalysis.TryCollectHashInputs(expression, out var inputs))
        {
            oldNode = null;
            return null;
        }

        var joined = JoinExpressions(inputs);
        return SyntaxFactory.ParseExpression($"System.HashCode.Combine({joined})").WithTriviaFrom(expression) as InvocationExpressionSyntax;
    }

    /// <summary>Joins expressions into an invocation argument list without LINQ allocations.</summary>
    /// <param name="inputs">The expressions to join.</param>
    /// <returns>The comma-separated expression text.</returns>
    private static string JoinExpressions(List<ExpressionSyntax> inputs)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < inputs.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(inputs[i].WithoutTrivia());
        }

        return builder.ToString();
    }

    /// <summary>Finds the two copied tuple element locals that follow a tuple temporary.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="local">The tuple temporary declaration.</param>
    /// <param name="first">The first copied local.</param>
    /// <param name="second">The second copied local.</param>
    /// <param name="firstName">The first deconstruction name.</param>
    /// <param name="secondName">The second deconstruction name.</param>
    /// <returns><see langword="true"/> when two copied locals are present.</returns>
    private static bool TryGetFollowingElementLocals(
        BlockSyntax block,
        LocalDeclarationStatementSyntax local,
        out LocalDeclarationStatementSyntax first,
        out LocalDeclarationStatementSyntax second,
        out string firstName,
        out string secondName)
    {
        first = null!;
        second = null!;
        firstName = string.Empty;
        secondName = string.Empty;
        if (!TryGetStatementIndex(block, local, out var index)
            || index + TwoFollowingStatements >= block.Statements.Count
            || block.Statements[index + 1] is not LocalDeclarationStatementSyntax firstLocal
            || block.Statements[index + TwoFollowingStatements] is not LocalDeclarationStatementSyntax secondLocal
            || !TryGetSingleVariableName(firstLocal, out firstName)
            || !TryGetSingleVariableName(secondLocal, out secondName))
        {
            return false;
        }

        first = firstLocal;
        second = secondLocal;
        return true;
    }

    /// <summary>Finds the two assignments that complete a local swap.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="local">The temporary declaration.</param>
    /// <param name="first">The first assignment.</param>
    /// <param name="second">The second assignment.</param>
    /// <param name="rightName">The right-side local name.</param>
    /// <returns><see langword="true"/> when the assignments are present.</returns>
    private static bool TryGetFollowingSwap(
        BlockSyntax block,
        LocalDeclarationStatementSyntax local,
        out ExpressionStatementSyntax first,
        out ExpressionStatementSyntax second,
        out string rightName)
    {
        first = null!;
        second = null!;
        rightName = string.Empty;
        if (!TryGetStatementIndex(block, local, out var index)
            || index + TwoFollowingStatements >= block.Statements.Count
            || block.Statements[index + 1] is not ExpressionStatementSyntax firstStatement
            || firstStatement.Expression is not AssignmentExpressionSyntax { Right: IdentifierNameSyntax right }
            || block.Statements[index + TwoFollowingStatements] is not ExpressionStatementSyntax secondStatement)
        {
            return false;
        }

        first = firstStatement;
        second = secondStatement;
        rightName = right.Identifier.ValueText;
        return rightName.Length > 0;
    }

    /// <summary>Gets a single declared variable name from a local declaration.</summary>
    /// <param name="local">The local declaration.</param>
    /// <param name="name">The variable name.</param>
    /// <returns><see langword="true"/> when one variable is declared.</returns>
    private static bool TryGetSingleVariableName(LocalDeclarationStatementSyntax local, out string name)
    {
        name = string.Empty;
        if (local.Declaration.Variables.Count != 1)
        {
            return false;
        }

        name = local.Declaration.Variables[0].Identifier.ValueText;
        return name.Length > 0;
    }

    /// <summary>Replaces one node and removes up to two tracked nodes.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <param name="replacement">The replacement node.</param>
    /// <param name="removeFirst">The first node to remove.</param>
    /// <param name="removeSecond">The second node to remove.</param>
    /// <returns>The updated root, or <see langword="null"/>.</returns>
    private static SyntaxNode? ReplaceAndRemove(
        SyntaxNode root,
        SyntaxNode oldNode,
        SyntaxNode replacement,
        SyntaxNode? removeFirst,
        SyntaxNode? removeSecond)
    {
        if (TryReplaceStatementsInBlock(root, oldNode, replacement, removeFirst, removeSecond, out var blockUpdated))
        {
            return blockUpdated;
        }

        var tracked = TrackNodes(root, oldNode, removeFirst, removeSecond);

        var trackedOld = tracked.GetCurrentNode(oldNode);
        if (trackedOld is null)
        {
            return null;
        }

        var updated = tracked.ReplaceNode(trackedOld, replacement);
        if (removeFirst is not null && updated.GetCurrentNode(removeFirst) is { } first)
        {
            updated = updated.RemoveNode(first, SyntaxRemoveOptions.KeepNoTrivia);
        }

        if (removeSecond is not null && updated?.GetCurrentNode(removeSecond) is { } second)
        {
            updated = updated.RemoveNode(second, SyntaxRemoveOptions.KeepNoTrivia);
        }

        return updated;
    }

    /// <summary>Rewrites statement lists directly when replacing a statement and removing siblings.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="oldNode">The statement to replace.</param>
    /// <param name="replacement">The replacement statement.</param>
    /// <param name="removeFirst">The first sibling to remove.</param>
    /// <param name="removeSecond">The second sibling to remove.</param>
    /// <param name="updated">The updated root.</param>
    /// <returns><see langword="true"/> when the statement list was rewritten.</returns>
    private static bool TryReplaceStatementsInBlock(
        SyntaxNode root,
        SyntaxNode oldNode,
        SyntaxNode replacement,
        SyntaxNode? removeFirst,
        SyntaxNode? removeSecond,
        out SyntaxNode? updated)
    {
        updated = null;
        if (oldNode is not StatementSyntax oldStatement
            || replacement is not StatementSyntax replacementStatement
            || oldStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        var oldIndex = block.Statements.IndexOf(oldStatement);
        if (oldIndex < 0)
        {
            return false;
        }

        var statements = block.Statements.Replace(oldStatement, replacementStatement);
        statements = RemoveStatement(statements, block, removeSecond);
        statements = RemoveStatement(statements, block, removeFirst);

        updated = root.ReplaceNode(block, block.WithStatements(statements));
        return true;
    }

    /// <summary>Removes one statement from a rewritten statement list by its original block position.</summary>
    /// <param name="statements">The statement list to update.</param>
    /// <param name="originalBlock">The original block.</param>
    /// <param name="node">The original statement node to remove.</param>
    /// <returns>The updated statement list.</returns>
    private static SyntaxList<StatementSyntax> RemoveStatement(
        SyntaxList<StatementSyntax> statements,
        BlockSyntax originalBlock,
        SyntaxNode? node)
    {
        if (node is not StatementSyntax statement)
        {
            return statements;
        }

        var index = originalBlock.Statements.IndexOf(statement);
        return index < 0 ? statements : statements.RemoveAt(index);
    }

    /// <summary>Tracks the replacement node plus optional remove nodes.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <param name="removeFirst">The first node to remove.</param>
    /// <param name="removeSecond">The second node to remove.</param>
    /// <returns>The root with nodes tracked.</returns>
    private static SyntaxNode TrackNodes(
        SyntaxNode root,
        SyntaxNode oldNode,
        SyntaxNode? removeFirst,
        SyntaxNode? removeSecond)
    {
        if (removeFirst is not null && removeSecond is not null)
        {
            return root.TrackNodes(oldNode, removeFirst, removeSecond);
        }

        return removeFirst is null ? root.TrackNodes(oldNode) : root.TrackNodes(oldNode, removeFirst);
    }

    /// <summary>Returns the index of a statement inside a block.</summary>
    /// <param name="block">The block.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="index">The statement index.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryGetStatementIndex(BlockSyntax block, StatementSyntax statement, out int index)
    {
        for (var i = 0; i < block.Statements.Count; i++)
        {
            if (block.Statements[i].Span == statement.Span)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    /// <summary>Finds the node at a span or one of its ancestors.</summary>
    /// <typeparam name="T">The ancestor node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    private static T? FindAncestor<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is T matched && matched.Span.Contains(span))
            {
                return matched;
            }

            node = node.Parent;
        }

        return null;
    }
}
