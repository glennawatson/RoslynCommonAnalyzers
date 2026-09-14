// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>await Task.Delay(...)</c> statements that pace a <c>while</c>/<c>do</c> loop
/// (PSH1304), suggesting <c>PeriodicTimer</c>. The whole rule is gated on
/// <c>System.Threading.PeriodicTimer</c> existing in the compilation; framework types are resolved
/// only after the syntax checks pass. Only unconditional pacing is reported — the delay statement must be a
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

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveTask),
            AnalyzeAwait,
            SyntaxKind.AwaitExpression);
    }

    /// <summary>Reports PSH1304 for an awaited delay that unconditionally paces a while/do loop.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The compilation's lazily resolved task and timer types.</param>
    private static void AnalyzeAwait(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> markers)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;
        if (awaitExpression.Expression is not InvocationExpressionSyntax invocation
            || !TypeNameReceiver.IsCallOnTypeName(invocation, DelayMethodName, TaskTypeName)
            || TryGetPacedLoopBody(awaitExpression) is not { } loopBody
            || LoopIsBounded(loopBody)
            || DelayArgumentIsAdjustedInLoop(invocation, loopBody))
        {
            return;
        }

        if (markers.Get() is not { } taskType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UsePeriodicTimer,
            awaitExpression.SyntaxTree,
            awaitExpression.Span));
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

        return !DescendantTraversalHelper.VisitDescendants<BinaryExpressionSyntax>(condition, static binary => !IsRelational(binary));
    }

    /// <summary>Returns whether an expression compares two operands for order.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, and <c>&gt;=</c>.</returns>
    private static bool IsRelational(SyntaxNode expression) =>
        expression.IsKind(SyntaxKind.LessThanExpression)
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
        _ = DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HashSet<string>>(loopBody, ref written, VisitWrittenName);
        return written.Count != 0
            && !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HashSet<string>>(invocation.ArgumentList, ref written, IsUnwrittenName);
    }

    /// <summary>Records one identifier the loop body writes.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="names">The names the loop body writes.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitWrittenName(IdentifierNameSyntax identifier, ref HashSet<string> names)
    {
        if (!WriteTargetSyntax.IsIdentifierWriteTarget(identifier))
        {
            return true;
        }

        _ = names.Add(identifier.Identifier.ValueText);
        return true;
    }

    /// <summary>Continues the walk past an identifier the loop body does not write.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="written">The names the loop body writes.</param>
    /// <returns><see langword="false"/> at a written name, which stops the walk.</returns>
    private static bool IsUnwrittenName(IdentifierNameSyntax identifier, ref HashSet<string> written) =>
        !written.Contains(identifier.Identifier.ValueText);

    /// <summary>Resolves the task type and checks for the periodic timer replacement.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    /// <returns>The task type, or null when either required type is absent.</returns>
    private static INamedTypeSymbol? ResolveTask(Compilation compilation)
    {
        var taskType = compilation.GetTypeByMetadataName(TaskMetadataName);
        return taskType is not null && compilation.GetTypeByMetadataName(PeriodicTimerMetadataName) is not null
            ? taskType
            : null;
    }
}
