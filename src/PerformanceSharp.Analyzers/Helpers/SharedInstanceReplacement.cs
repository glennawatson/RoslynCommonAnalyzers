// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Decides whether an allocation can be rewritten into a read of a shared instance on its own type, shared
/// by the rules that replace a parameterless construction with a framework singleton (PSH1022, PSH1412).
/// </summary>
internal static class SharedInstanceReplacement
{
    /// <summary>Returns whether the fix can name the shared instance's type at the allocation's position.</summary>
    /// <param name="creation">The allocation being reported.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The compilation's type that owns the shared instance.</param>
    /// <param name="typeName">The simple name the fix writes for a target-typed <c>new()</c>.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a compiling replacement exists.</returns>
    /// <remarks>
    /// An explicit allocation already spells the type out, and the fix reuses exactly what the author wrote —
    /// a qualified name stays qualified. A target-typed <c>new()</c> spells nothing out, so the fix has to write
    /// the simple name itself, and that only compiles where the simple name resolves to the same type. Where it
    /// does not, the diagnostic would have no fix, so it is not reported.
    /// </remarks>
    internal static bool CanWriteReplacement(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel model,
        INamedTypeSymbol type,
        string typeName,
        CancellationToken cancellationToken)
    {
        if (creation is ObjectCreationExpressionSyntax { Type: NameSyntax })
        {
            return true;
        }

        if (creation is ObjectCreationExpressionSyntax)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var candidate in model.LookupNamespacesAndTypes(creation.SpanStart, name: typeName))
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, type))
            {
                return true;
            }
        }

        return false;
    }
}
