// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>await Task.Delay(...)</c> statements that pace a <c>while</c>/<c>do</c> loop
/// (PSH1304), suggesting <c>PeriodicTimer</c>. The whole rule is gated on
/// <c>System.Threading.PeriodicTimer</c> existing in the compilation, so it costs nothing on
/// frameworks without it. Only unconditional pacing is reported — the delay statement must be a
/// direct child of the loop body — and loops that adjust the delay between iterations (retry
/// backoff) stay clean: any identifier used in the delay argument that is written inside the
/// loop suppresses the report. <c>for</c>/<c>foreach</c> loops are skipped because a bounded
/// iteration count usually means retry logic rather than periodic work.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1304UsePeriodicTimerAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    private const string DelayMethodName = "Delay";

    /// <summary>The receiver type name the syntax gate requires.</summary>
    private const string TaskTypeName = "Task";

    /// <summary>The metadata name of the periodic timer type the rule is gated on.</summary>
    private const string PeriodicTimerMetadataName = "System.Threading.PeriodicTimer";

    /// <summary>The metadata name of the task type that provides Delay.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.UsePeriodicTimer);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var taskType = start.Compilation.GetTypeByMetadataName(TaskMetadataName);
            if (taskType is null || start.Compilation.GetTypeByMetadataName(PeriodicTimerMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAwait(nodeContext, taskType), SyntaxKind.AwaitExpression);
        });
    }

    /// <summary>Reports PSH1304 for an awaited delay that unconditionally paces a while/do loop.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="taskType">The task type providing Delay.</param>
    private static void AnalyzeAwait(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;
        if (awaitExpression.Expression is not InvocationExpressionSyntax invocation
            || !IsTaskDelayShape(invocation)
            || TryGetPacedLoopBody(awaitExpression) is not { } loopBody
            || DelayArgumentIsAdjustedInLoop(invocation, loopBody))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UsePeriodicTimer,
            awaitExpression.SyntaxTree,
            awaitExpression.Span));
    }

    /// <summary>Returns whether an invocation has the <c>Task.Delay(...)</c> syntax shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name is Delay and the receiver's rightmost identifier is Task.</returns>
    private static bool IsTaskDelayShape(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != DelayMethodName)
        {
            return false;
        }

        var receiver = access.Expression;
        while (receiver is MemberAccessExpressionSyntax nested)
        {
            receiver = nested.Name;
        }

        return receiver is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == TaskTypeName;
    }

    /// <summary>
    /// Returns the while/do loop body the awaited delay paces, or <see langword="null"/> when the
    /// delay is conditional or not in such a loop. The await must be a standalone expression
    /// statement that is the loop body itself or a direct child of the loop body's block.
    /// </summary>
    /// <param name="awaitExpression">The awaited delay expression.</param>
    /// <returns>The paced loop's body statement.</returns>
    private static StatementSyntax? TryGetPacedLoopBody(AwaitExpressionSyntax awaitExpression)
    {
        if (awaitExpression.Parent is not ExpressionStatementSyntax statement)
        {
            return null;
        }

        var container = statement.Parent;
        if (container is BlockSyntax block)
        {
            return block.Parent is WhileStatementSyntax or DoStatementSyntax ? block : null;
        }

        return container is WhileStatementSyntax or DoStatementSyntax ? statement : null;
    }

    /// <summary>
    /// Returns whether any identifier used inside the delay's arguments is assigned or
    /// incremented anywhere in the loop body — the retry-backoff shape the rule must not flag.
    /// </summary>
    /// <param name="invocation">The delay invocation.</param>
    /// <param name="loopBody">The paced loop's body statement.</param>
    /// <returns><see langword="true"/> when the delay amount changes between iterations.</returns>
    private static bool DelayArgumentIsAdjustedInLoop(InvocationExpressionSyntax invocation, StatementSyntax loopBody)
    {
        var state = new AdjustmentScanState(invocation.ArgumentList, loopBody);
        return state.LoopWritesDelayIdentifier();
    }

    /// <summary>Scans the loop body for writes to identifiers the delay argument reads.</summary>
    private sealed class AdjustmentScanState
    {
        /// <summary>The delay invocation's argument list, whose identifier tokens are the read set.</summary>
        private readonly ArgumentListSyntax _arguments;

        /// <summary>The loop body to scan for writes.</summary>
        private readonly StatementSyntax _loopBody;

        /// <summary>Initializes a new instance of the <see cref="AdjustmentScanState"/> class.</summary>
        /// <param name="arguments">The delay invocation's argument list.</param>
        /// <param name="loopBody">The loop body to scan for writes.</param>
        public AdjustmentScanState(ArgumentListSyntax arguments, StatementSyntax loopBody)
        {
            _arguments = arguments;
            _loopBody = loopBody;
        }

        /// <summary>Returns whether the loop body writes any identifier used in the delay argument.</summary>
        /// <returns><see langword="true"/> when a write is found.</returns>
        public bool LoopWritesDelayIdentifier()
        {
            foreach (var token in _arguments.DescendantTokens())
            {
                if (token.IsKind(SyntaxKind.IdentifierToken)
                    && token.Parent is IdentifierNameSyntax
                    && LoopWrites(token.ValueText))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether the loop body assigns, increments, or passes by ref an identifier.</summary>
        /// <param name="name">The identifier name to look for.</param>
        /// <returns><see langword="true"/> when a write to the name is found.</returns>
        private bool LoopWrites(string name)
        {
            var state = new WriteScan(name);
            DescendantTraversalHelper.VisitDescendantTokens(_loopBody, ref state, static (in SyntaxToken token, ref WriteScan scan) => scan.Visit(in token));
            return state.Found;
        }

        /// <summary>Token-visitor state that detects writes to one identifier name.</summary>
        private sealed class WriteScan
        {
            /// <summary>The identifier name being tracked.</summary>
            private readonly string _name;

            /// <summary>Initializes a new instance of the <see cref="WriteScan"/> class.</summary>
            /// <param name="name">The identifier name to track.</param>
            public WriteScan(string name)
            {
                _name = name;
            }

            /// <summary>Gets a value indicating whether a write to the tracked identifier was found.</summary>
            public bool Found { get; private set; }

            /// <summary>Inspects one token for a write to the tracked identifier.</summary>
            /// <param name="token">The token to inspect.</param>
            /// <returns><see langword="true"/> to keep walking, or <see langword="false"/> once a write is found.</returns>
            public bool Visit(in SyntaxToken token)
            {
                if (!token.IsKind(SyntaxKind.IdentifierToken)
                    || token.ValueText != _name
                    || token.Parent is not IdentifierNameSyntax identifier)
                {
                    return true;
                }

                Found = IsWriteTarget(identifier);
                return !Found;
            }

            /// <summary>Returns whether an identifier occurrence is the target of a write.</summary>
            /// <param name="identifier">The identifier occurrence.</param>
            /// <returns><see langword="true"/> for assignment targets, increments, decrements, and ref/out arguments.</returns>
            private static bool IsWriteTarget(IdentifierNameSyntax identifier)
                => identifier.Parent switch
                {
                    AssignmentExpressionSyntax assignment => assignment.Left == identifier,
                    PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
                    ArgumentSyntax argument => !argument.RefOrOutKeyword.IsKind(SyntaxKind.None),
                    _ => false,
                };
        }
    }
}
