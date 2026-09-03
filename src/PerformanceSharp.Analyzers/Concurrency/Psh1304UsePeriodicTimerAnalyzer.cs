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
/// iteration count usually means retry logic rather than periodic work, and a <c>while</c>/<c>do</c>
/// loop whose condition is a relational comparison is skipped on the same grounds — a deadline or
/// attempt poll stops on a condition instead of running for the life of the process.
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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ConcurrencyRules.UsePeriodicTimer);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

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
            || LoopIsBounded(loopBody)
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

    /// <summary>Returns whether the paced loop runs to a bound rather than indefinitely.</summary>
    /// <param name="loopBody">The paced loop's body statement.</param>
    /// <returns><see langword="true"/> when a relational comparison decides whether the loop continues.</returns>
    /// <remarks>
    /// A deadline poll — <c>while (DateTime.UtcNow &lt; deadline)</c>, <c>while (sw.Elapsed &lt; timeout)</c>,
    /// <c>while (attempt &lt; max)</c> — is short-lived work that stops on a condition, not evenly spaced
    /// work that runs for the life of the process. <c>PeriodicTimer</c> replaces the loop condition with
    /// its own tick, so a bounded loop has to keep the condition regardless and gains a timer and a
    /// dispose for nothing. This is the same reasoning that already skips <c>for</c> loops, applied to the
    /// bounded <c>while</c> and <c>do</c> forms.
    /// </remarks>
    private static bool LoopIsBounded(StatementSyntax loopBody)
    {
        var condition = loopBody.Parent switch
        {
            WhileStatementSyntax loop => loop.Condition,
            DoStatementSyntax loop => loop.Condition,
            _ => null
        };

        if (condition is null)
        {
            return false;
        }

        if (IsRelational(condition))
        {
            return true;
        }

        var state = default(RelationalScanState);
        DescendantTraversalHelper.VisitDescendants<BinaryExpressionSyntax, RelationalScanState>(condition, ref state, VisitConditionOperand);
        return state.Found;
    }

    /// <summary>Classifies one binary expression inside a loop condition.</summary>
    /// <param name="binary">The visited binary expression.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a bound is seen.</returns>
    private static bool VisitConditionOperand(BinaryExpressionSyntax binary, ref RelationalScanState state)
    {
        if (!IsRelational(binary))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether an expression compares two operands for order.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, and <c>&gt;=</c>.</returns>
    private static bool IsRelational(SyntaxNode expression)
        => expression.IsKind(SyntaxKind.LessThanExpression)
            || expression.IsKind(SyntaxKind.LessThanOrEqualExpression)
            || expression.IsKind(SyntaxKind.GreaterThanExpression)
            || expression.IsKind(SyntaxKind.GreaterThanOrEqualExpression);

    /// <summary>
    /// Returns whether any identifier used inside the delay's arguments is assigned or
    /// incremented anywhere in the loop body — the retry-backoff shape the rule must not flag.
    /// </summary>
    /// <param name="invocation">The delay invocation.</param>
    /// <param name="loopBody">The paced loop's body statement.</param>
    /// <returns><see langword="true"/> when the delay amount changes between iterations.</returns>
    /// <remarks>
    /// The loop body is read once, into the set of names it writes, and the delay's identifiers are then a
    /// lookup each. Asking the question per identifier meant re-reading the whole body for every name the
    /// delay argument mentions, so a delay computed from two of them read the body twice.
    /// </remarks>
    private static bool DelayArgumentIsAdjustedInLoop(InvocationExpressionSyntax invocation, StatementSyntax loopBody)
    {
        var written = new HashSet<string>(StringComparer.Ordinal);
        var writeState = new WrittenNameScanState(written);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, WrittenNameScanState>(loopBody, ref writeState, VisitWrittenName);

        if (written.Count == 0)
        {
            return false;
        }

        var readState = new DelayIdentifierScanState(written);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, DelayIdentifierScanState>(invocation.ArgumentList, ref readState, VisitDelayIdentifier);
        return readState.Found;
    }

    /// <summary>Records one identifier the loop body writes.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitWrittenName(IdentifierNameSyntax identifier, ref WrittenNameScanState state)
    {
        if (!IsWriteTarget(identifier))
        {
            return true;
        }

        state.Names.Add(identifier.Identifier.ValueText);
        return true;
    }

    /// <summary>Classifies one identifier the delay argument reads.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a match is found.</returns>
    private static bool VisitDelayIdentifier(IdentifierNameSyntax identifier, ref DelayIdentifierScanState state)
    {
        if (!state.Written.Contains(identifier.Identifier.ValueText))
        {
            return true;
        }

        state.Found = true;
        return false;
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

    /// <summary>Tracks whether a loop condition compares two operands for order.</summary>
    private record struct RelationalScanState
    {
        /// <summary>Gets or sets a value indicating whether a relational comparison was found.</summary>
        public bool Found { get; set; }
    }

    /// <summary>Collects the names a loop body writes, in one pass over it.</summary>
    /// <param name="Names">The names collected so far.</param>
    private record struct WrittenNameScanState(HashSet<string> Names);

    /// <summary>Decides whether the delay argument reads any name the loop body writes.</summary>
    /// <param name="Written">The names the loop body writes.</param>
    private record struct DelayIdentifierScanState(HashSet<string> Written)
    {
        /// <summary>Gets or sets a value indicating whether the delay amount changes between iterations.</summary>
        public bool Found { get; set; }
    }
}
