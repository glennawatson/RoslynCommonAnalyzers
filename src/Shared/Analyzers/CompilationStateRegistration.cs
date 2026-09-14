// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Registers an action that receives state built once per compilation: the compilation-start action creates
/// the state — typically a holder that resolves well-known symbols on first demand — and every callback in
/// that compilation shares it.
/// </summary>
/// <remarks>
/// The kinds are captured once, at registration, so starting a compilation allocates nothing beyond the state
/// and the one closure that hands it to the callback. The state factory runs when the compilation starts, so a
/// holder that defers its symbol lookups keeps deferring them.
/// </remarks>
internal static class CompilationStateRegistration
{
    /// <summary>Analyzes one syntax node with the compilation's shared state.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="state">The state created when the compilation started.</param>
    internal delegate void SyntaxNodeAction<TState>(in SyntaxNodeAnalysisContext context, TState state);

    /// <summary>Analyzes one symbol with the compilation's shared state.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="state">The state created when the compilation started.</param>
    internal delegate void SymbolAction<TState>(in SymbolAnalysisContext context, TState state);

    /// <summary>Registers a syntax node action that receives state created once per compilation.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The analysis context passed to <c>Initialize</c>.</param>
    /// <param name="createState">Creates the state when a compilation starts.</param>
    /// <param name="action">Analyzes each matching node.</param>
    /// <param name="kinds">The syntax kinds the action runs for.</param>
    internal static void RegisterSyntaxNodeAction<TState>(
        AnalysisContext context,
        Func<Compilation, TState> createState,
        SyntaxNodeAction<TState> action,
        params SyntaxKind[] kinds)
    {
        var syntaxKinds = ImmutableArrays.Of(kinds);
        context.RegisterCompilationStartAction(start =>
        {
            var state = createState(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => action(nodeContext, state), syntaxKinds);
        });
    }

    /// <summary>Registers a symbol action that receives state created once per compilation.</summary>
    /// <typeparam name="TState">The per-compilation state type.</typeparam>
    /// <param name="context">The analysis context passed to <c>Initialize</c>.</param>
    /// <param name="createState">Creates the state when a compilation starts.</param>
    /// <param name="action">Analyzes each matching symbol.</param>
    /// <param name="kinds">The symbol kinds the action runs for.</param>
    internal static void RegisterSymbolAction<TState>(
        AnalysisContext context,
        Func<Compilation, TState> createState,
        SymbolAction<TState> action,
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
