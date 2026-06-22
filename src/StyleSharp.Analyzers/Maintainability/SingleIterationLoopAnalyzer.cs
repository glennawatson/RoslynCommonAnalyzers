// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports loops that cannot naturally reach a second iteration.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleIterationLoopAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.SingleIterationLoop);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            AnalyzeLoop,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement);
    }

    /// <summary>Analyzes one loop body for unconditional jumps.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context)
    {
        var loop = (StatementSyntax)context.Node;
        var body = GetLoopBody(loop);
        if (body is null)
        {
            return;
        }

        var walker = new LoopJumpWalker(loop);
        walker.Visit(body);
        var violation = walker.UnconditionalContinue ?? (walker.HasConditionalContinue ? null : walker.UnconditionalTerminate);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.SingleIterationLoop, loop.GetLocation()));
    }

    /// <summary>Returns a loop body.</summary>
    /// <param name="loop">The loop statement.</param>
    /// <returns>The loop body, or <see langword="null"/>.</returns>
    private static StatementSyntax? GetLoopBody(StatementSyntax loop)
        => loop switch
        {
            ForStatementSyntax statement => statement.Statement,
            ForEachStatementSyntax statement => statement.Statement,
            WhileStatementSyntax statement => statement.Statement,
            DoStatementSyntax statement => statement.Statement,
            _ => null
        };

    /// <summary>Finds jump statements that make a loop single-iteration.</summary>
    private sealed class LoopJumpWalker : CSharpSyntaxWalker
    {
        /// <summary>The loop being analyzed.</summary>
        private readonly SyntaxNode _loop;

        /// <summary>Initializes a new instance of the <see cref="LoopJumpWalker"/> class.</summary>
        /// <param name="loop">The loop being analyzed.</param>
        public LoopJumpWalker(SyntaxNode loop)
        {
            _loop = loop;
        }

        /// <summary>Gets the first unconditional continue.</summary>
        public StatementSyntax? UnconditionalContinue { get; private set; }

        /// <summary>Gets the first unconditional terminating jump.</summary>
        public StatementSyntax? UnconditionalTerminate { get; private set; }

        /// <summary>Gets a value indicating whether the loop has a conditional continue path.</summary>
        public bool HasConditionalContinue { get; private set; }

        /// <inheritdoc/>
        public override void Visit(SyntaxNode? node)
        {
            if (node is null || (node != _loop && IsNestedBoundary(node)))
            {
                return;
            }

            base.Visit(node);
        }

        /// <inheritdoc/>
        public override void VisitContinueStatement(ContinueStatementSyntax node)
        {
            StoreContinue(node);
            base.VisitContinueStatement(node);
        }

        /// <inheritdoc/>
        public override void VisitBreakStatement(BreakStatementSyntax node)
        {
            if (!BreakTargetsNestedSwitch(node))
            {
                StoreTerminatingJump(node);
            }

            base.VisitBreakStatement(node);
        }

        /// <inheritdoc/>
        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            StoreTerminatingJump(node);
            base.VisitReturnStatement(node);
        }

        /// <inheritdoc/>
        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            StoreTerminatingJump(node);
            base.VisitThrowStatement(node);
        }

        /// <summary>Returns whether a nested node should not be scanned as part of this loop.</summary>
        /// <param name="node">The candidate node.</param>
        /// <returns><see langword="true"/> for nested loops and nested function bodies.</returns>
        private static bool IsNestedBoundary(SyntaxNode node)
            => node is ForStatementSyntax
                or ForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or LocalFunctionStatementSyntax
                or SimpleLambdaExpressionSyntax
                or ParenthesizedLambdaExpressionSyntax
                or AnonymousMethodExpressionSyntax;

        /// <summary>Stores a continue statement.</summary>
        /// <param name="node">The continue statement.</param>
        private void StoreContinue(ContinueStatementSyntax node)
        {
            if (IsConditional(node))
            {
                HasConditionalContinue = true;
            }
            else
            {
                UnconditionalContinue ??= node;
            }
        }

        /// <summary>Stores a terminating jump statement.</summary>
        /// <param name="node">The jump statement.</param>
        private void StoreTerminatingJump(StatementSyntax node)
        {
            if (IsConditional(node))
            {
                return;
            }

            UnconditionalTerminate ??= node;
        }

        /// <summary>Returns whether a jump is under a conditional construct inside the loop.</summary>
        /// <param name="jump">The jump statement.</param>
        /// <returns><see langword="true"/> when the jump is conditional.</returns>
        private bool IsConditional(StatementSyntax jump)
        {
            for (var current = jump.Parent; current is not null && current != _loop; current = current.Parent)
            {
                if (current is IfStatementSyntax
                    or ElseClauseSyntax
                    or SwitchSectionSyntax
                    or CatchClauseSyntax
                    or TryStatementSyntax)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether a break exits a nested switch instead of the analyzed loop.</summary>
        /// <param name="breakStatement">The break statement.</param>
        /// <returns><see langword="true"/> when a switch owns the break before the loop does.</returns>
        private bool BreakTargetsNestedSwitch(BreakStatementSyntax breakStatement)
        {
            for (var current = breakStatement.Parent; current is not null && current != _loop; current = current.Parent)
            {
                if (current is SwitchStatementSyntax)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
