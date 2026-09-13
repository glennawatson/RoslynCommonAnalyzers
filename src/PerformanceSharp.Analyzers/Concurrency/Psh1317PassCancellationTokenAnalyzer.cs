// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a call that drops a cancellation token the call site is already holding (PSH1317):
/// <c>await client.GetAsync(url)</c> inside a method that took a <c>CancellationToken</c>, or
/// <c>await reader.ReadAsync()</c> where the token parameter was left at its default. Cancellation
/// stops at that call, so the work behind it runs to completion after the caller has given up —
/// holding a thread-pool worker, a connection, or a handle for a result nobody will read.
/// </summary>
/// <remarks>
/// <para>
/// The receiving method is never guessed. Either the called method has a token parameter the call left
/// empty, or an overload on the same type takes one and provably accepts the arguments already written
/// — see <see cref="CancellationTokenOverload"/>. A call whose framework has no cancellable form is
/// therefore never reported, and a call that already passes a token, including an explicit
/// <c>CancellationToken.None</c>, is left alone: that is a deliberate opt-out.
/// </para>
/// <para>
/// The clean path is a syntactic walk out of the call to the nearest function parameter declared as a
/// <c>CancellationToken</c>. A call with nothing to pass never reaches the semantic model, and the
/// overload search behind a call that does is memoized per method symbol.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1317PassCancellationTokenAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ConcurrencyRules.PassCancellationToken);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var state = new TokenAnalysisState(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, state), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1317 when a call could carry a token that is in scope, and does not.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="state">The deferred token type and overload cache for this compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, TokenAnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (CancellationTokenScope.TryFindInScope(invocation) is null
            || state.GetTokenType() is not { } tokenType
            || CancellationTokenOverload.TryFind(context.SemanticModel, invocation, tokenType, state.GetTargets(), context.CancellationToken) is not { } forwardable)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.PassCancellationToken,
            invocation.SyntaxTree,
            invocation.Span,
            forwardable.TokenName,
            forwardable.Method.Name));
    }

    /// <summary>Defers token resolution and overload-cache allocation until a call has a token in scope.</summary>
    /// <param name="compilation">The compilation whose token type and overloads are cached.</param>
    private sealed class TokenAnalysisState(Compilation compilation)
    {
        /// <summary>The published token-type lookup result, including a missing type.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>The shared overload cache, allocated only when a token type is available.</summary>
        private ConcurrentDictionary<ISymbol, CancellationTokenOverload.TokenTarget?>? _targets;

        /// <summary>Gets the cached token type, resolving it on first use.</summary>
        /// <returns>The token type, or <see langword="null"/> when absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetTokenType() => (_resolved ??= [compilation.GetTypeByMetadataName(CancellationTokenScope.TokenMetadataName)])[0];

        /// <summary>Gets the single shared overload cache, publishing it atomically on first use.</summary>
        /// <returns>The per-compilation overload cache.</returns>
        public ConcurrentDictionary<ISymbol, CancellationTokenOverload.TokenTarget?> GetTargets()
        {
            var targets = Volatile.Read(ref _targets);
            if (targets is null)
            {
                targets = new(concurrencyLevel: 4, capacity: 31, SymbolEqualityComparer.Default);
                targets = Interlocked.CompareExchange(ref _targets, targets, null) ?? targets;
            }

            return targets;
        }
    }
}
