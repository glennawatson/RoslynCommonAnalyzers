// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a null guard that throws, immediately followed by assigning the guarded value to a field,
/// property, or local (SST2283): <c>if (x is null) throw new SomeException(); _x = x;</c> folds into
/// <c>_x = x ?? throw new SomeException();</c>. Reported only when the guarded value is a side-effect-free
/// local or parameter of a reference type, the throw carries an expression, the assignment target is a
/// simple name or <c>this</c> member, and nothing sits between the guard and the assignment. The
/// guard-then-return shape and the argument-null guard whose throw is better written as a runtime
/// null-check helper are left to the rules that own them, so this rule never fires on a guard another covers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2283FoldGuardIntoAssignedValueAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArrays.Of(ModernSyntaxRules.FoldGuardIntoAssignedValue);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>
    /// Matches the guard-then-assignment shape and yields its parts, so the analyzer and the code fix
    /// re-derive one structure. When <paramref name="argumentNullFolded"/> is set, an argument-null guard
    /// that a runtime null-check helper already replaces is excluded, so the two rules never both fire.
    /// </summary>
    /// <param name="ifStatement">The candidate guard <c>if</c>.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="argumentNullFolded">Whether the runtime argument-null helper exists in this compilation.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="guardedValue">The guarded value identifier when matched.</param>
    /// <param name="throwOperand">The thrown expression (the operand of <c>throw</c>) when matched.</param>
    /// <param name="assignmentStatement">The following assignment statement when matched.</param>
    /// <returns><see langword="true"/> when the guard folds into the following assignment.</returns>
    internal static bool TryGetFold(
        IfStatementSyntax ifStatement,
        SemanticModel model,
        bool argumentNullFolded,
        CancellationToken cancellationToken,
        out ExpressionSyntax guardedValue,
        out ExpressionSyntax throwOperand,
        out ExpressionStatementSyntax assignmentStatement)
    {
        guardedValue = null!;
        throwOperand = null!;
        assignmentStatement = null!;

        if (!TryMatchGuardShape(ifStatement, argumentNullFolded, out var checkedIdentifier, out var thrown, out var nextStatement)
            || !IsFoldableGuardedValue(ifStatement.Condition, checkedIdentifier, model, cancellationToken))
        {
            return false;
        }

        guardedValue = checkedIdentifier;
        throwOperand = thrown;
        assignmentStatement = nextStatement;
        return true;
    }

    /// <summary>Registers the argument-null-helper probe, then analyzes every <c>if</c> statement.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var argumentNullFolded = HasStaticThrowIfNull(context.Compilation.GetTypeByMetadataName("System.ArgumentNullException"));
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, argumentNullFolded), SyntaxKind.IfStatement);
    }

    /// <summary>Reports a foldable guard-then-assignment shape.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="argumentNullFolded">Whether the runtime argument-null helper exists in this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, bool argumentNullFolded)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (!TryGetFold(ifStatement, context.SemanticModel, argumentNullFolded, context.CancellationToken, out _, out _, out _))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.FoldGuardIntoAssignedValue, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Runs the purely syntactic prepass, including the argument-null-guard stand-down.</summary>
    /// <param name="ifStatement">The candidate guard <c>if</c>.</param>
    /// <param name="argumentNullFolded">Whether the runtime argument-null helper exists in this compilation.</param>
    /// <param name="checkedIdentifier">The guarded identifier when matched.</param>
    /// <param name="thrown">The thrown expression when matched.</param>
    /// <param name="assignmentStatement">The following assignment statement when matched.</param>
    /// <returns><see langword="true"/> when the syntactic shape matches and no other rule owns the guard.</returns>
    private static bool TryMatchGuardShape(
        IfStatementSyntax ifStatement,
        bool argumentNullFolded,
        out IdentifierNameSyntax checkedIdentifier,
        out ExpressionSyntax thrown,
        out ExpressionStatementSyntax assignmentStatement)
    {
        checkedIdentifier = null!;
        thrown = null!;
        assignmentStatement = null!;

        if (ifStatement.Else is not null
            || HasNonWhitespaceTrivia(ifStatement)
            || !SupportsThrowExpression(ifStatement)
            || !TryGetNullCheckedIdentifier(ifStatement.Condition, out checkedIdentifier)
            || !TryGetSingleThrowOperand(ifStatement.Statement, out thrown)
            || !TryGetNextAssignment(ifStatement, checkedIdentifier, out assignmentStatement))
        {
            return false;
        }

        // Stand down where the argument-null-guard rule already reports this exact guard, so the two never
        // both fire on one guard.
        return !(argumentNullFolded && ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out _));
    }

    /// <summary>Confirms the guarded value can fold into a coalescing throw without changing behavior.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <param name="checkedIdentifier">The guarded identifier.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> for a side-effect-free reference-type local or parameter under a built-in null check.</returns>
    private static bool IsFoldableGuardedValue(
        ExpressionSyntax condition,
        IdentifierNameSyntax checkedIdentifier,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // A user-defined '==' is not the reference null check '?? throw' performs, so leave it alone.
        var unwrapped = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (unwrapped is BinaryExpressionSyntax
            && model.GetSymbolInfo(unwrapped, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
        {
            return false;
        }

        // The value is read in the guard and again in the assignment, so it must be side-effect-free — a
        // local or parameter is — and a reference type keeps the coalescing throw legal and identical.
        return model.GetSymbolInfo(checkedIdentifier, cancellationToken).Symbol is ILocalSymbol or IParameterSymbol
            && model.GetTypeInfo(checkedIdentifier, cancellationToken).Type is { IsReferenceType: true };
    }

    /// <summary>Returns whether a type declares a static <c>ThrowIfNull</c> method.</summary>
    /// <param name="type">The resolved <c>ArgumentNullException</c> type, when available.</param>
    /// <returns><see langword="true"/> when the runtime null-check helper exists.</returns>
    private static bool HasStaticThrowIfNull(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        var members = type.GetMembers("ThrowIfNull");
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the identifier checked against <see langword="null"/> by the guard condition.</summary>
    /// <param name="condition">The guard condition.</param>
    /// <param name="identifier">The guarded identifier when matched.</param>
    /// <returns><see langword="true"/> for <c>x is null</c>, <c>x == null</c>, or <c>null == x</c>.</returns>
    private static bool TryGetNullCheckedIdentifier(ExpressionSyntax condition, out IdentifierNameSyntax identifier)
    {
        identifier = null!;
        condition = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (condition is IsPatternExpressionSyntax
            {
                Expression: IdentifierNameSyntax patternIdentifier,
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression },
            })
        {
            identifier = patternIdentifier;
            return true;
        }

        if (condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary
            || NonNullEqualityOperand(binary) is not IdentifierNameSyntax equalityIdentifier)
        {
            return false;
        }

        identifier = equalityIdentifier;
        return true;
    }

    /// <summary>Gets the non-null side of an equality whose other side is the <c>null</c> literal.</summary>
    /// <param name="binary">The equality expression.</param>
    /// <returns>The non-null operand, or <see langword="null"/> when neither side is the <c>null</c> literal.</returns>
    private static ExpressionSyntax? NonNullEqualityOperand(BinaryExpressionSyntax binary)
    {
        var left = ExpressionSimplificationAnalyzer.Unwrap(binary.Left);
        var right = ExpressionSimplificationAnalyzer.Unwrap(binary.Right);
        if (right.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return left;
        }

        return left.IsKind(SyntaxKind.NullLiteralExpression) ? right : null;
    }

    /// <summary>Gets the thrown expression when the guard body is a single <c>throw new ...</c>.</summary>
    /// <param name="statement">The guard body.</param>
    /// <param name="thrown">The thrown expression when matched.</param>
    /// <returns><see langword="true"/> for a lone throw carrying an expression (not a rethrow).</returns>
    private static bool TryGetSingleThrowOperand(StatementSyntax statement, out ExpressionSyntax thrown)
    {
        thrown = null!;
        var throwStatement = statement switch
        {
            ThrowStatementSyntax direct => direct,
            BlockSyntax { Statements: [ThrowStatementSyntax single] } => single,
            _ => null,
        };

        if (throwStatement?.Expression is not { } expression)
        {
            return false;
        }

        thrown = expression;
        return true;
    }

    /// <summary>Gets the immediately following statement when it assigns the guarded value to a safe target.</summary>
    /// <param name="ifStatement">The guard <c>if</c>.</param>
    /// <param name="checkedIdentifier">The guarded identifier.</param>
    /// <param name="assignmentStatement">The following assignment statement when matched.</param>
    /// <returns><see langword="true"/> when the next statement is <c>target = checkedIdentifier;</c>.</returns>
    private static bool TryGetNextAssignment(
        IfStatementSyntax ifStatement,
        IdentifierNameSyntax checkedIdentifier,
        out ExpressionStatementSyntax assignmentStatement)
    {
        assignmentStatement = null!;
        if (ifStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        var statements = block.Statements;
        var index = statements.IndexOf(ifStatement);
        if (index < 0 || index + 1 >= statements.Count)
        {
            return false;
        }

        if (statements[index + 1] is not ExpressionStatementSyntax candidate
            || candidate.Expression is not AssignmentExpressionSyntax
            {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                Left: { } target,
                Right: { } right
            })
        {
            return false;
        }

        if (!IsSafeAssignmentTarget(target)
            || !SyntaxFactory.AreEquivalent(ExpressionSimplificationAnalyzer.Unwrap(right), checkedIdentifier))
        {
            return false;
        }

        assignmentStatement = candidate;
        return true;
    }

    /// <summary>Returns whether an assignment target has no receiver of its own to evaluate before the throw.</summary>
    /// <param name="target">The assignment target.</param>
    /// <returns><see langword="true"/> for a simple name or a <c>this</c> member access.</returns>
    /// <remarks>
    /// The fold evaluates the target before the coalescing throw, so a target with its own receiver
    /// — <c>obj.Member = x</c> — could throw on that receiver before the guard runs, changing behavior.
    /// A bare name or a <c>this</c> member has only a trivial receiver, so the order is preserved.
    /// </remarks>
    private static bool IsSafeAssignmentTarget(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } => true,
        _ => false,
    };

    /// <summary>Returns whether the leading or trailing trivia carries a comment the fold would move.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when non-whitespace trivia is present.</returns>
    private static bool HasNonWhitespaceTrivia(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        foreach (var trivia in node.GetTrailingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the tree's language version supports throw expressions (C# 7).</summary>
    /// <param name="node">A node in the tree.</param>
    /// <returns><see langword="true"/> when throw expressions are available.</returns>
    private static bool SupportsThrowExpression(SyntaxNode node)
        => node.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7 };
}
