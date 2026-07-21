// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The component-model type of one compilation, resolved once and shared by the rules that reason about a
/// component's render lifecycle: whether a declared type is a rendered component, and whether an invocation
/// is the framework's request-a-rerender call. It carries the <c>ComponentBase</c> symbol so a project that
/// references no component assembly registers nothing and pays nothing.
/// </summary>
/// <param name="ComponentBase">The <c>ComponentBase</c> type every rendered component derives from.</param>
internal readonly record struct BlazorComponentModel(INamedTypeSymbol ComponentBase)
{
    /// <summary>The metadata name of the base type every rendered component derives from.</summary>
    public const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

    /// <summary>The name of the method that asks the renderer to re-render the component.</summary>
    public const string StateHasChangedName = "StateHasChanged";

    /// <summary>Resolves the component model for a compilation, or nothing when it references no component assembly.</summary>
    /// <param name="compilation">The compilation to resolve against.</param>
    /// <returns>The resolved model, or <see langword="null"/> when <c>ComponentBase</c> is not present.</returns>
    public static BlazorComponentModel? Create(Compilation compilation)
        => compilation.GetTypeByMetadataName(ComponentBaseMetadataName) is { } componentBase
            ? new BlazorComponentModel(componentBase)
            : null;

    /// <summary>Returns whether the six render-lifecycle method names carry <paramref name="name"/>.</summary>
    /// <param name="name">The method name to test.</param>
    /// <returns><see langword="true"/> for an <c>OnInitialized</c>, <c>OnParametersSet</c>, or <c>OnAfterRender</c> method (sync or async).</returns>
    public static bool IsLifecycleMethodName(string name) => name switch
    {
        "OnInitialized" or "OnInitializedAsync"
            or "OnParametersSet" or "OnParametersSetAsync"
            or "OnAfterRender" or "OnAfterRenderAsync" => true,
        _ => false,
    };

    /// <summary>Returns whether an invocation's callee syntactically names the component's own <c>StateHasChanged</c>.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns><see langword="true"/> for <c>StateHasChanged()</c>, <c>this.StateHasChanged()</c>, or <c>base.StateHasChanged()</c>.</returns>
    public static bool IsSelfStateHasChangedSyntax(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax { Identifier.ValueText: StateHasChangedName } => true,
        MemberAccessExpressionSyntax { Name.Identifier.ValueText: StateHasChangedName, Expression: ThisExpressionSyntax or BaseExpressionSyntax } => true,
        _ => false,
    };

    /// <summary>Returns whether a type is, or derives from, <c>ComponentBase</c>.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is a rendered component.</returns>
    public bool DerivesFromComponentBase(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, ComponentBase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a resolved method is <c>ComponentBase.StateHasChanged</c>.</summary>
    /// <param name="method">The method symbol to test.</param>
    /// <returns><see langword="true"/> when the method is the framework's request-a-rerender call.</returns>
    public bool IsStateHasChanged(IMethodSymbol? method)
        => method is { Name: StateHasChangedName }
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, ComponentBase);
}
