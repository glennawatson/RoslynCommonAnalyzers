// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Recognizes the while loop whose counter is declared immediately above it, tested by its condition,
/// stepped by its last statement, and dead afterwards — the loop a <c>for</c> header states in one line.
/// Shared by the analyzer and its code fix so the two agree on what folds.
/// </summary>
internal static class WhileLoopCounter
{
    /// <summary>Matches a while statement against the counter-owning shape.</summary>
    /// <param name="loop">The while statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="parts">The declaration, the step, and the counter's name.</param>
    /// <returns><see langword="true"/> when the loop can become a for loop.</returns>
    public static bool TryMatch(
        WhileStatementSyntax loop,
        SemanticModel model,
        CancellationToken cancellationToken,
        out WhileLoopCounterParts parts)
    {
        parts = default;
        if (loop is not { Statement: BlockSyntax { Statements.Count: > 1 } body, Parent: BlockSyntax enclosing })
        {
            return false;
        }

        var (declaration, declarator) = TryGetCounterDeclarator(enclosing, loop);
        if (declarator is null)
        {
            return false;
        }

        var name = declarator.Identifier.ValueText;
        if (!HasTrailingStep(body, name, out var step)
            || !MentionsIdentifier(loop.Condition, name)
            || HasContinueTargetingLoop(body))
        {
            return false;
        }

        if (model.GetDeclaredSymbol(declarator, cancellationToken) is not ILocalSymbol counter
            || IsReadAfter(enclosing, loop, counter, model))
        {
            return false;
        }

        parts = new WhileLoopCounterParts(declaration!, step!, name);
        return true;
    }

    /// <summary>Gets the single initialized local declared immediately above the loop.</summary>
    /// <param name="enclosing">The block holding both statements.</param>
    /// <param name="loop">The while statement.</param>
    /// <returns>The declaration statement and its one declarator, or nulls when the shape does not match.</returns>
    private static (LocalDeclarationStatementSyntax? Declaration, VariableDeclaratorSyntax? Declarator) TryGetCounterDeclarator(
        BlockSyntax enclosing,
        WhileStatementSyntax loop)
    {
        if (TryGetPrecedingDeclaration(enclosing, loop) is not { Declaration.Variables: { Count: 1 } variables } declaration
            || variables[0].Initializer is null)
        {
            return (null, null);
        }

        return (declaration, variables[0]);
    }

    /// <summary>Gets the loop body's last statement when it steps the counter.</summary>
    /// <param name="body">The loop body.</param>
    /// <param name="name">The counter's name.</param>
    /// <param name="step">The stepping statement.</param>
    /// <returns><see langword="true"/> when the body ends by stepping the counter.</returns>
    private static bool HasTrailingStep(BlockSyntax body, string name, out ExpressionStatementSyntax? step)
    {
        step = body.Statements[body.Statements.Count - 1] as ExpressionStatementSyntax;
        return step is not null && StepsCounter(step.Expression, name);
    }

    /// <summary>Gets the local declaration written immediately above the loop.</summary>
    /// <param name="enclosing">The block holding both statements.</param>
    /// <param name="loop">The while statement.</param>
    /// <returns>The preceding declaration, or <see langword="null"/> when the loop is not preceded by one.</returns>
    private static LocalDeclarationStatementSyntax? TryGetPrecedingDeclaration(BlockSyntax enclosing, WhileStatementSyntax loop)
    {
        var statements = enclosing.Statements;
        for (var i = 1; i < statements.Count; i++)
        {
            if (ReferenceEquals(statements[i], loop))
            {
                return statements[i - 1] as LocalDeclarationStatementSyntax;
            }
        }

        return null;
    }

    /// <summary>Returns whether an expression steps the named counter and nothing else.</summary>
    /// <param name="expression">The last statement's expression.</param>
    /// <param name="name">The counter's name.</param>
    /// <returns><see langword="true"/> for <c>i++</c>, <c>++i</c>, <c>i--</c>, <c>--i</c>, and a compound assignment to <c>i</c>.</returns>
    private static bool StepsCounter(ExpressionSyntax expression, string name) => expression switch
    {
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression } postfix
            => IsNamed(postfix.Operand, name),
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression } prefix
            => IsNamed(prefix.Operand, name),
        AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression or (int)SyntaxKind.SubtractAssignmentExpression } assignment
            => IsNamed(assignment.Left, name) && !MentionsIdentifier(assignment.Right, name),
        _ => false,
    };

    /// <summary>Returns whether an expression is exactly the named identifier.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="name">The counter's name.</param>
    /// <returns><see langword="true"/> when the expression names the counter.</returns>
    private static bool IsNamed(ExpressionSyntax expression, string name)
        => expression is IdentifierNameSyntax identifier && string.Equals(identifier.Identifier.ValueText, name, StringComparison.Ordinal);

    /// <summary>Returns whether an expression mentions the named identifier anywhere.</summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="name">The counter's name.</param>
    /// <returns><see langword="true"/> when the name appears.</returns>
    private static bool MentionsIdentifier(ExpressionSyntax expression, string name)
    {
        if (IsNamed(expression, name))
        {
            return true;
        }

        var state = (Name: name, Found: false);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, (string Name, bool Found)>(
            expression,
            ref state,
            static (node, ref current) =>
            {
                if (!string.Equals(node.Identifier.ValueText, current.Name, StringComparison.Ordinal))
                {
                    return true;
                }

                current.Found = true;
                return false;
            });

        return state.Found;
    }

    /// <summary>Returns whether the body holds a <c>continue</c> that targets this loop.</summary>
    /// <param name="body">The loop body.</param>
    /// <returns><see langword="true"/> when a <c>continue</c> would skip the trailing step.</returns>
    /// <remarks>
    /// The scan stops at a nested loop, a lambda, and a local function, because a <c>continue</c> inside one
    /// of those belongs to that construct and never reaches this loop's step.
    /// </remarks>
    private static bool HasContinueTargetingLoop(SyntaxNode body)
    {
        foreach (var child in body.ChildNodes())
        {
            if (child is ContinueStatementSyntax)
            {
                return true;
            }

            if (OwnsItsOwnContinue(child))
            {
                continue;
            }

            if (HasContinueTargetingLoop(child))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node captures <c>continue</c> statements written inside it.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a nested loop or a nested function body.</returns>
    private static bool OwnsItsOwnContinue(SyntaxNode node)
        => node is ForStatementSyntax
            or ForEachStatementSyntax
            or ForEachVariableStatementSyntax
            or WhileStatementSyntax
            or DoStatementSyntax
            or AnonymousFunctionExpressionSyntax
            or LocalFunctionStatementSyntax;

    /// <summary>Returns whether the counter is still read or written after the loop.</summary>
    /// <param name="enclosing">The block holding the loop.</param>
    /// <param name="loop">The while statement.</param>
    /// <param name="counter">The counter symbol.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns><see langword="true"/> when anything after the loop touches the counter.</returns>
    private static bool IsReadAfter(BlockSyntax enclosing, WhileStatementSyntax loop, ILocalSymbol counter, SemanticModel model)
    {
        var statements = enclosing.Statements;
        var index = statements.IndexOf(loop);
        if (index < 0 || index == statements.Count - 1)
        {
            return false;
        }

        // A local can only be reached by writing its name, so if the name never appears after the loop the
        // counter is dead there and the flow analysis — by far the most expensive thing this rule could do —
        // is skipped. The name appearing does not prove it is the same symbol, so that case still asks.
        if (!MentionsIdentifierInRange(statements, index + 1, counter.Name))
        {
            return false;
        }

        var flow = model.AnalyzeDataFlow(statements[index + 1], statements[statements.Count - 1]);
        if (flow is not { Succeeded: true })
        {
            return true;
        }

        return Contains(flow.ReadInside, counter) || Contains(flow.WrittenInside, counter);
    }

    /// <summary>Returns whether a name is written anywhere in a trailing run of statements.</summary>
    /// <param name="statements">The enclosing block's statements.</param>
    /// <param name="start">The first statement to scan.</param>
    /// <param name="name">The counter's name.</param>
    /// <returns><see langword="true"/> when the name appears.</returns>
    private static bool MentionsIdentifierInRange(SyntaxList<StatementSyntax> statements, int start, string name)
    {
        for (var i = start; i < statements.Count; i++)
        {
            var state = (Name: name, Found: false);
            DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, (string Name, bool Found)>(
                statements[i],
                ref state,
                static (node, ref current) =>
                {
                    if (!string.Equals(node.Identifier.ValueText, current.Name, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    current.Found = true;
                    return false;
                });

            if (state.Found)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a data-flow symbol set holds the counter.</summary>
    /// <param name="symbols">The symbol set.</param>
    /// <param name="counter">The counter symbol.</param>
    /// <returns><see langword="true"/> when the counter is present.</returns>
    private static bool Contains(ImmutableArray<ISymbol> symbols, ILocalSymbol counter)
    {
        for (var i = 0; i < symbols.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(symbols[i], counter))
            {
                return true;
            }
        }

        return false;
    }
}
