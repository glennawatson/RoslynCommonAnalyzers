// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Recognizes a call on <c>System.GC</c>, shared by the rules that report garbage-collector calls (PSH1008,
/// PSH1021): a free syntax gate on the written receiver, then a binding check against the resolved type.
/// </summary>
internal static class GcInvocation
{
    /// <summary>The simple name of the garbage-collector type the syntax gate accepts.</summary>
    private const string GcTypeName = "GC";

    /// <summary>Returns whether a member access's receiver is written as <c>GC</c>, qualified or not.</summary>
    /// <param name="receiver">The receiver of the invoked member access.</param>
    /// <returns><see langword="true"/> for <c>GC</c>, <c>System.GC</c>, and <c>global::GC</c>.</returns>
    internal static bool IsGcReceiver(ExpressionSyntax receiver) => SyntaxNames.GetMemberName(receiver) == GcTypeName;

    /// <summary>Returns whether an invocation binds to a method declared on the resolved garbage-collector type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="gcType">The compilation's <c>System.GC</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a <c>System.GC</c> method, so a same-named user type never matches.</returns>
    internal static bool BindsToGcMethod(SemanticModel model, InvocationExpressionSyntax invocation, INamedTypeSymbol gcType, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, gcType);
}
