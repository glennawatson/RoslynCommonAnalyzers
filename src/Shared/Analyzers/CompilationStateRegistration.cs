// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Registers analyzer actions that share state created once per compilation.</summary>
internal static class CompilationStateRegistration
{
    /// <summary>Registers a syntax node action that receives state created when each compilation starts.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The analysis context passed to <c>Initialize</c>.</param>
    /// <param name="createState">Creates the state for a compilation.</param>
    /// <param name="action">Analyzes each matching node.</param>
    /// <param name="kinds">The syntax kinds the action runs for.</param>
    internal static void RegisterSyntaxNodeAction<TState>(
        AnalysisContext context,
        Func<Compilation, TState> createState,
        ActionIn<SyntaxNodeAnalysisContext, TState> action,
        params SyntaxKind[] kinds)
    {
        var syntaxKinds = ImmutableArrays.Of(kinds);
        context.RegisterCompilationStartAction(start =>
        {
            var state = createState(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => action(nodeContext, state), syntaxKinds);
        });
    }

    /// <summary>Registers two syntax node actions that share state created when each compilation starts.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The analysis context passed to <c>Initialize</c>.</param>
    /// <param name="createState">Creates the state for a compilation.</param>
    /// <param name="first">The first action and its syntax kinds.</param>
    /// <param name="second">The second action and its syntax kinds.</param>
    internal static void RegisterSyntaxNodeActions<TState>(
        AnalysisContext context,
        Func<Compilation, TState> createState,
        SyntaxNodeRegistration<TState> first,
        SyntaxNodeRegistration<TState> second)
    {
        var firstAction = first.Action;
        var firstKinds = ImmutableArrays.Of(first.Kinds);
        var secondAction = second.Action;
        var secondKinds = ImmutableArrays.Of(second.Kinds);
        context.RegisterCompilationStartAction(start =>
        {
            var state = createState(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => firstAction(nodeContext, state), firstKinds);
            start.RegisterSyntaxNodeAction(nodeContext => secondAction(nodeContext, state), secondKinds);
        });
    }

    /// <summary>Registers three syntax node actions that share state created when each compilation starts.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The analysis context passed to <c>Initialize</c>.</param>
    /// <param name="createState">Creates the state for a compilation.</param>
    /// <param name="first">The first action and its syntax kinds.</param>
    /// <param name="second">The second action and its syntax kinds.</param>
    /// <param name="third">The third action and its syntax kinds.</param>
    internal static void RegisterSyntaxNodeActions<TState>(
        AnalysisContext context,
        Func<Compilation, TState> createState,
        SyntaxNodeRegistration<TState> first,
        SyntaxNodeRegistration<TState> second,
        SyntaxNodeRegistration<TState> third)
    {
        var firstAction = first.Action;
        var firstKinds = ImmutableArrays.Of(first.Kinds);
        var secondAction = second.Action;
        var secondKinds = ImmutableArrays.Of(second.Kinds);
        var thirdAction = third.Action;
        var thirdKinds = ImmutableArrays.Of(third.Kinds);
        context.RegisterCompilationStartAction(start =>
        {
            var state = createState(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => firstAction(nodeContext, state), firstKinds);
            start.RegisterSyntaxNodeAction(nodeContext => secondAction(nodeContext, state), secondKinds);
            start.RegisterSyntaxNodeAction(nodeContext => thirdAction(nodeContext, state), thirdKinds);
        });
    }

    /// <summary>Registers a symbol action that receives state created when each compilation starts.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The analysis context passed to <c>Initialize</c>.</param>
    /// <param name="createState">Creates the state for a compilation.</param>
    /// <param name="action">Analyzes each matching symbol.</param>
    /// <param name="kinds">The symbol kinds the action runs for.</param>
    internal static void RegisterSymbolAction<TState>(
        AnalysisContext context,
        Func<Compilation, TState> createState,
        ActionIn<SymbolAnalysisContext, TState> action,
        params SymbolKind[] kinds)
    {
        var symbolKinds = ImmutableArrays.Of(kinds);
        context.RegisterCompilationStartAction(start =>
        {
            var state = createState(start.Compilation);
            start.RegisterSymbolAction(symbolContext => action(symbolContext, state), symbolKinds);
        });
    }
}
