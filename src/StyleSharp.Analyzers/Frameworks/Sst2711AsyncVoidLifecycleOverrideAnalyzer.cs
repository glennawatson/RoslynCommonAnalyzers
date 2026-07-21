// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>ComponentBase</c> override of a synchronous lifecycle method — <c>OnInitialized</c>,
/// <c>OnParametersSet</c>, or <c>OnAfterRender</c> — declared <c>async void</c> (SST2711). The runtime calls
/// the synchronous method and moves on without a Task to await, so the awaited work runs fire-and-forget and an
/// exception thrown after the first await is unobserved; on an interactive server circuit that tears the circuit
/// down.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on the <c>Microsoft.AspNetCore.Components.ComponentBase</c>
/// marker resolving; a project that references no component assembly registers nothing and pays nothing. The
/// clean path is symbol-only and short-circuits on cheap flags first — an ordinary method that is an
/// <c>override</c>, is <c>async</c>, and returns <c>void</c> — before the name is compared and the overridden
/// chain is walked to confirm the method it overrides is defined on <c>ComponentBase</c> itself, so a
/// same-named method that does not actually override the framework hook is never reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2711AsyncVoidLifecycleOverrideAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the component base type the lifecycle hooks are declared on.</summary>
    private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

    /// <summary>The suffix that names each synchronous hook's Task-returning twin.</summary>
    private const string AsyncSuffix = "Async";

    /// <summary>The synchronous lifecycle hook run once when the component is initialized.</summary>
    private const string OnInitializedName = "OnInitialized";

    /// <summary>The synchronous lifecycle hook run when the component's parameters are set.</summary>
    private const string OnParametersSetName = "OnParametersSet";

    /// <summary>The synchronous lifecycle hook run after the component renders.</summary>
    private const string OnAfterRenderName = "OnAfterRender";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.AsyncVoidLifecycleOverride);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var componentBase = start.Compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
            if (componentBase is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, componentBase), SymbolKind.Method);
        });
    }

    /// <summary>Reports a synchronous lifecycle override declared <c>async void</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="componentBase">The resolved <c>ComponentBase</c> type.</param>
    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol componentBase)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary
            || !method.IsOverride
            || !method.IsAsync
            || !method.ReturnsVoid
            || !IsSynchronousLifecycleName(method.Name)
            || !OverridesComponentBaseMethod(method, componentBase))
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
    private static bool IsSynchronousLifecycleName(string name)
        => string.Equals(name, OnInitializedName, StringComparison.Ordinal)
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
}
