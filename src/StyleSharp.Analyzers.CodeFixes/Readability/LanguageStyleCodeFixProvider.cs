// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies mechanical fixes for grouped language-style readability rules (SST1193-SST1199).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LanguageStyleCodeFixProvider))]
[Shared]
public sealed class LanguageStyleCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
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
            if (GetTitle(diagnostic.Id) is not { } title
                || CreateReplacement(root, diagnostic, out _, out _) is null)
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
        var replacement = CreateReplacement(editor.OriginalRoot, diagnostic, out var oldNode, out var removeNode);
        if (oldNode is null || replacement is null)
        {
            return;
        }

        editor.ReplaceNode(oldNode, replacement);
        if (removeNode is null)
        {
            return;
        }

        editor.RemoveNode(removeNode);
    }

    /// <summary>Applies one language-style fix.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
    {
        var replacement = CreateReplacement(root, diagnostic, out var oldNode, out var removeNode);
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
            updated = updated.RemoveNode(trackedRemove, SyntaxRemoveOptions.KeepNoTrivia);
        }

        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Creates the replacement node for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="oldNode">The syntax node to replace.</param>
    /// <param name="removeNode">The optional follow-up statement to remove.</param>
    /// <returns>The replacement node, or <see langword="null"/> when the source no longer matches.</returns>
    private static SyntaxNode? CreateReplacement(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode, out SyntaxNode? removeNode)
    {
        oldNode = null;
        removeNode = null;
        return diagnostic.Id switch
        {
            "SST1193" => CreateObjectInitializerFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeNode),
            "SST1194" => CreateCollectionInitializerFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeNode),
            "SST1195" => CreateNullCoalescingFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST1196" => CreateNullPropagationFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST1197" => CreateConditionalReturnFix(root, diagnostic.Location.SourceSpan, out oldNode, out removeNode),
            "SST1198" => CreateConditionalAssignmentFix(root, diagnostic.Location.SourceSpan, out oldNode),
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
            || !TryGetFollowingAssignment(block, local, variable.Identifier.ValueText, out var assignmentStatement, out var memberAccess, out var value))
        {
            oldNode = null;
            return null;
        }

        removeNode = assignmentStatement;
        var initializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.ObjectInitializerExpression,
            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                memberAccess.Name.WithoutTrivia(),
                value.WithoutTrivia())));

        return objectCreation.WithInitializer(initializer).WithTriviaFrom(objectCreation);
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
            || !TryGetFollowingAdd(block, local, variable.Identifier.ValueText, out var addStatement, out var invocation))
        {
            oldNode = null;
            return null;
        }

        removeNode = addStatement;
        var initializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.CollectionInitializerExpression,
            SyntaxFactory.SingletonSeparatedList(invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia()));

        return objectCreation.WithInitializer(initializer).WithTriviaFrom(objectCreation);
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
                operand.WithoutTrivia(),
                fallback.WithoutTrivia())
            .WithTriviaFrom(conditional);
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
                operand.WithoutTrivia(),
                SyntaxFactory.MemberBindingExpression(memberAccess.Name.WithoutTrivia()))
            .WithTriviaFrom(conditional);
    }

    /// <summary>Creates a conditional-return replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The if statement to replace.</param>
    /// <param name="removeNode">The following return statement to remove.</param>
    /// <returns>The replacement return statement, or <see langword="null"/>.</returns>
    private static ReturnStatementSyntax? CreateConditionalReturnFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode, out SyntaxNode? removeNode)
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
        var conditional = SyntaxFactory.ConditionalExpression(
            ifStatement.Condition.WithoutTrivia(),
            whenTrue.WithoutTrivia(),
            whenFalse.WithoutTrivia());
        return SyntaxFactory.ReturnStatement(conditional).WithTriviaFrom(ifStatement);
    }

    /// <summary>Returns whether a conditional rewrite would create nested conditional expressions.</summary>
    /// <param name="condition">The condition expression.</param>
    /// <param name="whenTrue">The expression used for the true branch.</param>
    /// <param name="whenFalse">The expression used for the false branch.</param>
    /// <returns><see langword="true"/> when the replacement would nest a conditional expression.</returns>
    private static bool WouldNestConditionalExpression(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse)
        => ContainsConditionalExpression(condition)
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

        foreach (var node in expression.DescendantNodes(static node => node is not ConditionalExpressionSyntax))
        {
            if (node is ConditionalExpressionSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates a conditional-assignment replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic source span.</param>
    /// <param name="oldNode">The if statement to replace.</param>
    /// <returns>The replacement assignment statement, or <see langword="null"/>.</returns>
    private static ExpressionStatementSyntax? CreateConditionalAssignmentFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
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

        var conditional = SyntaxFactory.ConditionalExpression(
            ifStatement.Condition.WithoutTrivia(),
            whenTrue.WithoutTrivia(),
            whenFalse.WithoutTrivia());
        return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                target.WithoutTrivia(),
                conditional))
            .WithTriviaFrom(ifStatement);
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
                SyntaxFactory.IdentifierName("nameof"),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(SyntaxFactory.ParseExpression(type.ToString())))))
            .WithTriviaFrom(memberAccess);
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
    private static string? GetTitle(string diagnosticId)
        => diagnosticId switch
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
