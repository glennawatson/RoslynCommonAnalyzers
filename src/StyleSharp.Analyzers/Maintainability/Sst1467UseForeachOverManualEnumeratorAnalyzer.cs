// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a while loop that drives an enumerator by hand (SST1467): a plain local initialized from a
/// zero-argument <c>GetEnumerator()</c> call, immediately followed by a <c>while</c> on that local's
/// <c>MoveNext()</c> whose body only reads <c>Current</c>, where the enumerator is never used after the
/// loop. The check is purely syntactic and abandons the pattern on the first use a foreach statement
/// could not express, so the paired code fix is always a safe mechanical rewrite.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1467UseForeachOverManualEnumeratorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Cached visitor that validates every enumerator use inside the loop body.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<SyntaxNode, BodyScanState> BodyVisitor = VisitBodyNode;

    /// <summary>Cached visitor that finds an enumerator use in a statement after the loop.</summary>
    private static readonly DescendantTraversalHelper.DescendantVisitor<IdentifierNameSyntax, LaterUseState> LaterUseVisitor = VisitLaterUse;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UseForeachOverManualEnumerator);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.WhileStatement);
    }

    /// <summary>Extracts the enumerator name from a condition of exactly the form <c>id.MoveNext()</c>.</summary>
    /// <param name="whileStatement">The while statement.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <returns><see langword="true"/> when the condition has the required shape.</returns>
    internal static bool TryGetEnumeratorName(WhileStatementSyntax whileStatement, out string name)
    {
        name = string.Empty;
        if (whileStatement.Condition is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation
            || invocation.Expression is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } memberAccess
            || memberAccess.Expression is not IdentifierNameSyntax receiver
            || memberAccess.Name is not IdentifierNameSyntax { Identifier.ValueText: "MoveNext" })
        {
            return false;
        }

        name = receiver.Identifier.ValueText;
        return true;
    }

    /// <summary>Finds the enumerator declaration as the statement immediately before the loop.</summary>
    /// <param name="whileStatement">The while statement.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <param name="declaration">The matching declaration statement.</param>
    /// <param name="source">The expression whose <c>GetEnumerator()</c> initialized the local.</param>
    /// <returns><see langword="true"/> when the previous sibling declares the enumerator from a zero-argument <c>GetEnumerator()</c> call.</returns>
    internal static bool TryGetEnumeratorDeclaration(
        WhileStatementSyntax whileStatement,
        string name,
        out LocalDeclarationStatementSyntax? declaration,
        out ExpressionSyntax? source)
    {
        declaration = null;
        source = null;
        if (!TryGetContainingStatements(whileStatement, out var statements))
        {
            return false;
        }

        var index = statements.IndexOf(whileStatement);
        if (index < 1 || !TryGetPlainSingleVariableDeclaration(statements[index - 1], out var candidate))
        {
            return false;
        }

        if (!TryGetGetEnumeratorReceiver(candidate!.Declaration.Variables[0], name, out source))
        {
            return false;
        }

        declaration = candidate;
        return true;
    }

    /// <summary>Returns whether every use of the enumerator inside the loop body is a plain <c>Current</c> read.</summary>
    /// <param name="whileStatement">The while statement.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <returns><see langword="true"/> when the body never uses the enumerator for anything a foreach cannot express.</returns>
    internal static bool HasForeachCompatibleBody(WhileStatementSyntax whileStatement, string name)
    {
        var state = new BodyScanState(name, Valid: true);
        DescendantTraversalHelper.VisitDescendants(whileStatement.Statement, ref state, BodyVisitor);
        return state.Valid;
    }

    /// <summary>Returns whether the enumerator name appears in any statement after the loop in the enclosing block.</summary>
    /// <param name="whileStatement">The while statement.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <returns><see langword="true"/> when a later sibling statement mentions the name.</returns>
    internal static bool IsEnumeratorUsedAfterLoop(WhileStatementSyntax whileStatement, string name)
    {
        if (!TryGetContainingStatements(whileStatement, out var statements))
        {
            return true;
        }

        var index = statements.IndexOf(whileStatement);
        for (var i = index + 1; i < statements.Count; i++)
        {
            var state = new LaterUseState(name, Found: false);
            DescendantTraversalHelper.VisitDescendants(statements[i], ref state, LaterUseVisitor);
            if (state.Found)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports while loops that restate the foreach pattern over a hand-driven enumerator.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var whileStatement = (WhileStatementSyntax)context.Node;
        if (!TryGetEnumeratorName(whileStatement, out var name)
            || !TryGetEnumeratorDeclaration(whileStatement, name, out _, out _)
            || !HasForeachCompatibleBody(whileStatement, name)
            || IsEnumeratorUsedAfterLoop(whileStatement, name))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.UseForeachOverManualEnumerator, whileStatement.WhileKeyword.GetLocation()));
    }

    /// <summary>Gets the sibling statement list that contains a statement.</summary>
    /// <param name="statement">The statement whose siblings to find.</param>
    /// <param name="statements">The containing statement list.</param>
    /// <returns><see langword="true"/> when the statement sits directly in a block or switch section.</returns>
    private static bool TryGetContainingStatements(StatementSyntax statement, out SyntaxList<StatementSyntax> statements)
    {
        switch (statement.Parent)
        {
            case BlockSyntax block:
            {
                statements = block.Statements;
                return true;
            }

            case SwitchSectionSyntax section:
            {
                statements = section.Statements;
                return true;
            }

            default:
            {
                statements = default;
                return false;
            }
        }
    }

    /// <summary>Returns whether a statement is a plain single-variable local declaration with no <c>using</c> keyword or modifiers.</summary>
    /// <param name="statement">The candidate statement.</param>
    /// <param name="declaration">The declaration when the shape matches.</param>
    /// <returns><see langword="true"/> for a plain one-variable declaration statement.</returns>
    private static bool TryGetPlainSingleVariableDeclaration(StatementSyntax statement, out LocalDeclarationStatementSyntax? declaration)
    {
        declaration = null;
        if (statement is not LocalDeclarationStatementSyntax candidate
            || !candidate.UsingKeyword.IsKind(SyntaxKind.None)
            || candidate.Modifiers.Count != 0
            || candidate.Declaration.Variables.Count != 1)
        {
            return false;
        }

        declaration = candidate;
        return true;
    }

    /// <summary>Extracts the receiver of a variable's zero-argument <c>GetEnumerator()</c> initializer.</summary>
    /// <param name="variable">The declared variable.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <param name="source">The receiver whose <c>GetEnumerator()</c> call initialized the variable.</param>
    /// <returns><see langword="true"/> when the variable matches the name and the initializer has the required shape.</returns>
    private static bool TryGetGetEnumeratorReceiver(VariableDeclaratorSyntax variable, string name, out ExpressionSyntax? source)
    {
        source = null;
        if (!string.Equals(variable.Identifier.ValueText, name, StringComparison.Ordinal)
            || variable.Initializer is not { Value: InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } initializer }
            || initializer.Expression is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } memberAccess
            || memberAccess.Name is not IdentifierNameSyntax { Identifier.ValueText: "GetEnumerator" })
        {
            return false;
        }

        source = memberAccess.Expression;
        return true;
    }

    /// <summary>Validates one body node: enumerator mentions must be <c>Current</c> reads and the name must not be redeclared.</summary>
    /// <param name="node">The visited syntax node.</param>
    /// <param name="state">The body scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once the body is known incompatible.</returns>
    private static bool VisitBodyNode(SyntaxNode node, ref BodyScanState state)
    {
        if (node is IdentifierNameSyntax identifier)
        {
            if (string.Equals(identifier.Identifier.ValueText, state.Name, StringComparison.Ordinal) && !IsCurrentReadAccess(identifier))
            {
                state = state with { Valid = false };
                return false;
            }

            return true;
        }

        if (!DeclaresName(node, state.Name))
        {
            return true;
        }

        state = state with { Valid = false };
        return false;
    }

    /// <summary>Returns whether an identifier is the receiver of a read-only <c>Current</c> member access.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns><see langword="true"/> for <c>id.Current</c> reads.</returns>
    private static bool IsCurrentReadAccess(IdentifierNameSyntax identifier)
        => identifier.Parent is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } memberAccess
            && memberAccess.Expression == identifier
            && memberAccess.Name is IdentifierNameSyntax { Identifier.ValueText: "Current" }
            && IsReadOnlyUse(memberAccess);

    /// <summary>Returns whether a <c>Current</c> access is only read, never written or aliased.</summary>
    /// <param name="memberAccess">The <c>Current</c> member access.</param>
    /// <returns><see langword="true"/> when the access can be replaced by a foreach iteration variable.</returns>
    private static bool IsReadOnlyUse(MemberAccessExpressionSyntax memberAccess)
    {
        if (IsAssignmentTarget(memberAccess))
        {
            return false;
        }

        return memberAccess.Parent switch
        {
            PostfixUnaryExpressionSyntax postfix => IsReadOnlyPostfixUse(postfix),
            PrefixUnaryExpressionSyntax prefix => IsReadOnlyPrefixUse(prefix),
            ArgumentSyntax argument => IsByValueArgument(argument),
            RefExpressionSyntax => false,
            _ => true
        };
    }

    /// <summary>Returns whether a postfix operator leaves its <c>Current</c> operand unmodified.</summary>
    /// <param name="postfix">The postfix expression whose operand is the access.</param>
    /// <returns><see langword="true"/> unless the operator is an increment or decrement.</returns>
    private static bool IsReadOnlyPostfixUse(PostfixUnaryExpressionSyntax postfix)
        => !postfix.IsKind(SyntaxKind.PostIncrementExpression) && !postfix.IsKind(SyntaxKind.PostDecrementExpression);

    /// <summary>Returns whether a prefix operator leaves its <c>Current</c> operand unmodified and unaliased.</summary>
    /// <param name="prefix">The prefix expression whose operand is the access.</param>
    /// <returns><see langword="true"/> unless the operator is an increment, a decrement, or address-of.</returns>
    private static bool IsReadOnlyPrefixUse(PrefixUnaryExpressionSyntax prefix)
        => !prefix.IsKind(SyntaxKind.PreIncrementExpression)
            && !prefix.IsKind(SyntaxKind.PreDecrementExpression)
            && !prefix.IsKind(SyntaxKind.AddressOfExpression);

    /// <summary>Returns whether an argument passes its <c>Current</c> expression by value rather than by <c>ref</c>/<c>out</c>.</summary>
    /// <param name="argument">The argument carrying the access.</param>
    /// <returns><see langword="true"/> for a plain by-value argument.</returns>
    private static bool IsByValueArgument(ArgumentSyntax argument)
        => !argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword) && !argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword);

    /// <summary>Returns whether an expression is an assignment target, directly or through tuple deconstruction.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression is written to.</returns>
    private static bool IsAssignmentTarget(ExpressionSyntax expression)
    {
        SyntaxNode node = expression;
        while (node.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax tuple })
        {
            node = tuple;
        }

        return node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node;
    }

    /// <summary>Returns whether a node declares the enumerator name inside the loop body.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <returns><see langword="true"/> when the node redeclares the name.</returns>
    private static bool DeclaresName(SyntaxNode node, string name)
        => node switch
        {
            ParameterSyntax parameter => Matches(parameter.Identifier, name),
            VariableDeclaratorSyntax variable => Matches(variable.Identifier, name),
            ForEachStatementSyntax forEach => Matches(forEach.Identifier, name),
            CatchDeclarationSyntax catchDeclaration => Matches(catchDeclaration.Identifier, name),
            SingleVariableDesignationSyntax designation => Matches(designation.Identifier, name),
            LocalFunctionStatementSyntax localFunction => Matches(localFunction.Identifier, name),
            _ => false
        };

    /// <summary>Returns whether an identifier token carries the enumerator name.</summary>
    /// <param name="identifier">The identifier token.</param>
    /// <param name="name">The enumerator local's name.</param>
    /// <returns><see langword="true"/> when the token matches.</returns>
    private static bool Matches(SyntaxToken identifier, string name)
        => identifier.RawKind != 0 && string.Equals(identifier.ValueText, name, StringComparison.Ordinal);

    /// <summary>Records an enumerator use after the loop.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The later-use search state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a use is found.</returns>
    private static bool VisitLaterUse(IdentifierNameSyntax identifier, ref LaterUseState state)
    {
        if (!string.Equals(identifier.Identifier.ValueText, state.Name, StringComparison.Ordinal))
        {
            return true;
        }

        state = state with { Found = true };
        return false;
    }

    /// <summary>Tracks whether the loop body has stayed foreach-compatible.</summary>
    /// <param name="Name">The enumerator local's name.</param>
    /// <param name="Valid">Whether every use seen so far is a <c>Current</c> read.</param>
    private readonly record struct BodyScanState(string Name, bool Valid);

    /// <summary>Tracks the search for an enumerator use after the loop.</summary>
    /// <param name="Name">The enumerator local's name.</param>
    /// <param name="Found">Whether a later use was found.</param>
    private readonly record struct LaterUseState(string Name, bool Found);
}
