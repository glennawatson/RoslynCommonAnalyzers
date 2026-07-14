// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Recognizes the three ways C# parks a thread on an awaitable — <c>t.Result</c>,
/// <c>t.Wait(…)</c>, and <c>t.GetAwaiter().GetResult()</c> — and hands back the awaitable that
/// was blocked on. Every match starts from the member's name, so an expression that cannot be a
/// blocking wait is rejected before the semantic model is ever touched; only then is the receiver
/// bound and required to be a real <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, or
/// <c>ValueTask&lt;T&gt;</c>. A <c>Wait()</c> on a <c>SemaphoreSlim</c> is therefore not a match.
/// <para>
/// A <c>ConfigureAwait(…)</c> in front of <c>GetAwaiter()</c> is carried, not discarded: the
/// expression to await keeps it (so the rewrite preserves the capture choice), while the guard
/// target — what a completion check must be written against — is the task underneath it.
/// </para>
/// </summary>
internal static class BlockingWait
{
    /// <summary>The blocking task property.</summary>
    internal const string ResultPropertyName = "Result";

    /// <summary>The blocking task method.</summary>
    internal const string WaitMethodName = "Wait";

    /// <summary>The blocking awaiter method.</summary>
    internal const string GetResultMethodName = "GetResult";

    /// <summary>The awaiter factory that precedes the blocking awaiter method.</summary>
    internal const string GetAwaiterMethodName = "GetAwaiter";

    /// <summary>The continuation-capture configuration that may sit between a task and its awaiter.</summary>
    internal const string ConfigureAwaitMethodName = "ConfigureAwait";

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

        return new Site(access.Expression, access.Expression, ResultPropertyName, AwaitIsEquivalent: true);
    }

    /// <summary>Matches a <c>t.Wait(…)</c> call or a <c>t.GetAwaiter().GetResult()</c> chain.</summary>
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
            _ => null,
        };
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
        if (invocation.ArgumentList.Arguments.Count != 0
            || access.Expression is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } awaiterCall
            || awaiterCall.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetAwaiterMethodName } awaiter)
        {
            return null;
        }

        var awaited = awaiter.Expression;
        var target = UnwrapConfigureAwait(awaited);
        if (GetTypeOf(target, model, cancellationToken) is not { } type || !AsyncSiblingResolver.IsAwaitable(type, tasks))
        {
            return null;
        }

        return new Site(awaited, target, GetResultMethodName, AwaitIsEquivalent: true);
    }

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

        return new Site(receiver, receiver, WaitMethodName, invocation.ArgumentList.Arguments.Count == 0);
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
    /// <param name="Awaited">The expression an <c>await</c> would replace the block with, <c>ConfigureAwait</c> and all.</param>
    /// <param name="GuardTarget">The task underneath it, which a completion check must be written against.</param>
    /// <param name="BlockingMember">The member that does the blocking, for the message.</param>
    /// <param name="AwaitIsEquivalent">Whether awaiting preserves what the blocking call meant.</param>
    internal readonly record struct Site(
        ExpressionSyntax Awaited,
        ExpressionSyntax GuardTarget,
        string BlockingMember,
        bool AwaitIsEquivalent);
}
