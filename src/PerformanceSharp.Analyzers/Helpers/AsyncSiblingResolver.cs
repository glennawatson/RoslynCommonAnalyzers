// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Resolves the asynchronous sibling of a synchronous method — the <c>FooAsync</c> that a
/// <c>Foo</c> call could be replaced by — and proves the match rather than guessing it from the
/// name. A candidate qualifies only when it is declared on the same type, agrees on staticness,
/// awaits to exactly the type the synchronous call returned (<c>void</c> to <c>Task</c>, <c>T</c>
/// to <c>Task&lt;T&gt;</c>, or the <c>ValueTask</c> equivalents), and accepts the same arguments —
/// any parameters it adds beyond the synchronous ones must be optional, so the existing call's
/// arguments still bind.
/// </summary>
internal static class AsyncSiblingResolver
{
    /// <summary>The suffix that names an asynchronous sibling.</summary>
    internal const string AsyncSuffix = "Async";

    /// <summary>The method whose asynchronous sibling belongs to PSH1310, not here.</summary>
    private const string DisposeMethodName = "Dispose";

    /// <summary>Resolves the asynchronous sibling a synchronous call could move to.</summary>
    /// <param name="sync">The bound synchronous method.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns>The matching asynchronous sibling, or <see langword="null"/> when the type has none that fits.</returns>
    public static IMethodSymbol? TryResolveAsyncSibling(IMethodSymbol sync, in TaskTypes tasks)
    {
        if (!IsReplaceableSyncMethod(sync, tasks))
        {
            return null;
        }

        var candidates = sync.ContainingType.GetMembers(sync.Name + AsyncSuffix);
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is IMethodSymbol candidate && IsMatchingSibling(candidate, sync, tasks))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Returns whether a type is one of the four task shapes.</summary>
    /// <param name="type">The type to classify.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the type is a task or value task, generic or not.</returns>
    public static bool IsAwaitable(ITypeSymbol type, in TaskTypes tasks)
    {
        var definition = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, tasks.Task)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.TaskOfT)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.ValueTask)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.ValueTaskOfT);
    }

    /// <summary>Returns whether a synchronous method is even a candidate for replacement.</summary>
    /// <param name="sync">The bound synchronous method.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the method is non-generic, not already async, and not PSH1310's Dispose.</returns>
    private static bool IsReplaceableSyncMethod(IMethodSymbol sync, in TaskTypes tasks)
        => !sync.IsGenericMethod
            && sync.Name.Length > 0
            && sync.Name != DisposeMethodName
            && !sync.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal)
            && !IsAwaitable(sync.ReturnType, tasks);

    /// <summary>Returns whether a same-named candidate really is a drop-in asynchronous sibling.</summary>
    /// <param name="candidate">The <c>…Async</c> candidate.</param>
    /// <param name="sync">The bound synchronous method.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the candidate matches on staticness, awaited result, and arguments.</returns>
    private static bool IsMatchingSibling(IMethodSymbol candidate, IMethodSymbol sync, in TaskTypes tasks)
        => !candidate.IsGenericMethod
            && candidate.IsStatic == sync.IsStatic
            && candidate.DeclaredAccessibility == Accessibility.Public
            && AwaitsTo(candidate.ReturnType, sync.ReturnType, tasks)
            && AcceptsSameArguments(sync.Parameters, candidate.Parameters);

    /// <summary>Returns whether awaiting a candidate's return type yields exactly what the synchronous call returned.</summary>
    /// <param name="asyncReturn">The candidate's return type.</param>
    /// <param name="syncReturn">The synchronous method's return type.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the awaited result is substitutable for the synchronous result.</returns>
    private static bool AwaitsTo(ITypeSymbol asyncReturn, ITypeSymbol syncReturn, in TaskTypes tasks)
    {
        var definition = asyncReturn.OriginalDefinition;
        if (syncReturn.SpecialType == SpecialType.System_Void)
        {
            return SymbolEqualityComparer.Default.Equals(definition, tasks.Task)
                || SymbolEqualityComparer.Default.Equals(definition, tasks.ValueTask);
        }

        var isGenericTask = SymbolEqualityComparer.Default.Equals(definition, tasks.TaskOfT)
            || SymbolEqualityComparer.Default.Equals(definition, tasks.ValueTaskOfT);
        return isGenericTask
            && asyncReturn is INamedTypeSymbol { TypeArguments.Length: 1 } named
            && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], syncReturn);
    }

    /// <summary>Returns whether the existing call's arguments still bind to the candidate.</summary>
    /// <param name="sync">The synchronous method's parameters.</param>
    /// <param name="candidate">The candidate sibling's parameters.</param>
    /// <returns><see langword="true"/> when the candidate leads with the same parameters and adds only optional ones.</returns>
    private static bool AcceptsSameArguments(ImmutableArray<IParameterSymbol> sync, ImmutableArray<IParameterSymbol> candidate)
    {
        if (candidate.Length < sync.Length)
        {
            return false;
        }

        for (var i = 0; i < sync.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(sync[i].Type, candidate[i].Type)
                || sync[i].RefKind != candidate[i].RefKind)
            {
                return false;
            }
        }

        for (var i = sync.Length; i < candidate.Length; i++)
        {
            if (!candidate[i].IsOptional)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The task types resolved once per compilation.</summary>
    /// <param name="Task">The non-generic task type.</param>
    /// <param name="TaskOfT">The generic task type, when the framework has one.</param>
    /// <param name="ValueTask">The non-generic value-task type, when the framework has one.</param>
    /// <param name="ValueTaskOfT">The generic value-task type, when the framework has one.</param>
    internal readonly record struct TaskTypes(
        INamedTypeSymbol Task,
        INamedTypeSymbol? TaskOfT,
        INamedTypeSymbol? ValueTask,
        INamedTypeSymbol? ValueTaskOfT)
    {
        /// <summary>The metadata name of the non-generic task type.</summary>
        private const string TaskMetadataName = "System.Threading.Tasks.Task";

        /// <summary>The metadata name of the generic task type.</summary>
        private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";

        /// <summary>The metadata name of the non-generic value-task type.</summary>
        private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";

        /// <summary>The metadata name of the generic value-task type.</summary>
        private const string ValueTaskOfTMetadataName = "System.Threading.Tasks.ValueTask`1";

        /// <summary>Resolves the task types from a compilation, once.</summary>
        /// <param name="compilation">The compilation being analyzed.</param>
        /// <returns>The resolved task types, or <see langword="null"/> when the framework has no task type at all.</returns>
        public static TaskTypes? Create(Compilation compilation)
            => compilation.GetTypeByMetadataName(TaskMetadataName) is { } task
                ? new TaskTypes(
                    task,
                    compilation.GetTypeByMetadataName(TaskOfTMetadataName),
                    compilation.GetTypeByMetadataName(ValueTaskMetadataName),
                    compilation.GetTypeByMetadataName(ValueTaskOfTMetadataName))
                : null;
    }
}
