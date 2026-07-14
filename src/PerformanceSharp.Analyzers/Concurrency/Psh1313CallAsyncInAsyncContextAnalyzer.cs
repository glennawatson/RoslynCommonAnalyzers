// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a synchronous call inside an <c>async</c> method, local function, or lambda that has an
/// asynchronous sibling to move to (PSH1313): <c>stream.Read(…)</c> where <c>ReadAsync</c> fits,
/// <c>File.ReadAllText(…)</c> where <c>ReadAllTextAsync</c> fits. Blocking the thread that was
/// about to be released is how a thread pool starves under load.
/// <para>
/// The async overload is never guessed from the name. The candidate is resolved off the bound
/// method's own containing type and must agree on staticness, await to exactly the type the
/// synchronous call returned, and accept the very arguments already written — see
/// <see cref="AsyncSiblingResolver"/>. A type with a same-named <c>…Async</c> that does not fit is
/// therefore never suggested. Resolutions are memoized per method symbol, so the repeated lookups
/// an async-heavy file would otherwise cost are paid once.
/// </para>
/// <para>
/// The nearest enclosing function decides the context, so a synchronous local function inside an
/// async method stays clean. <c>Dispose</c> is left to PSH1310, which owns the <c>await using</c>
/// rewrite. Blocking on a task the code already has — <c>Result</c>, <c>Wait</c>,
/// <c>GetAwaiter().GetResult()</c> — is a different defect with a different fix and belongs to
/// PSH1315, so a receiver that binds to a task is handed straight over: only PSH1315 reports it,
/// and only PSH1315 decides whether a completion check already made it safe.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1313CallAsyncInAsyncContextAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.CallAsyncInAsyncContext);

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

            var siblings = new ConcurrentDictionary<ISymbol, IMethodSymbol?>(SymbolEqualityComparer.Default);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, tasks, siblings), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1313 when a synchronous call has an async sibling that provably accepts the same arguments.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="siblings">The per-compilation cache of resolved async siblings.</param>
    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        AsyncSiblingResolver.TaskTypes tasks,
        ConcurrentDictionary<ISymbol, IMethodSymbol?> siblings)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(invocation)
            || BlockingWait.TryMatch(invocation, context.SemanticModel, tasks, context.CancellationToken) is not null
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol sync)
        {
            return;
        }

        if (!siblings.TryGetValue(sync, out var sibling))
        {
            sibling = AsyncSiblingResolver.TryResolveAsyncSibling(sync, tasks);
            siblings.TryAdd(sync, sibling);
        }

        if (sibling is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.CallAsyncInAsyncContext,
            invocation.SyntaxTree,
            invocation.Span,
            sync.Name,
            sibling.Name));
    }
}
