// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>ComponentBase</c> override of a synchronous lifecycle method — <c>OnInitialized</c>,
/// <c>OnParametersSet</c>, or <c>OnAfterRender</c> — declared <c>async void</c> (SST2711). The runtime calls
/// the synchronous method and moves on without a Task to await, so the awaited work runs fire-and-forget and an
/// exception thrown after the first await is unobserved; on an interactive server circuit that tears the circuit
/// down.
/// </summary>
/// <remarks>
/// The component marker is resolved once on first demand, after the cheap method flags and lifecycle name
/// match. A project without the marker stays silent. The override chain must reach a method declared on
/// <c>ComponentBase</c> itself, so a same-named method outside the framework contract is never reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2711AsyncVoidLifecycleOverrideAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The suffix that names each synchronous hook's Task-returning twin.</summary>
    private const string AsyncSuffix = "Async";

    /// <summary>The synchronous lifecycle hook run once when the component is initialized.</summary>
    private const string OnInitializedName = "OnInitialized";

    /// <summary>The synchronous lifecycle hook run when the component's parameters are set.</summary>
    private const string OnParametersSetName = "OnParametersSet";

    /// <summary>The synchronous lifecycle hook run after the component renders.</summary>
    private const string OnAfterRenderName = "OnAfterRender";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(FrameworksRules.AsyncVoidLifecycleOverride);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var componentBase = new ComponentBaseType(start.Compilation);
            start.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, componentBase), SymbolKind.Method);
        });
    }

    /// <summary>Reports a synchronous lifecycle override declared <c>async void</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="componentBase">The component base type resolved on first demand.</param>
    private static void AnalyzeMethod(in SymbolAnalysisContext context, ComponentBaseType componentBase)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary
            || !method.IsOverride
            || !method.IsAsync
            || !method.ReturnsVoid
            || !IsSynchronousLifecycleName(method.Name)
            || componentBase.Get() is not { } resolved
            || !OverridesComponentBaseMethod(method, resolved))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            FrameworksRules.AsyncVoidLifecycleOverride,
            method.Locations[0],
            method.Name,
            method.Name + AsyncSuffix));
    }

    /// <summary>Returns whether a name is one of the three synchronous lifecycle hooks.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when the name is a synchronous lifecycle hook.</returns>
    private static bool IsSynchronousLifecycleName(string name) =>
        string.Equals(name, OnInitializedName, StringComparison.Ordinal)
            || string.Equals(name, OnParametersSetName, StringComparison.Ordinal)
            || string.Equals(name, OnAfterRenderName, StringComparison.Ordinal);

    /// <summary>Returns whether a method's override chain reaches a method declared on <c>ComponentBase</c>.</summary>
    /// <param name="method">The overriding method.</param>
    /// <param name="componentBase">The resolved <c>ComponentBase</c> type.</param>
    /// <returns><see langword="true"/> when the framework hook itself is the root of the override chain.</returns>
    private static bool OverridesComponentBaseMethod(IMethodSymbol method, INamedTypeSymbol componentBase)
    {
        for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current.ContainingType, componentBase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the component marker once, only when a lifecycle override needs it.</summary>
    /// <param name="compilation">The compilation whose component marker is resolved.</param>
    private sealed class ComponentBaseType(Compilation compilation)
    {
        /// <summary>The metadata name of the component base type the lifecycle hooks are declared on.</summary>
        private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

        /// <summary>Serializes the first metadata lookup across symbol callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The resolved component marker, or null when it is absent.</summary>
        private INamedTypeSymbol? _componentBase;

        /// <summary>Indicates that the marker lookup, including an absent result, has completed.</summary>
        private bool _resolved;

        /// <summary>Gets the component marker without locking after the first lookup.</summary>
        /// <returns>The component marker, or null when the compilation does not define it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => Volatile.Read(ref _resolved) ? _componentBase : Resolve();

        /// <summary>Resolves and publishes the marker under the first-demand gate.</summary>
        /// <returns>The component marker, or null when the compilation does not define it.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol? Resolve()
        {
            lock (_gate)
            {
                if (!_resolved)
                {
                    _componentBase = compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
                    Volatile.Write(ref _resolved, true);
                }

                return _componentBase;
            }
        }
    }
}
