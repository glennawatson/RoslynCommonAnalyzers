// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies mechanical fixes for modern readability rules (SST2212-SST2217).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxReadabilityCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxReadabilityCodeFixProvider : CodeFixProvider
{
    /// <summary>The number of following statements rewritten by tuple deconstruction and swap fixes.</summary>
    private const int TwoFollowingStatements = 2;

    /// <summary>A supported multiplier in generated hash-code expressions.</summary>
    private const int HashMultiplier397 = 397;

    /// <summary>A supported multiplier in generated hash-code expressions.</summary>
    private const int HashMultiplier31 = 31;

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.UseUtf8StringLiteral.Id,
        ModernSyntaxRules.RemoveUnnecessaryDiscard.Id,
        ModernSyntaxRules.UseDeconstruction.Id,
        ModernSyntaxRules.UseTupleSwap.Id,
        ModernSyntaxRules.UseInferredTupleElementName.Id,
        ModernSyntaxRules.UseHashCodeCombine.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            static diagnostic => GetTitle(diagnostic.Id),
            static diagnostic => diagnostic.Id,
            CanCreateEdit,
            Apply);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
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

    /// <summary>Checks the original syntax without constructing the edit offered by the action.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the edit's syntax preconditions still hold.</returns>
    private static bool CanCreateEdit(SyntaxNode root, Diagnostic diagnostic)
    {
        var span = diagnostic.Location.SourceSpan;
        return diagnostic.Id switch
        {
            "SST2212" => CanCreateUtf8Fix(root, diagnostic),
            "SST2213" => FindAncestor<DeclarationPatternSyntax>(root, span)?.Parent is IsPatternExpressionSyntax,
            "SST2214" => FindAncestor<LocalDeclarationStatementSyntax>(root, span) is { Parent: BlockSyntax block } local
                && TryGetSingleInitializer(local, out _)
                && TryGetFollowingElementLocals(block, local, out _, out _, out _, out _),
            "SST2215" => FindAncestor<LocalDeclarationStatementSyntax>(root, span) is { Parent: BlockSyntax block } local
                && TryGetSingleIdentifierInitializer(local, out _)
                && TryGetFollowingSwap(block, local, out _, out _, out _),
            "SST2216" => FindAncestor<ArgumentSyntax>(root, span) is { } argument
                && ModernSyntaxReadabilityAnalysis.TryGetInferredTupleElementName(argument, out _),
            "SST2217" => FindAncestor<ExpressionSyntax>(root, span) is { } expression
                && CountHashInputs(expression) >= ModernSyntaxReadabilityAnalysis.HashCodeCombineMinInputs,
            _ => false
        };
    }

    /// <summary>Checks literal shape and target metadata without creating a UTF-8 token.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether a string literal and non-null target metadata are present.</returns>
    private static bool CanCreateUtf8Fix(SyntaxNode root, Diagnostic diagnostic) =>
        FindAncestor<ExpressionSyntax>(root, diagnostic.Location.SourceSpan) is InvocationExpressionSyntax
            { ArgumentList.Arguments: [{ Expression: LiteralExpressionSyntax literal }] }
        && literal.IsKind(SyntaxKind.StringLiteralExpression)
        && diagnostic.Properties.TryGetValue(ModernSyntaxReadabilityAnalysis.Utf8TargetKey, out var target)
        && target is not null;

    /// <summary>Counts supported hash inputs without allocating the list needed only by the rewrite.</summary>
    /// <param name="expression">The original hash expression.</param>
    /// <returns>The input count, or zero for an unsupported shape or too many inputs.</returns>
    private static int CountHashInputs(ExpressionSyntax expression)
    {
        expression = ExpressionShapes.WalkDownParentheses(expression);
        if (IsHashInput(expression))
        {
            return 1;
        }

        if (expression is not BinaryExpressionSyntax binary
            || (!binary.IsKind(SyntaxKind.ExclusiveOrExpression) && !binary.IsKind(SyntaxKind.AddExpression))
            || GetMultipliedHash(binary.Left) is not { } multiplied
            || CountHashInputs(multiplied) is not (> 0 and var leftCount))
        {
            return 0;
        }

        var rightCount = CountHashInputs(binary.Right);
        var count = leftCount + rightCount;
        return rightCount > 0 && count <= ModernSyntaxReadabilityAnalysis.HashCodeCombineMaxInputs ? count : 0;
    }

    /// <summary>Recognizes a supported hash receiver without retaining it in a collection.</summary>
    /// <param name="expression">The unwrapped expression.</param>
    /// <returns>Whether the expression is a supported zero-argument hash call.</returns>
    private static bool IsHashInput(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax
        {
            ArgumentList.Arguments.Count: 0,
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: nameof(GetHashCode), Expression: { } receiver }
        }
        && ExpressionShapes.WalkDownParentheses(receiver) is IdentifierNameSyntax or MemberAccessExpressionSyntax;

    /// <summary>Finds the hash operand paired with a supported multiplier.</summary>
    /// <param name="expression">The original multiplication expression.</param>
    /// <returns>The hash operand, or null for an unsupported multiplication.</returns>
    private static ExpressionSyntax? GetMultipliedHash(ExpressionSyntax expression)
    {
        if (ExpressionShapes.WalkDownParentheses(expression) is not BinaryExpressionSyntax multiply
            || !multiply.IsKind(SyntaxKind.MultiplyExpression))
        {
            return null;
        }

        if (IsHashMultiplier(multiply.Right))
        {
            return multiply.Left;
        }

        return IsHashMultiplier(multiply.Left) ? multiply.Right : null;
    }

    /// <summary>Recognizes the same multiplier literals accepted by hash input collection.</summary>
    /// <param name="expression">The candidate multiplier.</param>
    /// <returns>Whether the unwrapped expression is a supported integer multiplier.</returns>
    private static bool IsHashMultiplier(ExpressionSyntax expression) =>
        ExpressionShapes.WalkDownParentheses(expression) is LiteralExpressionSyntax literal
        && literal.Token.Value is int value
        && value is HashMultiplier397 or HashMultiplier31;

    /// <summary>Gets the user-facing title for one diagnostic id.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The code action title, or <see langword="null"/>.</returns>
    private static string? GetTitle(string diagnosticId) =>
        diagnosticId switch
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
            isPattern.Expression.WithoutTrailingTrivia(),
            SyntaxFactory.Token(SyntaxKind.IsKeyword),
            pattern.Type.WithTrailingTrivia(isPattern.GetTrailingTrivia()));
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

        var refKeyword = argument.RefKindKeyword;
        return argument.Update(
            nameColon: null,
            refKeyword.RawKind == 0 ? refKeyword : refKeyword.WithLeadingTrivia(argument.GetLeadingTrivia()),
            refKeyword.RawKind == 0 ? argument.Expression.WithLeadingTrivia(argument.GetLeadingTrivia()) : argument.Expression);
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
        var capacity = inputs.Count > 0 ? (inputs.Count - 1) * ", ".Length : 0;
        for (var i = 0; i < inputs.Count; i++)
        {
            capacity += inputs[i].Span.Length;
        }

        var builder = new System.Text.StringBuilder(capacity);
        for (var i = 0; i < inputs.Count; i++)
        {
            if (i > 0)
            {
                _ = builder.Append(", ");
            }

            _ = builder.Append(inputs[i].WithoutTrivia());
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
        if (!ModernSyntaxReadabilityAnalysis.TryGetStatementIndex(block, local, out var index)
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
        if (!ModernSyntaxReadabilityAnalysis.TryGetStatementIndex(block, local, out var index)
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
