// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a blocking call inside an <c>async</c> method, local function, or lambda (PSH1313):
/// <c>.Result</c>, <c>.Wait()</c>, and <c>.GetAwaiter().GetResult()</c> on a task, and a
/// synchronous call such as <c>stream.Read(...)</c> or <c>File.ReadAllText(...)</c> that has an
/// <c>…Async</c> sibling. Blocking the thread that was about to be released is how a thread pool
/// starves under load.
/// <para>
/// The async overload is never guessed from the name. For the task-blocking forms the receiver
/// must bind to a real task type, and the replacement is simply awaiting it. For the synchronous
/// sibling the candidate is resolved off the bound method's own containing type and must agree on
/// staticness, await to exactly the type the synchronous call returned, and accept the very
/// arguments already written — see <see cref="AsyncSiblingResolver"/>. A type with a same-named
/// <c>…Async</c> that does not fit is therefore never suggested. Resolutions are memoized per
/// method symbol, so the repeated lookups an async-heavy file would otherwise cost are paid once.
/// </para>
/// <para>
/// The nearest enclosing function decides the context, so a synchronous local function inside an
/// async method stays clean. <c>Dispose</c> is left to PSH1310, which owns the <c>await using</c>
/// rewrite.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1313CallAsyncInAsyncContextAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The blocking task property.</summary>
    internal const string ResultPropertyName = "Result";

    /// <summary>The blocking task method.</summary>
    internal const string WaitMethodName = "Wait";

    /// <summary>The blocking awaiter method.</summary>
    internal const string GetResultMethodName = "GetResult";

    /// <summary>The awaiter factory that precedes the blocking awaiter method.</summary>
    internal const string GetAwaiterMethodName = "GetAwaiter";

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
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMemberAccess(nodeContext, tasks), SyntaxKind.SimpleMemberAccessExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, tasks, siblings), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns the receiver a <c>GetAwaiter().GetResult()</c> chain blocks on, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The awaited receiver, or <see langword="null"/> when the chain does not match.</returns>
    internal static ExpressionSyntax? TryGetAwaiterChainReceiver(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetResultMethodName } outer
            && outer.Expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } inner
            && inner.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetAwaiterMethodName } awaiter
            ? awaiter.Expression
            : null;

    /// <summary>Reports PSH1313 for a <c>.Result</c> read on a task inside an async function.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, AsyncSiblingResolver.TaskTypes tasks)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name.Identifier.ValueText != ResultPropertyName
            || !Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(access)
            || !IsGenericTask(context, access.Expression, tasks))
        {
            return;
        }

        Report(context, access, ResultPropertyName, access.Expression.ToString());
    }

    /// <summary>Reports PSH1313 for a blocking wait, a blocking awaiter chain, or a synchronous call with an async sibling.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="siblings">The per-compilation cache of resolved async siblings.</param>
    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        AsyncSiblingResolver.TaskTypes tasks,
        ConcurrentDictionary<ISymbol, IMethodSymbol?> siblings)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(invocation))
        {
            return;
        }

        if (TryGetAwaiterChainReceiver(invocation) is { } awaited)
        {
            if (IsAnyTask(context, awaited, tasks))
            {
                Report(context, invocation, GetResultMethodName, awaited.ToString());
            }

            return;
        }

        if (IsParameterlessWait(invocation))
        {
            var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
            if (IsAnyTask(context, receiver, tasks))
            {
                Report(context, invocation, WaitMethodName, receiver.ToString());
            }

            return;
        }

        AnalyzeSyncSibling(context, invocation, tasks, siblings);
    }

    /// <summary>Reports PSH1313 when a synchronous call has an async sibling that provably accepts the same arguments.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The synchronous invocation.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="siblings">The per-compilation cache of resolved async siblings.</param>
    private static void AnalyzeSyncSibling(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        AsyncSiblingResolver.TaskTypes tasks,
        ConcurrentDictionary<ISymbol, IMethodSymbol?> siblings)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol sync)
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

        Report(context, invocation, sync.Name, sibling.Name);
    }

    /// <summary>Returns whether an invocation is a parameterless <c>Wait()</c> call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the syntax names a parameterless Wait.</returns>
    private static bool IsParameterlessWait(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: WaitMethodName };

    /// <summary>Returns whether an expression's type is a generic task, the only shape that has a blocking Result.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The receiver expression.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the receiver is a <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c>.</returns>
    private static bool IsGenericTask(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, in AsyncSiblingResolver.TaskTypes tasks)
    {
        if (context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is not { } type)
        {
            return false;
        }

        var definition = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, tasks.TaskOfT)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.ValueTaskOfT);
    }

    /// <summary>Returns whether an expression's type is any task shape.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The receiver expression.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the receiver is a task or value task.</returns>
    private static bool IsAnyTask(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, in AsyncSiblingResolver.TaskTypes tasks)
        => context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is { } type
            && AsyncSiblingResolver.IsAwaitable(type, tasks);

    /// <summary>Reports one blocking call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The blocking expression to report.</param>
    /// <param name="blocking">The blocking member's name.</param>
    /// <param name="replacement">What to call and await instead.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode node, string blocking, string replacement)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.CallAsyncInAsyncContext,
            node.SyntaxTree,
            node.Span,
            blocking,
            replacement));
}
