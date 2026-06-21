// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Applies mechanical fixes for value, cast, and LINQ modern syntax rules (SST2220-SST2232).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModernSyntaxValueCodeFixProvider))]
[Shared]
public sealed class ModernSyntaxValueCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix, IBatchEditKeyProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernSyntaxRules.SimplifyInterpolation.Id,
        ModernSyntaxRules.MakeIgnoredExpressionValueExplicit.Id,
        ModernSyntaxRules.RemoveOverwrittenValue.Id,
        ModernSyntaxRules.UseCoalesceAssignment.Id,
        ModernSyntaxRules.ConvertAnonymousObjectToTuple.Id,
        ModernSyntaxRules.AddExplicitForeachCast.Id,
        ModernSyntaxRules.AddVisibleInnerCast.Id,
        ModernSyntaxRules.FoldNullCheckIntoAssignment.Id,
        ModernSyntaxRules.UseLocalFunction.Id,
        ModernSyntaxRules.CollapseLinqWhereTerminal.Id,
        ModernSyntaxRules.CollapseLinqTypeFilter.Id,
        ModernSyntaxRules.UseDirectNullPattern.Id,
        ModernSyntaxRules.UseUnboundGenericName.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => ModernSyntaxValueFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var title = GetTitle(diagnostic.Id);
            if (title is null || CreateEdit(root, model, diagnostic, out _, out _, context.CancellationToken) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    _ => Task.FromResult(Apply(context.Document, root, model, diagnostic)),
                    equivalenceKey: diagnostic.Id),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(editor.OriginalRoot, diagnostic, out var oldNode, out var removeNode);
        if (oldNode is null)
        {
            return;
        }

        if (replacement is not null)
        {
            editor.ReplaceNode(oldNode, replacement);
        }
        else
        {
            editor.RemoveNode(oldNode, SyntaxRemoveOptions.KeepNoTrivia);
        }

        if (removeNode is null)
        {
            return;
        }

        editor.RemoveNode(removeNode, SyntaxRemoveOptions.KeepNoTrivia);
    }

    /// <inheritdoc/>
    bool IBatchEditKeyProvider.TryGetBatchEditSpan(SyntaxNode root, Diagnostic diagnostic, out TextSpan span)
    {
        if (diagnostic.Id == ModernSyntaxRules.MakeIgnoredExpressionValueExplicit.Id
            && TryGetIgnoredValueEditSpan(root, diagnostic.Location.SourceSpan, out span))
        {
            return true;
        }

        _ = CreateEdit(root, diagnostic, out var oldNode, out _);
        if (oldNode is null)
        {
            span = default;
            return false;
        }

        span = oldNode.Span;
        return true;
    }

    /// <summary>Applies one value-syntax fix.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, Diagnostic diagnostic)
        => Apply(document, root, model: null, diagnostic);

    /// <summary>Applies one value-syntax fix.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The optional semantic model.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, SemanticModel? model, Diagnostic diagnostic)
    {
        var replacement = CreateEdit(root, model, diagnostic, out var oldNode, out var removeNode, CancellationToken.None);
        if (oldNode is null)
        {
            return document;
        }

        var tracked = removeNode is null ? root.TrackNodes(oldNode) : root.TrackNodes(oldNode, removeNode);
        if (tracked.GetCurrentNode(oldNode) is not { } trackedOld)
        {
            return document;
        }

        SyntaxNode? updated = replacement is null
            ? tracked.RemoveNode(trackedOld, SyntaxRemoveOptions.KeepNoTrivia)
            : tracked.ReplaceNode(trackedOld, replacement);
        if (updated is null)
        {
            return document;
        }

        if (removeNode is not null && updated.GetCurrentNode(removeNode) is { } trackedRemove)
        {
            updated = updated.RemoveNode(trackedRemove, SyntaxRemoveOptions.KeepNoTrivia);
        }

        return updated is null ? document : document.WithSyntaxRoot(updated);
    }

    /// <summary>Gets the edited statement span for an ignored-value diagnostic without requiring semantic proof.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnosticSpan">The diagnostic span.</param>
    /// <param name="editSpan">The resolved statement span.</param>
    /// <returns><see langword="true"/> when a statement edit target was found.</returns>
    private static bool TryGetIgnoredValueEditSpan(SyntaxNode root, TextSpan diagnosticSpan, out TextSpan editSpan)
    {
        var statement = FindAncestor<ExpressionStatementSyntax>(root, diagnosticSpan);
        if (statement is null || IsDiscardAssignment(statement.Expression))
        {
            editSpan = default;
            return false;
        }

        editSpan = statement.Span;
        return true;
    }

    /// <summary>Returns a code action title.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The title, or <see langword="null"/>.</returns>
    private static string? GetTitle(string diagnosticId)
        => GetValueTitle(diagnosticId) ?? GetLinqAndPatternTitle(diagnosticId);

    /// <summary>Returns a code action title for the first value-syntax batch.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The title, or <see langword="null"/>.</returns>
    private static string? GetValueTitle(string diagnosticId)
        => diagnosticId switch
        {
            "SST2220" => "Move ToString into interpolation",
            "SST2221" => "Assign ignored value to discard",
            "SST2222" => "Remove overwritten value",
            "SST2223" => "Use coalescing assignment",
            "SST2224" => "Use tuple literal",
            "SST2225" => "Cast foreach source explicitly",
            "SST2226" => "Add inner cast",
            "SST2227" => "Fold null check into assignment",
            _ => null
        };

    /// <summary>Returns a code action title for LINQ, pattern, and nameof rules.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <returns>The title, or <see langword="null"/>.</returns>
    private static string? GetLinqAndPatternTitle(string diagnosticId)
        => diagnosticId switch
        {
            "SST2228" => "Use a local function",
            "SST2229" => "Move predicate to terminal call",
            "SST2230" => "Use one typed filter",
            "SST2231" => "Use a direct null pattern",
            "SST2232" => "Omit generic arguments in nameof",
            _ => null
        };

    /// <summary>Creates the syntax edit for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to replace or remove.</param>
    /// <param name="removeNode">An additional node to remove after replacement.</param>
    /// <returns>The replacement node, <see langword="null"/> for remove-only, or <see langword="null"/> with no old node when no fix is available.</returns>
    private static SyntaxNode? CreateEdit(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode, out SyntaxNode? removeNode)
        => CreateEdit(root, model: null, diagnostic, out oldNode, out removeNode, CancellationToken.None);

    /// <summary>Creates the syntax edit for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The optional semantic model.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to replace or remove.</param>
    /// <param name="removeNode">An additional node to remove after replacement.</param>
    /// <param name="cancellationToken">A token that cancels semantic checks.</param>
    /// <returns>The replacement node, <see langword="null"/> for remove-only, or <see langword="null"/> with no old node when no fix is available.</returns>
    private static SyntaxNode? CreateEdit(
        SyntaxNode root,
        SemanticModel? model,
        Diagnostic diagnostic,
        out SyntaxNode? oldNode,
        out SyntaxNode? removeNode,
        CancellationToken cancellationToken)
    {
        oldNode = null;
        removeNode = null;
        return CreateValueEdit(root, model, diagnostic, ref oldNode, ref removeNode, cancellationToken)
            ?? CreateLinqAndPatternEdit(root, diagnostic, ref oldNode);
    }

    /// <summary>Creates the syntax edit for the first value-syntax batch.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The optional semantic model.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to replace or remove.</param>
    /// <param name="removeNode">An additional node to remove after replacement.</param>
    /// <param name="cancellationToken">A token that cancels semantic checks.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static SyntaxNode? CreateValueEdit(
        SyntaxNode root,
        SemanticModel? model,
        Diagnostic diagnostic,
        ref SyntaxNode? oldNode,
        ref SyntaxNode? removeNode,
        CancellationToken cancellationToken)
        => diagnostic.Id switch
        {
            "SST2220" => CreateInterpolationFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2221" => CreateIgnoredValueFix(root, model, diagnostic.Location.SourceSpan, out oldNode, cancellationToken),
            "SST2222" => CreateOverwrittenValueFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2223" => CreateCoalesceAssignmentFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2224" => CreateTupleLiteralFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2225" => CreateForeachCastFix(root, diagnostic, out oldNode),
            "SST2226" => CreateVisibleInnerCastFix(root, diagnostic, out oldNode),
            "SST2227" => CreateFoldNullCheckFix(root, diagnostic, out oldNode, out removeNode),
            _ => null
        };

    /// <summary>Creates the syntax edit for LINQ, pattern, and nameof rules.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The node to replace or remove.</param>
    /// <returns>The replacement node, or <see langword="null"/>.</returns>
    private static SyntaxNode? CreateLinqAndPatternEdit(SyntaxNode root, Diagnostic diagnostic, ref SyntaxNode? oldNode)
        => diagnostic.Id switch
        {
            "SST2228" => CreateLocalFunctionFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2229" => CreateWhereTerminalFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2230" => CreateTypeFilterFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2231" => CreateNullPatternFix(root, diagnostic.Location.SourceSpan, out oldNode),
            "SST2232" => CreateUnboundGenericNameFix(root, diagnostic.Location.SourceSpan, out oldNode),
            _ => null
        };

    /// <summary>Creates an interpolation simplification.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The interpolation to replace.</param>
    /// <returns>The updated interpolation.</returns>
    private static InterpolationSyntax? CreateInterpolationFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        var interpolation = FindAncestor<InterpolationSyntax>(root, span);
        if (interpolation is null || !ModernSyntaxValueAnalyzer.TryGetSimplifiedInterpolation(interpolation, out var replacement))
        {
            oldNode = null;
            return null;
        }

        oldNode = interpolation;
        return replacement;
    }

    /// <summary>Creates an explicit discard assignment for an ignored value.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The optional semantic model.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The statement to replace.</param>
    /// <param name="cancellationToken">A token that cancels semantic checks.</param>
    /// <returns>The replacement statement.</returns>
    private static ExpressionStatementSyntax? CreateIgnoredValueFix(
        SyntaxNode root,
        SemanticModel? model,
        TextSpan span,
        out SyntaxNode? oldNode,
        CancellationToken cancellationToken)
    {
        var statement = FindAncestor<ExpressionStatementSyntax>(root, span);
        if (statement is null || !CanAssignIgnoredValueToDiscard(statement, model, cancellationToken))
        {
            oldNode = null;
            return null;
        }

        oldNode = statement;
        return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName("_"),
                statement.Expression.WithoutTrivia()))
            .WithTriviaFrom(statement);
    }

    /// <summary>Returns whether a discard assignment can be emitted without binding to an existing underscore symbol.</summary>
    /// <param name="statement">The ignored expression statement.</param>
    /// <param name="model">The optional semantic model.</param>
    /// <param name="cancellationToken">A token that cancels semantic checks.</param>
    /// <returns><see langword="true"/> when <c>_</c> is provably a discard at the statement.</returns>
    private static bool CanAssignIgnoredValueToDiscard(
        ExpressionStatementSyntax statement,
        SemanticModel? model,
        CancellationToken cancellationToken)
    {
        if (IsDiscardAssignment(statement.Expression) || model is null)
        {
            return false;
        }

        foreach (var symbol in model.LookupSymbols(statement.SpanStart, name: "_"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (symbol.Name == "_")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression already assigns an ignored value to the discard.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression is an explicit discard assignment.</returns>
    private static bool IsDiscardAssignment(ExpressionSyntax expression)
        => expression is AssignmentExpressionSyntax
        {
            RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
            Left: IdentifierNameSyntax { Identifier.ValueText: "_" }
        };

    /// <summary>Creates a remove/rewrite edit for an overwritten local value.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The node to replace or remove.</param>
    /// <returns>The replacement node, or <see langword="null"/> for statement removal.</returns>
    private static LocalDeclarationStatementSyntax? CreateOverwrittenValueFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        if (FindAncestor<LocalDeclarationStatementSyntax>(root, span) is { } local
            && local.Declaration.Variables.Count == 1
            && local.Declaration.Variables[0].Initializer is not null)
        {
            oldNode = local;
            var variable = local.Declaration.Variables[0];
            variable = variable
                .WithIdentifier(variable.Identifier.WithTrailingTrivia())
                .WithInitializer(null);
            return local.WithDeclaration(local.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));
        }

        oldNode = FindAncestor<ExpressionStatementSyntax>(root, span);
        return null;
    }

    /// <summary>Creates a <c>??=</c> rewrite.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <returns>The replacement node.</returns>
    private static SyntaxNode? CreateCoalesceAssignmentFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        if (FindAncestor<IfStatementSyntax>(root, span) is { } ifStatement
            && TryGetEmbeddedAssignment(ifStatement.Statement, out var target, out var value))
        {
            oldNode = ifStatement;
            return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                    SyntaxKind.CoalesceAssignmentExpression,
                    target.WithoutTrivia(),
                    SyntaxFactory.Token(SyntaxKind.QuestionQuestionEqualsToken),
                    value.WithoutTrivia()))
                .WithTriviaFrom(ifStatement);
        }

        if (FindAncestor<BinaryExpressionSyntax>(root, span) is { } coalesce
            && coalesce.IsKind(SyntaxKind.CoalesceExpression)
            && ExpressionSimplificationAnalyzer.Unwrap(coalesce.Right) is AssignmentExpressionSyntax assignment)
        {
            oldNode = coalesce;
            return SyntaxFactory.AssignmentExpression(
                    SyntaxKind.CoalesceAssignmentExpression,
                    coalesce.Left.WithoutTrivia(),
                    SyntaxFactory.Token(SyntaxKind.QuestionQuestionEqualsToken),
                    assignment.Right.WithoutTrivia())
                .WithTriviaFrom(coalesce);
        }

        oldNode = null;
        return null;
    }

    /// <summary>Creates a tuple literal from an anonymous object.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The anonymous object to replace.</param>
    /// <returns>The tuple expression.</returns>
    private static TupleExpressionSyntax? CreateTupleLiteralFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        var anonymous = FindAncestor<AnonymousObjectCreationExpressionSyntax>(root, span);
        if (anonymous is null || anonymous.Initializers.Count == 0)
        {
            oldNode = null;
            return null;
        }

        var arguments = new List<ArgumentSyntax>(anonymous.Initializers.Count);
        for (var i = 0; i < anonymous.Initializers.Count; i++)
        {
            var initializer = anonymous.Initializers[i];
            if (!ModernSyntaxValueAnalyzer.TryGetTupleElement(initializer, out var name, out var expression))
            {
                oldNode = null;
                return null;
            }

            var argument = SyntaxFactory.Argument(expression.WithoutTrivia());
            if (initializer.NameEquals is not null || ExpressionSimplificationAnalyzer.InferredName(expression) != name)
            {
                argument = argument.WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(name)));
            }

            arguments.Add(argument);
        }

        oldNode = anonymous;
        return SyntaxFactory.TupleExpression(SyntaxFactory.SeparatedList(arguments)).WithTriviaFrom(anonymous);
    }

    /// <summary>Creates a foreach source cast with <c>System.Linq.Enumerable.Cast&lt;T&gt;</c>.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The source expression to replace.</param>
    /// <returns>The replacement expression.</returns>
    private static InvocationExpressionSyntax? CreateForeachCastFix(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        var foreachStatement = FindAncestor<ForEachStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (foreachStatement is null
            || !diagnostic.Properties.TryGetValue(ModernSyntaxValueAnalyzer.ElementTypeProperty, out var elementType)
            || string.IsNullOrWhiteSpace(elementType))
        {
            oldNode = null;
            return null;
        }

        oldNode = foreachStatement.Expression;
        return SyntaxFactory.ParseExpression($"System.Linq.Enumerable.Cast<{elementType}>({foreachStatement.Expression.WithoutTrivia()})")
            .WithTriviaFrom(foreachStatement.Expression) as InvocationExpressionSyntax;
    }

    /// <summary>Creates an added inner cast.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The original cast to replace.</param>
    /// <returns>The replacement cast.</returns>
    private static CastExpressionSyntax? CreateVisibleInnerCastFix(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode)
    {
        var cast = FindAncestor<CastExpressionSyntax>(root, diagnostic.Location.SourceSpan);
        if (cast is null
            || !diagnostic.Properties.TryGetValue(ModernSyntaxValueAnalyzer.TypeProperty, out var type)
            || string.IsNullOrWhiteSpace(type))
        {
            oldNode = null;
            return null;
        }

        oldNode = cast;
        return cast.WithExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName(type!), cast.Expression.WithoutTrivia()));
    }

    /// <summary>Creates a post-assignment null-check fold.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="oldNode">The assignment or declaration to replace.</param>
    /// <param name="removeNode">The null-check if statement to remove.</param>
    /// <returns>The replacement assignment or declaration.</returns>
    private static SyntaxNode? CreateFoldNullCheckFix(SyntaxNode root, Diagnostic diagnostic, out SyntaxNode? oldNode, out SyntaxNode? removeNode)
    {
        oldNode = null;
        removeNode = null;
        var ifStatement = FindAncestor<IfStatementSyntax>(root, diagnostic.Location.SourceSpan);
        if (ifStatement is null
            || !diagnostic.Properties.TryGetValue(ModernSyntaxValueAnalyzer.FoldKindProperty, out var foldKind)
            || !TryGetPreviousStatement(ifStatement, out var previous)
            || !TryGetFoldRight(ifStatement.Statement, foldKind, out var right))
        {
            return null;
        }

        removeNode = ifStatement;
        if (previous is LocalDeclarationStatementSyntax local
            && local.Declaration.Variables.Count == 1
            && local.Declaration.Variables[0] is { Initializer.Value: { } initializer } variable)
        {
            oldNode = local;
            var folded = SyntaxFactory.BinaryExpression(SyntaxKind.CoalesceExpression, initializer.WithoutTrivia(), right);
            return local.WithDeclaration(local.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable.WithInitializer(SyntaxFactory.EqualsValueClause(folded)))));
        }

        if (TryGetEmbeddedAssignment(previous, out var target, out var value))
        {
            oldNode = previous;
            var folded = SyntaxFactory.BinaryExpression(SyntaxKind.CoalesceExpression, value.WithoutTrivia(), right);
            return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    target.WithoutTrivia(),
                    folded))
                .WithTriviaFrom(previous);
        }

        return null;
    }

    /// <summary>Creates a local-function replacement for a delegate local initialized by a lambda.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The local declaration to replace.</param>
    /// <returns>The local function statement, or <see langword="null"/>.</returns>
    private static LocalFunctionStatementSyntax? CreateLocalFunctionFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var local = FindAncestor<LocalDeclarationStatementSyntax>(root, span);
        if (local is null
            || local.Declaration.Variables.Count != 1
            || local.Declaration.Variables[0] is not { Initializer.Value: LambdaExpressionSyntax lambda } variable
            || !TryGetFuncOrActionTypes(local.Declaration.Type, out var returnType, out var parameterTypes)
            || !TryBuildParameters(lambda, parameterTypes, out var parameters))
        {
            return null;
        }

        oldNode = local;
        return SyntaxFactory.LocalFunctionStatement(
                attributeLists: default,
                modifiers: default,
                returnType: returnType.WithoutTrivia(),
                identifier: SyntaxFactory.Identifier(variable.Identifier.ValueText),
                typeParameterList: null,
                parameterList: SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)),
                constraintClauses: default,
                body: lambda.Block,
                expressionBody: lambda.ExpressionBody is null ? null : SyntaxFactory.ArrowExpressionClause(lambda.ExpressionBody.WithoutTrivia()),
                semicolonToken: lambda.ExpressionBody is null ? default : SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithTriviaFrom(local);
    }

    /// <summary>Creates a collapsed <c>Where(predicate).Terminal()</c> invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The terminal invocation to replace.</param>
    /// <returns>The collapsed invocation.</returns>
    private static InvocationExpressionSyntax? CreateWhereTerminalFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = FindAncestor<InvocationExpressionSyntax>(root, span);
        if (invocation is not { ArgumentList.Arguments.Count: 0, Expression: MemberAccessExpressionSyntax outerAccess }
            || outerAccess.Expression is not InvocationExpressionSyntax whereInvocation
            || whereInvocation is not
            {
                ArgumentList.Arguments.Count: 1,
                Expression: MemberAccessExpressionSyntax { Expression: { } receiver }
            })
        {
            return null;
        }

        oldNode = invocation;
        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            receiver.WithoutTrivia(),
            outerAccess.Name.WithoutTrivia());
        return invocation
            .WithExpression(memberAccess)
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(whereInvocation.ArgumentList.Arguments[0].WithoutTrivia())))
            .WithTriviaFrom(invocation);
    }

    /// <summary>Creates a collapsed <c>OfType&lt;T&gt;</c> invocation.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The cast invocation to replace.</param>
    /// <returns>The collapsed invocation.</returns>
    private static InvocationExpressionSyntax? CreateTypeFilterFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = FindAncestor<InvocationExpressionSyntax>(root, span);
        if (invocation is not
            {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { TypeArgumentList: { } typeArguments },
                    Expression: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Expression: { } receiver }
                    }
                }
            })
        {
            return null;
        }

        oldNode = invocation;
        var ofTypeName = SyntaxFactory.GenericName(SyntaxFactory.Identifier("OfType")).WithTypeArgumentList(typeArguments.WithoutTrivia());
        var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver.WithoutTrivia(), ofTypeName);
        return invocation.WithExpression(memberAccess).WithTriviaFrom(invocation);
    }

    /// <summary>Creates a direct null-pattern replacement for a broad object pattern.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The pattern to replace.</param>
    /// <returns>The replacement pattern.</returns>
    private static SyntaxNode? CreateNullPatternFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var patternExpression = FindAncestor<IsPatternExpressionSyntax>(root, span);
        if (patternExpression is not null
            && ModernSyntaxValueAnalyzer.TryGetBroadObjectNullPattern(patternExpression.Pattern, out _, out var negated))
        {
            oldNode = patternExpression.Pattern;
            return CreateNullPattern(negated).WithTriviaFrom(patternExpression.Pattern);
        }

        var binary = FindAncestor<BinaryExpressionSyntax>(root, span);
        if (binary?.IsKind(SyntaxKind.IsExpression) != true)
        {
            return null;
        }

        oldNode = binary;
        return SyntaxFactory.IsPatternExpression(binary.Left.WithoutTrivia(), CreateNullPattern(negated: false)).WithTriviaFrom(binary);
    }

    /// <summary>Creates a null pattern, negated when required.</summary>
    /// <param name="negated">Whether to create <c>null</c> instead of <c>not null</c>.</param>
    /// <returns>The pattern syntax.</returns>
    private static PatternSyntax CreateNullPattern(bool negated)
    {
        var nullPattern = SyntaxFactory.ConstantPattern(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
        return negated
            ? nullPattern
            : SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword), nullPattern);
    }

    /// <summary>Creates a <c>nameof</c> invocation with omitted generic arguments.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <param name="oldNode">The invocation to replace.</param>
    /// <returns>The updated invocation.</returns>
    private static InvocationExpressionSyntax? CreateUnboundGenericNameFix(SyntaxNode root, TextSpan span, out SyntaxNode? oldNode)
    {
        oldNode = null;
        var invocation = FindAncestor<InvocationExpressionSyntax>(root, span);
        if (invocation is null || invocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var names = new List<GenericNameSyntax>();
        foreach (var node in invocation.ArgumentList.Arguments[0].DescendantNodesAndSelf())
        {
            if (node is GenericNameSyntax genericName && HasConcreteTypeArgument(genericName))
            {
                names.Add(genericName);
            }
        }

        if (names.Count == 0)
        {
            return null;
        }

        oldNode = invocation;
        return invocation.ReplaceNodes(names, static (_, current) => OmitGenericArguments(current));
    }

    /// <summary>Gets return and parameter type syntax for <c>Func</c> and <c>Action</c> delegate locals.</summary>
    /// <param name="type">The delegate type syntax.</param>
    /// <param name="returnType">The local-function return type.</param>
    /// <param name="parameterTypes">The local-function parameter types.</param>
    /// <returns><see langword="true"/> when the delegate has a supported built-in shape.</returns>
    private static bool TryGetFuncOrActionTypes(TypeSyntax type, out TypeSyntax returnType, out List<TypeSyntax> parameterTypes)
    {
        returnType = null!;
        parameterTypes = [];
        var genericName = GetGenericName(type);
        if (genericName is null)
        {
            return false;
        }

        var typeArguments = genericName.TypeArgumentList.Arguments;
        if (genericName.Identifier.ValueText == "Action")
        {
            returnType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
            for (var i = 0; i < typeArguments.Count; i++)
            {
                parameterTypes.Add(typeArguments[i]);
            }

            return true;
        }

        if (genericName.Identifier.ValueText != "Func" || typeArguments.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < typeArguments.Count - 1; i++)
        {
            parameterTypes.Add(typeArguments[i]);
        }

        returnType = typeArguments[typeArguments.Count - 1];
        return true;
    }

    /// <summary>Gets the final generic name from a simple or qualified type.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns>The generic name, or <see langword="null"/>.</returns>
    private static GenericNameSyntax? GetGenericName(TypeSyntax type)
        => type switch
        {
            GenericNameSyntax generic => generic,
            QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic,
            AliasQualifiedNameSyntax { Name: GenericNameSyntax generic } => generic,
            _ => null
        };

    /// <summary>Builds local-function parameters from lambda parameter names and delegate type arguments.</summary>
    /// <param name="lambda">The source lambda.</param>
    /// <param name="parameterTypes">The delegate parameter types.</param>
    /// <param name="parameters">The resulting local-function parameters.</param>
    /// <returns><see langword="true"/> when the lambda shape matches the delegate parameter count.</returns>
    private static bool TryBuildParameters(LambdaExpressionSyntax lambda, List<TypeSyntax> parameterTypes, out List<ParameterSyntax> parameters)
    {
        parameters = [];
        if (lambda is SimpleLambdaExpressionSyntax simple)
        {
            if (parameterTypes.Count != 1)
            {
                return false;
            }

            parameters.Add(SyntaxFactory.Parameter(simple.Parameter.Identifier).WithType(parameterTypes[0].WithoutTrivia()));
            return true;
        }

        if (lambda is not ParenthesizedLambdaExpressionSyntax parenthesized
            || parenthesized.ParameterList.Parameters.Count != parameterTypes.Count)
        {
            return false;
        }

        for (var i = 0; i < parameterTypes.Count; i++)
        {
            parameters.Add(SyntaxFactory.Parameter(parenthesized.ParameterList.Parameters[i].Identifier).WithType(parameterTypes[i].WithoutTrivia()));
        }

        return true;
    }

    /// <summary>Returns whether a generic name has at least one concrete type argument.</summary>
    /// <param name="genericName">The generic name.</param>
    /// <returns><see langword="true"/> when one argument is not omitted.</returns>
    private static bool HasConcreteTypeArgument(GenericNameSyntax genericName)
    {
        var arguments = genericName.TypeArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (!arguments[i].IsKind(SyntaxKind.OmittedTypeArgument))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Replaces concrete generic arguments with omitted generic arguments.</summary>
    /// <param name="genericName">The generic name.</param>
    /// <returns>The updated generic name.</returns>
    private static GenericNameSyntax OmitGenericArguments(GenericNameSyntax genericName)
    {
        var arguments = genericName.TypeArgumentList.Arguments;
        var omitted = new List<TypeSyntax>(arguments.Count);
        for (var i = 0; i < arguments.Count; i++)
        {
            omitted.Add(SyntaxFactory.OmittedTypeArgument());
        }

        return genericName.WithTypeArgumentList(genericName.TypeArgumentList.WithArguments(SyntaxFactory.SeparatedList(omitted)));
    }

    /// <summary>Finds the nearest ancestor of the requested type for a span.</summary>
    /// <typeparam name="T">The ancestor type.</typeparam>
    /// <param name="root">The syntax root.</param>
    /// <param name="span">The span.</param>
    /// <returns>The ancestor node, or <see langword="null"/>.</returns>
    private static T? FindAncestor<T>(SyntaxNode root, TextSpan span)
        where T : SyntaxNode
        => root.FindNode(span).FirstAncestorOrSelf<T>();

    /// <summary>Gets the previous statement in a block.</summary>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="previous">The previous statement.</param>
    /// <returns><see langword="true"/> when there is a previous statement.</returns>
    private static bool TryGetPreviousStatement(IfStatementSyntax ifStatement, out StatementSyntax previous)
    {
        previous = null!;
        if (ifStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        var statements = block.Statements;
        for (var i = 1; i < statements.Count; i++)
        {
            if (statements[i] == ifStatement)
            {
                previous = statements[i - 1];
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a simple assignment from a statement or single-statement block.</summary>
    /// <param name="statement">The statement.</param>
    /// <param name="target">The target.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when a simple assignment was found.</returns>
    private static bool TryGetEmbeddedAssignment(StatementSyntax statement, out ExpressionSyntax target, out ExpressionSyntax value)
    {
        target = null!;
        value = null!;
        var candidate = statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : statement;
        if (candidate is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: { } left,
                    Right: { } right
                }
            })
        {
            return false;
        }

        target = left;
        value = right;
        return true;
    }

    /// <summary>Gets the right operand for a folded null check.</summary>
    /// <param name="statement">The if body.</param>
    /// <param name="foldKind">The fold kind.</param>
    /// <param name="right">The coalesce right operand.</param>
    /// <returns><see langword="true"/> when the fold body was found.</returns>
    private static bool TryGetFoldRight(StatementSyntax statement, string? foldKind, out ExpressionSyntax right)
    {
        right = null!;
        var candidate = statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : statement;
        if (foldKind == ModernSyntaxValueAnalyzer.ThrowFold && candidate is ThrowStatementSyntax { Expression: { } thrown })
        {
            right = SyntaxFactory.ThrowExpression(thrown.WithoutTrivia());
            return true;
        }

        if (foldKind != ModernSyntaxValueAnalyzer.AssignmentFold
            || !TryGetEmbeddedAssignment(candidate, out _, out var value))
        {
            return false;
        }

        right = value.WithoutTrivia();
        return true;
    }

    /// <summary>Applies value-syntax fix-all batches, with a direct root rewrite for duplicate-heavy ignored-value fixes.</summary>
    private sealed class ModernSyntaxValueFixAllProvider : DocumentBasedFixAllProvider
    {
        /// <summary>The shared provider instance.</summary>
        public static readonly ModernSyntaxValueFixAllProvider Instance = new();

        /// <inheritdoc/>
        protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            if (diagnostics.IsEmpty || fixAllContext.CodeFixProvider is not IBatchFixableCodeFix fix)
            {
                return document;
            }

            var root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            if (AllDiagnosticsAreIgnoredValue(diagnostics))
            {
                var model = await document.GetSemanticModelAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
                return FixIgnoredValues(document, root, model, diagnostics, fixAllContext.CancellationToken);
            }

            var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
            foreach (var diagnostic in BatchEditFixAllProvider.UniqueDiagnostics(editor.OriginalRoot, fix, diagnostics))
            {
                BatchEditFixAllProvider.RegisterBatchEdit(editor, fix, diagnostic);
            }

            return editor.GetChangedDocument();
        }

        /// <summary>Returns whether every diagnostic is for the ignored expression value rule.</summary>
        /// <param name="diagnostics">The diagnostics to inspect.</param>
        /// <returns><see langword="true"/> when every diagnostic has id SST2221.</returns>
        private static bool AllDiagnosticsAreIgnoredValue(ImmutableArray<Diagnostic> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Id != ModernSyntaxRules.MakeIgnoredExpressionValueExplicit.Id)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Applies ignored-value fixes directly against the original syntax root.</summary>
        /// <param name="document">The document to update.</param>
        /// <param name="root">The original syntax root.</param>
        /// <param name="model">The optional semantic model.</param>
        /// <param name="diagnostics">The diagnostics to fix.</param>
        /// <param name="cancellationToken">A token that cancels semantic checks.</param>
        /// <returns>The updated document.</returns>
        private static Document FixIgnoredValues(
            Document document,
            SyntaxNode root,
            SemanticModel? model,
            ImmutableArray<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            var seen = new HashSet<TextSpan>();
            var targets = new List<SyntaxNode>();
            var replacements = new Dictionary<TextSpan, SyntaxNode>();
            foreach (var diagnostic in diagnostics)
            {
                var replacement = CreateIgnoredValueFix(root, model, diagnostic.Location.SourceSpan, out var oldNode, cancellationToken);
                if (replacement is null || oldNode is null || !seen.Add(oldNode.Span))
                {
                    continue;
                }

                targets.Add(oldNode);
                replacements.Add(oldNode.Span, replacement);
            }

            if (targets.Count == 0)
            {
                return document;
            }

            var updatedRoot = root.ReplaceNodes(
                targets,
                (original, _) => replacements[original.Span]);
            return document.WithSyntaxRoot(updatedRoot);
        }
    }
}
