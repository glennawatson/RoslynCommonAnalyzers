// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags the racy bool once-guard shape (PSH1306): a method that reads a bool field to return
/// early and later sets the same field to true lets every thread that passes the check before
/// the first write run the protected code. An <c>Interlocked.Exchange</c> latch on an int
/// field admits exactly one caller. Both the check and the write must sit outside lock
/// statements — a lock already serializes them — and the shape is matched syntactically
/// before either end is bound to the field. Whether a given guard must be thread-safe is
/// contextual, so the rule is opt-in.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1306InterlockedOnceGuardAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.InterlockedOnceGuard);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
    }

    /// <summary>Returns the flag expression of an early-return guard, before any binding.</summary>
    /// <param name="ifStatement">The if statement to inspect.</param>
    /// <returns>The read flag expression, or <see langword="null"/> when the shape does not match.</returns>
    internal static ExpressionSyntax? TryGetGuardFlag(IfStatementSyntax ifStatement)
    {
        if (ifStatement.Else is not null || !IsReturnStatement(ifStatement.Statement))
        {
            return null;
        }

        return TryGetFlagName(ifStatement.Condition) is null ? null : ifStatement.Condition;
    }

    /// <summary>Returns the flag's simple name from a bare or this-qualified reference.</summary>
    /// <param name="expression">The candidate flag expression.</param>
    /// <returns>The name node, or <see langword="null"/>.</returns>
    internal static SimpleNameSyntax? TryGetFlagName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name,
            _ => null,
        };

    /// <summary>Returns whether a guarded statement is a return, alone or as a single-statement block.</summary>
    /// <param name="statement">The guarded statement.</param>
    /// <returns><see langword="true"/> for the early-return shape.</returns>
    private static bool IsReturnStatement(StatementSyntax statement)
        => statement is ReturnStatementSyntax or BlockSyntax { Statements: [ReturnStatementSyntax] };

    /// <summary>Returns whether a node sits inside a lock statement below a limit node.</summary>
    /// <param name="node">The node to test.</param>
    /// <param name="limit">The enclosing function that bounds the walk.</param>
    /// <returns><see langword="true"/> when a lock statement is between the node and the limit.</returns>
    private static bool IsInsideLock(SyntaxNode node, SyntaxNode limit)
    {
        for (var current = node.Parent; current is not null && current != limit; current = current.Parent)
        {
            if (current is LockStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the nearest enclosing function-like body owner of a node.</summary>
    /// <param name="node">The node whose owner is sought.</param>
    /// <returns>The owner, or <see langword="null"/> at type scope.</returns>
    private static SyntaxNode? FindBodyOwner(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
                    or AccessorDeclarationSyntax or BaseMethodDeclarationSyntax:
                    return current;
                case BaseTypeDeclarationSyntax or CompilationUnitSyntax:
                    return null;
                default:
                    continue;
            }
        }

        return null;
    }

    /// <summary>Reports PSH1306 for a guard whose flag is later set outside any lock.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (TryGetGuardFlag(ifStatement) is not { } flag
            || FindBodyOwner(ifStatement) is not { } owner
            || IsInsideLock(ifStatement, owner))
        {
            return;
        }

        var scan = new LatchScan(TryGetFlagName(flag)!.Identifier.ValueText, ifStatement.Span.End, owner);
        DescendantTraversalHelper.VisitDescendantTokens(owner, ref scan, static (in SyntaxToken token, ref LatchScan state) => state.Visit(in token));
        if (scan.Assignment is not { } assignment || !BindsToSameBoolField(context, flag, assignment))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.InterlockedOnceGuard,
            ifStatement.SyntaxTree,
            ifStatement.Condition.Span,
            TryGetFlagName(flag)!.Identifier.ValueText));
    }

    /// <summary>Returns whether the guard read and the later write bind to one bool field.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="flag">The guard's flag expression.</param>
    /// <param name="assignment">The later true-assignment.</param>
    /// <returns><see langword="true"/> when both ends are the same bool field.</returns>
    private static bool BindsToSameBoolField(SyntaxNodeAnalysisContext context, ExpressionSyntax flag, AssignmentExpressionSyntax assignment)
        => context.SemanticModel.GetSymbolInfo(flag, context.CancellationToken).Symbol
            is IFieldSymbol { Type.SpecialType: SpecialType.System_Boolean } field
            && SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol,
                field);

    /// <summary>Token-visitor state that finds a later unlocked <c>flag = true</c> assignment.</summary>
    private sealed class LatchScan
    {
        /// <summary>The flag name being tracked.</summary>
        private readonly string _name;

        /// <summary>The guard's end position; only later writes count.</summary>
        private readonly int _minimumPosition;

        /// <summary>The enclosing function bounding lock checks.</summary>
        private readonly SyntaxNode _owner;

        /// <summary>Initializes a new instance of the <see cref="LatchScan"/> class.</summary>
        /// <param name="name">The flag name to track.</param>
        /// <param name="minimumPosition">The guard's end position.</param>
        /// <param name="owner">The enclosing function.</param>
        public LatchScan(string name, int minimumPosition, SyntaxNode owner)
        {
            _name = name;
            _minimumPosition = minimumPosition;
            _owner = owner;
        }

        /// <summary>Gets the matched true-assignment, when found.</summary>
        public AssignmentExpressionSyntax? Assignment { get; private set; }

        /// <summary>Classifies one token; stops the walk on the first match.</summary>
        /// <param name="token">The token to inspect.</param>
        /// <returns><see langword="true"/> to keep walking.</returns>
        public bool Visit(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.SpanStart < _minimumPosition
                || token.ValueText != _name
                || token.Parent is not IdentifierNameSyntax identifier
                || TryGetTrueAssignment(identifier) is not { } assignment
                || IsInsideLock(assignment, _owner))
            {
                return true;
            }

            Assignment = assignment;
            return false;
        }

        /// <summary>Returns the enclosing <c>flag = true</c> assignment of a flag reference.</summary>
        /// <param name="identifier">The flag reference.</param>
        /// <returns>The assignment, or <see langword="null"/>.</returns>
        private static AssignmentExpressionSyntax? TryGetTrueAssignment(IdentifierNameSyntax identifier)
        {
            SyntaxNode target = identifier.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } qualified
                && qualified.Name == identifier
                ? qualified
                : identifier;

            return target.Parent is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
                && assignment.Left == target
                && assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
                ? assignment
                : null;
        }
    }
}
