// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Applies mechanical fixes for grouped language-style readability rules (SST1193-SST1199).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LanguageStyleCodeFixProvider))]
[Shared]
public sealed class LanguageStyleCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The characters a conditional return adds around the expression: <c>return </c> and <c>;</c>.</summary>
    private const int ReturnWidth = 8;

    /// <summary>The characters a conditional assignment adds around the expression: <c> = </c> and <c>;</c>.</summary>
    private const int AssignmentWidth = 4;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ReadabilityRules.UseObjectInitializer.Id,
        ReadabilityRules.UseCollectionInitializer.Id,
        ReadabilityRules.UseNullCoalescingExpression.Id,
        ReadabilityRules.UseNullPropagation.Id,
        ReadabilityRules.UseConditionalExpressionForReturn.Id,
        ReadabilityRules.UseConditionalExpressionForAssignment.Id,
        ReadabilityRules.UseNameofType.Id);

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
            if (GetTitle(diagnostic.Id) is not { } title || !CanRewrite(root, diagnostic))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(context.Document, root, diagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var options = editor.OriginalDocument.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(editor.OriginalRoot.SyntaxTree);
        var replacement = CreateReplacement(editor.OriginalRoot, options, diagnostic, out var oldNode, out var removeNode);
        if (oldNode is null || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
        if (removeNode is null)
        {
            return;
        }

        editor.RemoveNode(removeNode, GetRemovalOptions(diagnostic, removeNode));
    }

    /// <summary>Applies one language-style fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree);
        var replacement = CreateReplacement(root, options, diagnostic, out var oldNode, out var removeNode);
        if (oldNode is null || replacement is null)
        {
            return document;
        }

        var tracked = removeNode is null ? root.TrackNodes(oldNode) : root.TrackNodes(oldNode, removeNode);
        var trackedOld = tracked.GetCurrentNode(oldNode);
        if (trackedOld is null)
        {
            return document;
        }

        var updated = tracked.ReplaceNode(trackedOld, replacement);
        if (removeNode is not null && updated.GetCurrentNode(removeNode) is { } trackedRemove)
        {
            updated = updated.RemoveNode(trackedRemove, GetRemovalOptions(diagnostic, removeNode));
        }

        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Preserves initializer comments and line boundaries without leaving an empty statement line.</summary>
    /// <param name="diagnostic">The applied diagnostic.</param>
    /// <param name="node">The original statement being absorbed.</param>
    /// <returns>The trivia to retain when removing the statement.</returns>
    private static SyntaxRemoveOptions GetRemovalOptions(Diagnostic diagnostic, SyntaxNode node)
    {
        if (diagnostic.Id is not ("SST1193" or "SST1194"))
        {
            return SyntaxRemoveOptions.KeepNoTrivia;
        }

        if (HasNonWhitespaceTrivia(node.GetLeadingTrivia()) || HasNonWhitespaceTrivia(node.GetTrailingTrivia()))
        {
            return SyntaxRemoveOptions.KeepExteriorTrivia;
        }

        foreach (var trivia in node.GetFirstToken().GetPreviousToken().TrailingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return SyntaxRemoveOptions.KeepNoTrivia;
            }
        }

        return SyntaxRemoveOptions.KeepEndOfLine;
    }

    /// <summary>Returns whether exterior trivia contains text that must survive statement removal.</summary>
    /// <param name="triviaList">The exterior trivia.</param>
    /// <returns>Whether any trivia is more than whitespace or a line break.</returns>
    private static bool HasNonWhitespaceTrivia(in SyntaxTriviaList triviaList)
    {
        foreach (var trivia in triviaList)
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the original syntax without constructing a replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the fix can be applied.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var span = diagnostic.Location.SourceSpan;
        return diagnostic.Id switch
        {
            "SST1193" => CanMoveIntoInitializer(root, span, collection: false),
            "SST1194" => CanMoveIntoInitializer(root, span, collection: true),
            "SST1195" => CanRewriteNullConditional(root, span, propagation: false),
            "SST1196" => CanRewriteNullConditional(root, span, propagation: true),
            "SST1197" => CanRewriteConditionalReturn(root, span),
            "SST1198" => FindAncestor<IfStatementSyntax>(root, span) is { Else.Statement: { } elseStatement } ifStatement
                && TryGetEmbeddedAssignment(ifStatement.Statement, out _, out _)
                && TryGetEmbeddedAssignment(elseStatement, out _, out _),
            "SST1199" => root.FindNode(span) is MemberAccessExpressionSyntax
            {
                Expression: TypeOfExpressionSyntax,
                Name.Identifier.ValueText: "Name",
            },
            _ => false,
        };
    }

    /// <summary>Checks the following assignment or Add call and its directive boundary.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="collection">Whether the initializer takes an Add argument.</param>
    /// <returns>Whether the following statement can move into the initializer.</returns>
    private static bool CanMoveIntoInitializer(SyntaxNode root, TextSpan span, bool collection)
    {
        if (!TryGetLocalObjectCreation(root, span, out _, out var local, out var block, out var variable, out _))
        {
            return false;
        }

        ExpressionStatementSyntax statement;
        var matched = collection
            ? TryGetFollowingAdd(block, local, variable.Identifier.ValueText, out statement, out _)
            : TryGetFollowingAssignment(block, local, variable.Identifier.ValueText, out statement, out _, out _);
        return matched && !statement.ContainsDirectives && !DirectiveBoundaries.Separate(local, statement);
    }

    /// <summary>Checks a null conditional using the exact text of its original operands.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="propagation">Whether the non-null branch must be a member access.</param>
    /// <returns>Whether the conditional can become the requested expression.</returns>
    private static bool CanRewriteNullConditional(SyntaxNode root, TextSpan span, bool propagation)
    {
        if (root.FindNode(span) is not ConditionalExpressionSyntax conditional
            || !TryGetNullConditionalParts(conditional, out var operand, out var fallback, out var whenNotNull))
        {
            return false;
        }

        return propagation
            ? fallback.IsKind(SyntaxKind.NullLiteralExpression)
                && whenNotNull is MemberAccessExpressionSyntax memberAccess
                && HaveSameText(memberAccess.Expression, operand)
            : HaveSameText(operand, whenNotNull);
    }

    /// <summary>Compares source spelling, including internal trivia, without allocating strings.</summary>
    /// <param name="left">The first expression in the original tree.</param>
    /// <param name="right">The second expression in the original tree.</param>
    /// <returns>Whether the expressions have identical text excluding outer trivia.</returns>
    private static bool HaveSameText(ExpressionSyntax left, ExpressionSyntax right)
    {
        var leftSpan = left.Span;
        var rightSpan = right.Span;
        if (leftSpan.Length != rightSpan.Length)
        {
            return false;
        }

        var text = left.SyntaxTree.GetText();
        for (var i = 0; i < leftSpan.Length; i++)
        {
            if (text[leftSpan.Start + i] != text[rightSpan.Start + i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks both returns before any conditional-expression layout is built.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <returns>Whether the returns can be collapsed without nesting conditionals or crossing directives.</returns>
    private static bool CanRewriteConditionalReturn(SyntaxNode root, TextSpan span) =>
        FindAncestor<IfStatementSyntax>(root, span) is { Parent: BlockSyntax block } ifStatement
            && TryGetEmbeddedReturn(ifStatement.Statement, out var whenTrue)
            && NextStatement(block, ifStatement) is ReturnStatementSyntax { Expression: { } whenFalse } followingReturn
            && !WouldNestConditionalExpression(ifStatement.Condition, whenTrue, whenFalse)
            && !DirectiveBoundaries.Separate(ifStatement, followingReturn);

    /// <summary>Creates the replacement node for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The syntax node to replace.</param>
    /// <param name="removeNode">The optional follow-up statement to remove.</param>
    /// <returns>The replacement node, or <see langword="null"/> when the source no longer matches.</returns>
    /// <remarks>
    /// Folding the follow-up statement into the first is refused when a directive stands between them: the
    /// statement that goes carries whichever half of the pair its trivia holds and leaves the other behind,
    /// and the surviving statement is one the directive no longer covers.
    /// </remarks>
    private static SyntaxNode? CreateReplacement(
        SyntaxNode root,
        AnalyzerConfigOptions options,
        Diagnostic diagnostic,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeNode)
    {
        var replacement = CreateEdit(root, options, diagnostic, out oldNode, out removeNode);
        if (oldNode is null || removeNode is null || !DirectiveBoundaries.Separate(oldNode, removeNode))
        {
            return replacement;
        }

        oldNode = null;
        removeNode = null;
        return null;
    }

    /// <summary>Builds one diagnostic's edit, before the directive check weighs it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The syntax node to replace.</param>
    /// <param name="removeNode">The optional follow-up statement to remove.</param>
    /// <returns>The replacement node, or <see langword="null"/> when the source no longer matches.</returns>
    private static SyntaxNode? CreateEdit(
        SyntaxNode root,
        AnalyzerConfigOptions options,
        Diagnostic diagnostic,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeNode)
    {
        oldNode = null;
        removeNode = null;
        return diagnostic.Id switch
        {
            "SST1193" => CreateObjectInitializerFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeNode),
            "SST1194" => CreateCollectionInitializerFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeNode),
            "SST1195" => CreateNullCoalescingFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST1196" => CreateNullPropagationFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST1197" => CreateConditionalReturnFix(root, options, diagnostic.Location.SourceSpan, out oldNode, out removeNode),
            "SST1198" => CreateConditionalAssignmentFix(root, options, diagnostic.Location.SourceSpan, out oldNode),
            "SST1199" => CreateNameofTypeFix(root, diagnostic.Location.SourceSpan, out oldNode),
            _ => null
        };
    }

    /// <summary>Creates an object-initializer replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The object creation node to replace.</param>
    /// <param name="removeNode">The assignment statement to remove.</param>
    /// <returns>The updated object creation, or <see langword="null"/>.</returns>
    private static ObjectCreationExpressionSyntax? CreateObjectInitializerFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode, out SyntaxNode? removeNode)
    {
        removeNode = null;
        if (!TryGetLocalObjectCreation(root, span, out var objectCreation, out var local, out var block, out var variable, out oldNode)
            || !TryGetFollowingAssignment(block, local, variable.Identifier.ValueText, out var assignmentStatement, out var memberAccess, out var value)
            || assignmentStatement.ContainsDirectives
            || DirectiveBoundaries.Separate(local, assignmentStatement))
        {
            oldNode = null;
            return null;
        }

        removeNode = assignmentStatement;
        var initializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.ObjectInitializerExpression,
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                memberAccess.Name.WithoutTrivia(),
                SyntaxFactory.Token(SyntaxKind.EqualsToken),
                value.WithoutTrivia())),
            SyntaxFactory.Token(default, SyntaxKind.CloseBraceToken, objectCreation.GetTrailingTrivia()));

        return objectCreation.Update(
            objectCreation.NewKeyword,
            objectCreation.Type,
            objectCreation.ArgumentList,
            initializer);
    }

    /// <summary>Creates a collection-initializer replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The object creation node to replace.</param>
    /// <param name="removeNode">The add-call statement to remove.</param>
    /// <returns>The updated object creation, or <see langword="null"/>.</returns>
    private static ObjectCreationExpressionSyntax? CreateCollectionInitializerFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode, out SyntaxNode? removeNode)
    {
        removeNode = null;
        if (!TryGetLocalObjectCreation(root, span, out var objectCreation, out var local, out var block, out var variable, out oldNode)
            || !TryGetFollowingAdd(block, local, variable.Identifier.ValueText, out var addStatement, out var invocation)
            || addStatement.ContainsDirectives
            || DirectiveBoundaries.Separate(local, addStatement))
        {
            oldNode = null;
            return null;
        }

        removeNode = addStatement;
        var initializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.CollectionInitializerExpression,
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
            SyntaxFactory.SingletonSeparatedList(invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia()),
            SyntaxFactory.Token(default, SyntaxKind.CloseBraceToken, objectCreation.GetTrailingTrivia()));

        return objectCreation.Update(
            objectCreation.NewKeyword,
            objectCreation.Type,
            objectCreation.ArgumentList,
            initializer);
    }

    /// <summary>Creates a null-coalescing replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The conditional expression node to replace.</param>
    /// <returns>The coalesce expression, or <see langword="null"/>.</returns>
    private static BinaryExpressionSyntax? CreateNullCoalescingFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = root.FindNode(span) as ConditionalExpressionSyntax;
        if (oldNode is not ConditionalExpressionSyntax conditional
            || !TryGetNullConditionalParts(conditional, out var operand, out var fallback, out var whenNotNull)
            || operand.ToString() != whenNotNull.ToString())
        {
            oldNode = null;
            return null;
        }

        return SyntaxFactory.BinaryExpression(
            SyntaxKind.CoalesceExpression,
            operand.WithoutTrailingTrivia().WithLeadingTrivia(conditional.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxKind.QuestionQuestionToken),
            fallback.WithoutLeadingTrivia().WithTrailingTrivia(conditional.GetTrailingTrivia()));
    }

    /// <summary>Creates a null-propagation replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The conditional expression node to replace.</param>
    /// <returns>The conditional access expression, or <see langword="null"/>.</returns>
    private static ConditionalAccessExpressionSyntax? CreateNullPropagationFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = root.FindNode(span) as ConditionalExpressionSyntax;
        if (oldNode is not ConditionalExpressionSyntax conditional
            || !TryGetNullConditionalParts(conditional, out var operand, out var fallback, out var whenNotNull)
            || !fallback.IsKind(SyntaxKind.NullLiteralExpression)
            || whenNotNull is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Expression.ToString() != operand.ToString())
        {
            oldNode = null;
            return null;
        }

        return SyntaxFactory.ConditionalAccessExpression(
            operand.WithoutTrailingTrivia().WithLeadingTrivia(conditional.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxKind.QuestionToken),
            SyntaxFactory.MemberBindingExpression(
                memberAccess.Name.WithoutLeadingTrivia().WithTrailingTrivia(conditional.GetTrailingTrivia())));
    }

    /// <summary>Creates a conditional-return replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The if statement to replace.</param>
    /// <param name="removeNode">The following return statement to remove.</param>
    /// <returns>The replacement return statement, or <see langword="null"/>.</returns>
    private static ReturnStatementSyntax? CreateConditionalReturnFix(
        SyntaxNode root,
        AnalyzerConfigOptions options,
        TextSpan span,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeNode)
    {
        oldNode = FindAncestor<IfStatementSyntax>(root, span);
        removeNode = null;
        if (oldNode is not IfStatementSyntax ifStatement
            || !TryGetEmbeddedReturn(ifStatement.Statement, out var whenTrue)
            || ifStatement.Parent is not BlockSyntax block
            || NextStatement(block, ifStatement) is not ReturnStatementSyntax { Expression: { } whenFalse } followingReturn
            || WouldNestConditionalExpression(ifStatement.Condition, whenTrue, whenFalse))
        {
            oldNode = null;
            return null;
        }

        removeNode = followingReturn;
        var conditional = LayOutConditional(ifStatement, options, ifStatement.Condition, whenTrue, whenFalse, ReturnWidth);
        return SyntaxFactory.ReturnStatement(
            SyntaxFactory.Token(ifStatement.GetLeadingTrivia(), SyntaxKind.ReturnKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            conditional,
            SyntaxFactory.Token(default, SyntaxKind.SemicolonToken, ifStatement.GetTrailingTrivia()));
    }

    /// <summary>Builds a conditional expression, wrapping its branches when one line would run past the maximum.</summary>
    /// <param name="anchor">The statement the replacement takes the place of.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="condition">The condition expression.</param>
    /// <param name="whenTrue">The expression used for the true branch.</param>
    /// <param name="whenFalse">The expression used for the false branch.</param>
    /// <param name="surroundingWidth">The characters the enclosing statement adds around the expression.</param>
    /// <returns>The conditional expression laid out to fit the line budget.</returns>
    /// <remarks>
    /// Each branch operator leads its continuation line one indent step in from the statement, which is the
    /// layout SST1140 and SST1145 ask for, so a wrap does not simply trade SST1521 for a layout diagnostic.
    /// </remarks>
    private static ConditionalExpressionSyntax LayOutConditional(
        StatementSyntax anchor,
        AnalyzerConfigOptions options,
        ExpressionSyntax condition,
        ExpressionSyntax whenTrue,
        ExpressionSyntax whenFalse,
        int surroundingWidth)
    {
        var text = anchor.SyntaxTree.GetText();
        var indent = LayoutFixHelpers.IndentOfLine(text, anchor.SpanStart);
        var joined = Conditional(condition, whenTrue, whenFalse, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        if (indent.Length + surroundingWidth + joined.Span.Length <= SizeLimitOptions.ReadMaxLineLength(options))
        {
            return joined;
        }

        var newLine = LayoutFixHelpers.DetectNewLine(text);
        return Conditional(
            condition,
            whenTrue,
            whenFalse,
            SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(newLine), SyntaxFactory.Whitespace(indent + LayoutFixHelpers.IndentStep)));
    }

    /// <summary>Builds a conditional expression whose branch operators carry the given leading trivia.</summary>
    /// <param name="condition">The condition expression.</param>
    /// <param name="whenTrue">The expression used for the true branch.</param>
    /// <param name="whenFalse">The expression used for the false branch.</param>
    /// <param name="operatorLeading">The trivia that precedes <c>?</c> and <c>:</c>.</param>
    /// <returns>The conditional expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConditionalExpressionSyntax Conditional(
        ExpressionSyntax condition,
        ExpressionSyntax whenTrue,
        ExpressionSyntax whenFalse,
        in SyntaxTriviaList operatorLeading) =>
        SyntaxFactory.ConditionalExpression(
            condition.WithoutTrivia(),
            SyntaxFactory.Token(operatorLeading, SyntaxKind.QuestionToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            whenTrue.WithoutTrivia(),
            SyntaxFactory.Token(operatorLeading, SyntaxKind.ColonToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            whenFalse.WithoutTrivia());

    /// <summary>Returns whether a conditional rewrite would create nested conditional expressions.</summary>
    /// <param name="condition">The condition expression.</param>
    /// <param name="whenTrue">The expression used for the true branch.</param>
    /// <param name="whenFalse">The expression used for the false branch.</param>
    /// <returns><see langword="true"/> when the replacement would nest a conditional expression.</returns>
    private static bool WouldNestConditionalExpression(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse) =>
        ContainsConditionalExpression(condition)
            || ContainsConditionalExpression(whenTrue)
            || ContainsConditionalExpression(whenFalse);

    /// <summary>Returns whether an expression contains a conditional expression.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when a conditional expression is present.</returns>
    private static bool ContainsConditionalExpression(ExpressionSyntax expression)
    {
        if (expression is ConditionalExpressionSyntax)
        {
            return true;
        }

        var found = false;
        _ = DescendantTraversalHelper.VisitDescendants(
            expression,
            ref found,
            static (ConditionalExpressionSyntax node, ref bool state) =>
            {
                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Creates a conditional-assignment replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="options">The tree's configuration.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The if statement to replace.</param>
    /// <returns>The replacement assignment statement, or <see langword="null"/>.</returns>
    private static ExpressionStatementSyntax? CreateConditionalAssignmentFix(
        SyntaxNode root,
        AnalyzerConfigOptions options,
        TextSpan span,
        out SyntaxNode? oldNode)
    {
        oldNode = FindAncestor<IfStatementSyntax>(root, span);
        if (oldNode is not IfStatementSyntax ifStatement
            || !TryGetEmbeddedAssignment(ifStatement.Statement, out var target, out var whenTrue)
            || ifStatement.Else?.Statement is not { } elseStatement
            || !TryGetEmbeddedAssignment(elseStatement, out _, out var whenFalse))
        {
            oldNode = null;
            return null;
        }

        var assigned = target.WithoutTrivia();
        var conditional = LayOutConditional(
            ifStatement,
            options,
            ifStatement.Condition,
            whenTrue,
            whenFalse,
            AssignmentWidth + assigned.Span.Length);
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                assigned.WithLeadingTrivia(ifStatement.GetLeadingTrivia()),
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.EqualsToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                conditional),
            SyntaxFactory.Token(default, SyntaxKind.SemicolonToken, ifStatement.GetTrailingTrivia()));
    }

    /// <summary>Creates a <c>nameof</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The <c>typeof(T).Name</c> node to replace.</param>
    /// <returns>The <c>nameof(T)</c> expression, or <see langword="null"/>.</returns>
    private static InvocationExpressionSyntax? CreateNameofTypeFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = root.FindNode(span) as MemberAccessExpressionSyntax;
        if (oldNode is not MemberAccessExpressionSyntax { Expression: TypeOfExpressionSyntax { Type: { } type }, Name.Identifier.ValueText: "Name" } memberAccess)
        {
            oldNode = null;
            return null;
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(memberAccess.GetLeadingTrivia(), "nameof", SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker))),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.ParseExpression(type.ToString()))),
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, memberAccess.GetTrailingTrivia())));
    }

    /// <summary>Returns the next statement in a block.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="statement">The current statement.</param>
    /// <returns>The next statement, or <see langword="null"/>.</returns>
    private static StatementSyntax? NextStatement(BlockSyntax block, StatementSyntax statement)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count - 1; i++)
        {
            if (statements[i].Span == statement.Span)
            {
                return statements[i + 1];
            }
        }

        return null;
    }

    /// <summary>Gets an empty local object creation at a diagnostic span.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="objectCreation">The object creation expression.</param>
    /// <param name="local">The local declaration statement.</param>
    /// <param name="block">The containing block.</param>
    /// <param name="variable">The declared variable.</param>
    /// <param name="oldNode">The object creation node as a replace target.</param>
    /// <returns><see langword="true"/> when the source has the expected local creation shape.</returns>
    private static bool TryGetLocalObjectCreation(
        SyntaxNode root,
        TextSpan span,
        out ObjectCreationExpressionSyntax objectCreation,
        out LocalDeclarationStatementSyntax local,
        out BlockSyntax block,
        out VariableDeclaratorSyntax variable,
        out SyntaxNode? oldNode)
    {
        oldNode = root.FindNode(span) as ObjectCreationExpressionSyntax;
        objectCreation = null!;
        local = null!;
        block = null!;
        variable = null!;
        if (oldNode is not ObjectCreationExpressionSyntax foundCreation
            || foundCreation.Parent is not EqualsValueClauseSyntax
            || foundCreation.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is not { } localDeclaration
            || localDeclaration.Declaration.Variables.Count != 1
            || localDeclaration.Declaration.Variables[0] is not { } declarator
            || localDeclaration.Parent is not BlockSyntax parentBlock)
        {
            return false;
        }

        objectCreation = foundCreation;
        local = localDeclaration;
        block = parentBlock;
        variable = declarator;
        return true;
    }

    /// <summary>Gets the member assignment immediately after a local declaration.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="local">The local declaration statement.</param>
    /// <param name="variableName">The declared variable name.</param>
    /// <param name="statement">The assignment statement.</param>
    /// <param name="memberAccess">The assigned member access.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns><see langword="true"/> when the next statement assigns a member of the local.</returns>
    private static bool TryGetFollowingAssignment(
        BlockSyntax block,
        LocalDeclarationStatementSyntax local,
        string variableName,
        out ExpressionStatementSyntax statement,
        out MemberAccessExpressionSyntax memberAccess,
        out ExpressionSyntax value)
    {
        statement = null!;
        memberAccess = null!;
        value = null!;
        if (NextStatement(block, local) is not ExpressionStatementSyntax assignmentStatement
            || assignmentStatement.Expression is not AssignmentExpressionSyntax assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || assignment.Left is not MemberAccessExpressionSyntax assignedMember
            || assignment.Right is not { } assignedValue
            || assignedMember.Expression is not IdentifierNameSyntax receiver
            || receiver.Identifier.ValueText != variableName)
        {
            return false;
        }

        statement = assignmentStatement;
        memberAccess = assignedMember;
        value = assignedValue;
        return true;
    }

    /// <summary>Gets the <c>Add</c> call immediately after a local declaration.</summary>
    /// <param name="block">The containing block.</param>
    /// <param name="local">The local declaration statement.</param>
    /// <param name="variableName">The declared variable name.</param>
    /// <param name="statement">The add-call statement.</param>
    /// <param name="invocation">The <c>Add</c> invocation.</param>
    /// <returns><see langword="true"/> when the next statement calls <c>Add</c> on the local.</returns>
    private static bool TryGetFollowingAdd(
        BlockSyntax block,
        LocalDeclarationStatementSyntax local,
        string variableName,
        out ExpressionStatementSyntax statement,
        out InvocationExpressionSyntax invocation)
    {
        statement = null!;
        invocation = null!;
        if (NextStatement(block, local) is not ExpressionStatementSyntax addStatement
            || addStatement.Expression is not InvocationExpressionSyntax addInvocation
            || addInvocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Expression is not IdentifierNameSyntax receiver
            || memberAccess.Name.Identifier.ValueText != "Add"
            || addInvocation.ArgumentList.Arguments.Count != 1
            || receiver.Identifier.ValueText != variableName)
        {
            return false;
        }

        statement = addStatement;
        invocation = addInvocation;
        return true;
    }

    /// <summary>Extracts a null-conditional expression shape.</summary>
    /// <param name="conditional">The conditional expression to inspect.</param>
    /// <param name="operand">The expression compared to <c>null</c>.</param>
    /// <param name="fallback">The expression used when the operand is <c>null</c>.</param>
    /// <param name="whenNotNull">The expression used when the operand is not <c>null</c>.</param>
    /// <returns><see langword="true"/> when the conditional expression has a null-check shape.</returns>
    private static bool TryGetNullConditionalParts(
        ConditionalExpressionSyntax conditional,
        out ExpressionSyntax operand,
        out ExpressionSyntax fallback,
        out ExpressionSyntax whenNotNull)
    {
        operand = null!;
        fallback = null!;
        whenNotNull = null!;
        if (ExpressionSimplificationAnalyzer.Unwrap(conditional.Condition) is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        var leftNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        var rightNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (leftNull == rightNull)
        {
            return false;
        }

        operand = leftNull ? binary.Right : binary.Left;
        if (binary.IsKind(SyntaxKind.EqualsExpression))
        {
            fallback = conditional.WhenTrue;
            whenNotNull = conditional.WhenFalse;
            return true;
        }

        if (!binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return false;
        }

        fallback = conditional.WhenFalse;
        whenNotNull = conditional.WhenTrue;
        return true;
    }

    /// <summary>Returns a return expression from a statement or single-statement block.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="expression">The returned expression.</param>
    /// <returns><see langword="true"/> when the statement is a value return.</returns>
    private static bool TryGetEmbeddedReturn(StatementSyntax statement, out ExpressionSyntax expression)
    {
        expression = null!;
        if (statement is ReturnStatementSyntax { Expression: { } returnExpression })
        {
            expression = returnExpression;
            return true;
        }

        if (statement is not BlockSyntax { Statements.Count: 1 } block
            || block.Statements[0] is not ReturnStatementSyntax { Expression: { } blockExpression })
        {
            return false;
        }

        expression = blockExpression;
        return true;
    }

    /// <summary>Returns a target and value from a simple assignment statement or single-statement block.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="target">The assignment target.</param>
    /// <param name="value">The assigned value.</param>
    /// <returns><see langword="true"/> when the statement is a simple assignment.</returns>
    private static bool TryGetEmbeddedAssignment(
        StatementSyntax statement,
        out ExpressionSyntax target,
        out ExpressionSyntax value)
    {
        target = null!;
        value = null!;
        ExpressionSyntax? expression = null;
        if (statement is ExpressionStatementSyntax expressionStatement)
        {
            expression = expressionStatement.Expression;
        }
        else if (statement is BlockSyntax { Statements.Count: 1 } block
            && block.Statements[0] is ExpressionStatementSyntax blockStatement)
        {
            expression = blockStatement.Expression;
        }

        if (expression is not AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment)
        {
            return false;
        }

        target = assignment.Left;
        value = assignment.Right;
        return true;
    }

    /// <summary>Finds the node at a span or one of its ancestors.</summary>
    /// <typeparam name="T">The ancestor node type to find.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    private static T? FindAncestor<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
    {
        var node = root.FindToken(span.Start).Parent;
        while (node is not null)
        {
            if (node is T matched)
            {
                return matched;
            }

            node = node.Parent;
        }

        return null;
    }

    /// <summary>Gets the code action title for a supported diagnostic id.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The code action title, or <see langword="null"/>.</returns>
    private static string? GetTitle(string diagnosticId) =>
        diagnosticId switch
        {
            "SST1193" => "Move assignment into initializer",
            "SST1194" => "Move Add call into initializer",
            "SST1195" => "Write fallback with ??",
            "SST1196" => "Write guarded access with ?.",
            "SST1197" => "Collapse into one conditional return",
            "SST1198" => "Collapse into one conditional assignment",
            "SST1199" => "Replace runtime type name with nameof",
            _ => null
        };
}
