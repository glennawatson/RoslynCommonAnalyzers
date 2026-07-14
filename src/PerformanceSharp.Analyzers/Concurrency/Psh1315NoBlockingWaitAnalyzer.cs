// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a thread parked on a task that is not provably complete (PSH1315): <c>t.Result</c>,
/// <c>t.Wait()</c>, <c>t.Wait(timeout)</c>, <c>t.Wait(cancellationToken)</c>, and
/// <c>t.GetAwaiter().GetResult()</c> — with or without a <c>ConfigureAwait</c> in front of the
/// awaiter — on a <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, or <c>ValueTask&lt;T&gt;</c>.
/// Under a SynchronizationContext the continuation needs the very thread the wait is holding and
/// deadlocks; on the thread pool the wait burns a worker that could have been completing the task.
/// Synchronous callers are reported too — that is where the deadlock lives — but only where an
/// author could act on it.
/// <para>
/// Three things keep the rule off code that is already right. A wait a completion check has
/// already proved cannot block is not a defect but the point of <c>IsCompletedSuccessfully</c>, so
/// <see cref="CompletionGuard"/> looks for that check and stays silent when it finds one. An
/// awaiter's own <c>GetResult</c> is required by the awaiter pattern to be synchronous. And a
/// member that overrides or implements a signature its author does not own — including
/// <c>IDisposable.Dispose</c> — cannot be made async at all. Those last two, plus <c>Main</c>,
/// live in <see cref="BlockingWaitExemption"/>.
/// </para>
/// <para>
/// Blocking on a task is reported here and nowhere else: PSH1313 covers the different mistake of
/// calling a synchronous method that has an async overload, and hands any receiver that binds to a
/// task straight over to this rule.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1315NoBlockingWaitAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the interface every awaiter implements.</summary>
    private const string NotifyCompletionMetadataName = "System.Runtime.CompilerServices.INotifyCompletion";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.NoBlockingWait);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (AsyncSiblingResolver.TaskTypes.Create(start.Compilation) is not { } tasks)
            {
                return;
            }

            // Only a violation ever needs these, and binding an entry point is not free: a clean
            // file must not pay for either.
            var compilation = start.Compilation;
            var notifyCompletion = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName(NotifyCompletionMetadataName));
            var entryPoint = new Lazy<IMethodSymbol?>(() => compilation.GetEntryPoint(CancellationToken.None));

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, tasks, notifyCompletion, entryPoint),
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1315 for a blocking wait the code has not proved complete and the author can act on.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="notifyCompletion">The lazily resolved awaiter marker interface.</param>
    /// <param name="entryPoint">The lazily resolved entry point.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        AsyncSiblingResolver.TaskTypes tasks,
        Lazy<INamedTypeSymbol?> notifyCompletion,
        Lazy<IMethodSymbol?> entryPoint)
    {
        var node = context.Node;
        if (BlockingWait.TryMatch(node, context.SemanticModel, tasks, context.CancellationToken) is not { } site
            || CompletionGuard.IsProvablyComplete(node, site.GuardTarget)
            || IsUnactionable(context, node, notifyCompletion, entryPoint))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.NoBlockingWait,
            node.SyntaxTree,
            node.Span,
            site.BlockingMember,
            site.GuardTarget.ToString()));
    }

    /// <summary>Returns whether the author could not act on the wait even if it were reported.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The blocking expression.</param>
    /// <param name="notifyCompletion">The lazily resolved awaiter marker interface.</param>
    /// <param name="entryPoint">The lazily resolved entry point.</param>
    /// <returns><see langword="true"/> when the enclosing member cannot await.</returns>
    /// <remarks>
    /// Inside an <c>async</c> function the fix is an <c>await</c> and no signature moves, so the
    /// member it belongs to never matters.
    /// </remarks>
    private static bool IsUnactionable(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node,
        Lazy<INamedTypeSymbol?> notifyCompletion,
        Lazy<IMethodSymbol?> entryPoint)
        => !Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(node)
            && BlockingWaitExemption.IsExempt(context, node, notifyCompletion.Value, entryPoint.Value);
}
