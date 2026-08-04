// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Recognizes the ways C# parks a thread on an awaitable — <c>t.Result</c>, <c>t.Wait(…)</c>,
/// <c>t.GetAwaiter().GetResult()</c>, <c>t.RunSynchronously()</c>, <c>Task.WaitAll(…)</c>, and
/// <c>Task.WaitAny(…)</c> — and hands back the awaitable that was blocked on. Every match starts
/// from the member's name, so an expression that cannot be a blocking wait is rejected before the
/// semantic model is ever touched; only then is the call bound and required to be a real
/// <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, or <c>ValueTask&lt;T&gt;</c>. A
/// <c>Wait()</c> on a <c>SemaphoreSlim</c> is therefore not a match, and neither is a
/// <c>WaitAll</c> of the caller's own. <c>WaitAsync</c> is a different name from <c>Wait</c> and is
/// never matched: it is the non-blocking API, and the fix rather than the defect.
/// <para>
/// A <c>ConfigureAwait(…)</c> in front of <c>GetAwaiter()</c> is carried, not discarded: the
/// expression to await keeps it (so the rewrite preserves the capture choice), while the guard
/// target — what a completion check must be written against — is the task underneath it.
/// </para>
/// <para>
/// <c>WaitAll</c> and <c>WaitAny</c> are static, so they block on their arguments rather than on a
/// receiver. <see cref="Site.Kind"/> says which shape matched, and for those two the tasks are the
/// invocation's arguments — that is where both the guard analysis and the rewrite read them from.
/// </para>
/// </summary>
internal static class BlockingWait
{
    /// <summary>The blocking task property.</summary>
    internal const string ResultPropertyName = "Result";

    /// <summary>The blocking task method.</summary>
    internal const string WaitMethodName = "Wait";

    /// <summary>The static method that blocks until every listed task finishes.</summary>
    internal const string WaitAllMethodName = "WaitAll";

    /// <summary>The static method that blocks until one of the listed tasks finishes.</summary>
    internal const string WaitAnyMethodName = "WaitAny";

    /// <summary>The method that runs a task on the calling thread.</summary>
    internal const string RunSynchronouslyMethodName = "RunSynchronously";

    /// <summary>The blocking awaiter method.</summary>
    internal const string GetResultMethodName = "GetResult";

    /// <summary>The awaiter factory that precedes the blocking awaiter method.</summary>
    internal const string GetAwaiterMethodName = "GetAwaiter";

    /// <summary>The continuation-capture configuration that may sit between a task and its awaiter.</summary>
    internal const string ConfigureAwaitMethodName = "ConfigureAwait";

    /// <summary>The blocking shape that matched, which decides what the wait blocks on.</summary>
    internal enum Kind
    {
        /// <summary>A wait on one awaitable: <c>Result</c>, <c>Wait</c>, <c>GetAwaiter().GetResult()</c>, or <c>RunSynchronously</c>.</summary>
        SingleTask,

        /// <summary><c>Task.WaitAll(…)</c>, which parks the thread until every argument has finished.</summary>
        WaitAll,

        /// <summary><c>Task.WaitAny(…)</c>, which parks the thread until one argument has finished.</summary>
        WaitAny,
    }

    /// <summary>Matches a node against the blocking-wait shapes and binds the awaitable it blocks on.</summary>
    /// <param name="node">The candidate member access or invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/> when the node does not block on a task.</returns>
    public static Site? TryMatch(SyntaxNode node, SemanticModel model, in AsyncSiblingResolver.TaskTypes tasks, CancellationToken cancellationToken)
        => node switch
        {
            MemberAccessExpressionSyntax access => TryMatchResult(access, model, tasks, cancellationToken),
            InvocationExpressionSyntax invocation => TryMatchInvocation(invocation, model, tasks, cancellationToken),
            _ => null,
        };

    /// <summary>Strips the <c>ConfigureAwait(…)</c> calls wrapping an awaitable, leaving the task itself.</summary>
    /// <param name="expression">The awaited expression.</param>
    /// <returns>The underlying task expression.</returns>
    public static ExpressionSyntax UnwrapConfigureAwait(ExpressionSyntax expression)
    {
        while (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: ConfigureAwaitMethodName } configured })
        {
            expression = configured.Expression;
        }

        return expression;
    }

    /// <summary>Matches a <c>t.Result</c> read on a generic task.</summary>
    /// <param name="access">The member access to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/>.</returns>
    private static Site? TryMatchResult(
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        CancellationToken cancellationToken)
    {
        if (access.Name.Identifier.ValueText != ResultPropertyName
            || GetTypeOf(access.Expression, model, cancellationToken) is not { } type
            || !IsGenericTask(type, tasks))
        {
            return null;
        }

        return new Site(Kind.SingleTask, access.Expression, access.Expression, ResultPropertyName, AwaitIsEquivalent: true);
    }

    /// <summary>Matches the invocation shapes: <c>t.Wait(…)</c>, <c>t.GetAwaiter().GetResult()</c>, <c>t.RunSynchronously()</c>, <c>Task.WaitAll(…)</c>, and <c>Task.WaitAny(…)</c>.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/>.</returns>
    private static Site? TryMatchInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return null;
        }

        return access.Name.Identifier.ValueText switch
        {
            GetResultMethodName => TryMatchAwaiterChain(invocation, access, model, tasks, cancellationToken),
            WaitMethodName => TryMatchWait(invocation, access, model, tasks, cancellationToken),
            WaitAllMethodName => TryMatchCombinator(invocation, model, tasks, Kind.WaitAll, cancellationToken),
            WaitAnyMethodName => TryMatchCombinator(invocation, model, tasks, Kind.WaitAny, cancellationToken),
            RunSynchronouslyMethodName => TryMatchRunSynchronously(invocation, access, model, tasks, cancellationToken),
            _ => null,
        };
    }

    /// <summary>Matches a <c>Task.WaitAll(…)</c> or <c>Task.WaitAny(…)</c> call.</summary>
    /// <param name="invocation">The combinator invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="kind">Which of the two combinators the name matched.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/>.</returns>
    /// <remarks>
    /// These are static, so there is no receiver to bind a type off: the invocation itself is bound
    /// and required to be the framework's own <c>Task.WaitAll</c> / <c>Task.WaitAny</c>, which is
    /// what keeps a <c>WaitAll</c> of the caller's own out of the rule. A call that was given no
    /// tasks parks nothing and is not a wait at all.
    /// <para>
    /// Only the overload that takes nothing but the tasks maps onto <c>WhenAll</c> / <c>WhenAny</c>:
    /// the timeout and cancellation-token overloads give up on the wait in a way an <c>await</c>
    /// does not, so they are reported without a fix. <c>WaitAny</c> also loses its meaning whenever
    /// its result is used, because it returns the *index* of the task that finished and
    /// <c>WhenAny</c> returns the task itself — so the rewrite is offered only where the value is
    /// discarded.
    /// </para>
    /// </remarks>
    private static Site? TryMatchCombinator(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        Kind kind,
        CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList.Arguments.Count == 0
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, tasks.Task))
        {
            return null;
        }

        var awaitIsEquivalent = method.Parameters.Length == 1
            && (kind == Kind.WaitAll || invocation.Parent is ExpressionStatementSyntax);

        return new Site(kind, invocation, invocation, method.Name, awaitIsEquivalent);
    }

    /// <summary>Matches a <c>t.RunSynchronously(…)</c> call, which runs the task on the calling thread.</summary>
    /// <param name="invocation">The <c>RunSynchronously</c> invocation.</param>
    /// <param name="access">The <c>RunSynchronously</c> member access.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Never await-equivalent, so it is reported without a fix. <c>RunSynchronously</c> is what
    /// *starts* a cold task — awaiting one that was never started waits forever — so there is no
    /// rewrite to offer: the task has to be created differently, and only its author can say how.
    /// </remarks>
    private static Site? TryMatchRunSynchronously(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { IsStatic: false } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, tasks.Task))
        {
            return null;
        }

        var receiver = access.Expression;
        return new Site(Kind.SingleTask, receiver, receiver, RunSynchronouslyMethodName, AwaitIsEquivalent: false);
    }

    /// <summary>Matches the <c>GetAwaiter().GetResult()</c> chain, with or without a <c>ConfigureAwait</c> in front of it.</summary>
    /// <param name="invocation">The <c>GetResult()</c> invocation.</param>
    /// <param name="access">The <c>GetResult</c> member access.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/>.</returns>
    private static Site? TryMatchAwaiterChain(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList.Arguments.Count != 0)
        {
            return null;
        }

        // Parking the awaiter in a local splits the same wait over two statements and blocks exactly
        // as hard, so the local is followed back to what it was given.
        var awaiterExpression = access.Expression is IdentifierNameSyntax identifier
            && TryGetLocalInitializer(identifier, model, cancellationToken) is { } initializer
                ? initializer
                : access.Expression;

        if (awaiterExpression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } awaiterCall
            && awaiterCall.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetAwaiterMethodName } awaiter)
        {
            var awaited = awaiter.Expression;
            var target = UnwrapConfigureAwait(awaited);
            return GetTypeOf(target, model, cancellationToken) is { } type && AsyncSiblingResolver.IsAwaitable(type, tasks)
                ? new Site(Kind.SingleTask, awaited, target, GetResultMethodName, AwaitIsEquivalent: true)
                : null;
        }

        // The awaiter can reach this call from anywhere — a field, a property, a parameter. Its type
        // still says a thread is being parked. There is no task expression to rewrite to, so the wait
        // is reported without a fix rather than rewritten into something that means something else.
        return GetTypeOf(access.Expression, model, cancellationToken) is { } receiver && IsTaskAwaiter(receiver)
            ? new Site(Kind.SingleTask, access.Expression, access.Expression, GetResultMethodName, AwaitIsEquivalent: false)
            : null;
    }

    /// <summary>Returns whether a type is one of the awaiters the task types hand out.</summary>
    /// <param name="type">The receiver's type.</param>
    /// <returns><see langword="true"/> for a BCL task or value-task awaiter, configured or not.</returns>
    /// <remarks>
    /// The set is closed on purpose: it names exactly the awaiters of the four types this rule covers,
    /// so a custom awaitable's own awaiter — whose <c>GetResult</c> is required by the awaiter pattern
    /// to be synchronous — is never mistaken for a blocking wait.
    /// </remarks>
    private static bool IsTaskAwaiter(ITypeSymbol type)
    {
        if (type.Name is not ("TaskAwaiter" or "ValueTaskAwaiter" or "ConfiguredTaskAwaiter" or "ConfiguredValueTaskAwaiter"))
        {
            return false;
        }

        // A configured awaiter is nested inside its awaitable, which is the type carrying the namespace.
        var owner = type.ContainingType ?? type;
        return owner.ContainingNamespace is
        {
            Name: "CompilerServices",
            ContainingNamespace: { Name: "Runtime", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }
        };
    }

    /// <summary>Returns the value a local was declared with, when it has exactly one declaration.</summary>
    /// <param name="identifier">The identifier naming the local.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The initializer expression, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Only reached once a <c>GetResult()</c> has already been found on a bare identifier, so the
    /// symbol lookup never costs a file that does not block.
    /// </remarks>
    private static ExpressionSyntax? TryGetLocalInitializer(
        IdentifierNameSyntax identifier,
        SemanticModel model,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol { DeclaringSyntaxReferences.Length: 1 } local
            && local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Initializer.Value: { } value }
                ? value
                : null;

    /// <summary>Matches a <c>t.Wait(…)</c> call on a task.</summary>
    /// <param name="invocation">The <c>Wait</c> invocation.</param>
    /// <param name="access">The <c>Wait</c> member access.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched site, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Only the parameterless overload is await-equivalent. <c>Wait(timeout)</c> gives up after an
    /// interval and <c>Wait(cancellationToken)</c> abandons the wait when the token fires; a plain
    /// <c>await</c> does neither, so those two are reported without a fix rather than rewritten
    /// into something that means something else.
    /// </remarks>
    private static Site? TryMatchWait(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        CancellationToken cancellationToken)
    {
        var receiver = access.Expression;
        if (GetTypeOf(receiver, model, cancellationToken) is not { } type || !IsTask(type, tasks))
        {
            return null;
        }

        return new Site(Kind.SingleTask, receiver, receiver, WaitMethodName, invocation.ArgumentList.Arguments.Count == 0);
    }

    /// <summary>Binds an expression's type.</summary>
    /// <param name="expression">The expression to bind.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bound type, or <see langword="null"/>.</returns>
    private static ITypeSymbol? GetTypeOf(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
        => model.GetTypeInfo(expression, cancellationToken).Type;

    /// <summary>Returns whether a type is a generic task, the only shape with a blocking result to read.</summary>
    /// <param name="type">The receiver type.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> for <c>Task&lt;T&gt;</c> and <c>ValueTask&lt;T&gt;</c>.</returns>
    private static bool IsGenericTask(ITypeSymbol type, in AsyncSiblingResolver.TaskTypes tasks)
    {
        var definition = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, tasks.TaskOfT)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.ValueTaskOfT);
    }

    /// <summary>Returns whether a type is a task, the only shape with a blocking <c>Wait</c>.</summary>
    /// <param name="type">The receiver type.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> for <c>Task</c> and <c>Task&lt;T&gt;</c>.</returns>
    private static bool IsTask(ITypeSymbol type, in AsyncSiblingResolver.TaskTypes tasks)
    {
        var definition = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, tasks.Task)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.TaskOfT);
    }

    /// <summary>One matched blocking wait.</summary>
    /// <param name="Kind">Which blocking shape matched, and therefore where the tasks it blocks on are to be found.</param>
    /// <param name="Awaited">The expression an <c>await</c> would replace the block with, <c>ConfigureAwait</c> and all.</param>
    /// <param name="GuardTarget">The task underneath it, which a completion check must be written against.</param>
    /// <param name="BlockingMember">The member that does the blocking, for the message.</param>
    /// <param name="AwaitIsEquivalent">Whether awaiting preserves what the blocking call meant.</param>
    /// <remarks>
    /// <see cref="Awaited"/> and <see cref="GuardTarget"/> speak for a <see cref="Kind.SingleTask"/>
    /// wait, which has one receiver. A <see cref="Kind.WaitAll"/> or <see cref="Kind.WaitAny"/>
    /// blocks on its arguments instead, so both are set to the invocation itself and the tasks are
    /// read off its argument list — by the guard analysis, which asks about each one, and by the
    /// fix, which hands the whole list to <c>WhenAll</c> / <c>WhenAny</c>.
    /// </remarks>
    internal readonly record struct Site(
        Kind Kind,
        ExpressionSyntax Awaited,
        ExpressionSyntax GuardTarget,
        string BlockingMember,
        bool AwaitIsEquivalent);
}
