// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a class that implements <c>IEquatable&lt;T&gt;</c> against itself while still allowing a
/// derived type to exist (SST2301).
/// </summary>
/// <remarks>
/// <para>
/// The contract is the problem, not the syntax. <c>IEquatable&lt;Money&gt;</c> says "I decide equality
/// against a <c>Money</c>", and the moment <c>Coin : Money</c> exists, <c>money.Equals(coin)</c> can
/// answer true through the base's field comparison while <c>coin.Equals(money)</c> answers false through
/// the derived one. Equality has stopped being symmetric, and every dictionary and set built on it now
/// depends on which operand happened to be on the left.
/// </para>
/// <para>
/// Skipped: <b>abstract</b> classes, because the type that has to keep the contract is the leaf, and an
/// abstract type that implements <c>IEquatable&lt;TSelf&gt;</c> is usually handing its concrete
/// descendants a shared field comparison rather than claiming to be the final word — the leaves are the
/// ones this rule wants sealed, and they are reported on their own. <b>Structs</b>, which cannot be
/// derived from, so the asymmetry cannot arise. <b>Records</b>, whose generated equality already carries
/// the type check (<c>EqualityContract</c>) that keeps a hierarchy honest. And a class implementing
/// <c>IEquatable&lt;TOther&gt;</c> for some other type, which makes no claim about itself.
/// </para>
/// <para>
/// The clean path is a walk of the interface list, which is empty for most types, and a name comparison
/// that costs no well-known-type lookup at all.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2301EquatableTypeShouldBeSealedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The unqualified name of the equality contract.</summary>
    private const string EquatableName = "IEquatable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.EquatableTypeShouldBeSealed);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports an open class that claims to decide equality against itself.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class
            || type.IsSealed
            || type.IsAbstract
            || type.IsStatic
            || type.IsRecord)
        {
            return;
        }

        if (!ImplementsEquatableOfSelf(type) || type.Locations.Length == 0 || !type.Locations[0].IsInSource)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DesignRules.EquatableTypeShouldBeSealed, type.Locations[0], type.Name));
    }

    /// <summary>Returns whether a type implements <c>IEquatable&lt;T&gt;</c> where <c>T</c> is the type itself.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type signs the equality contract for itself.</returns>
    /// <remarks>
    /// The interface is matched on its name and namespace rather than a well-known-type lookup, so a
    /// compilation whose types never mention equality never pays for resolving one.
    /// </remarks>
    private static bool ImplementsEquatableOfSelf(INamedTypeSymbol type)
    {
        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidate = interfaces[i];
            if (candidate.TypeArguments.Length != 1
                || !string.Equals(candidate.Name, EquatableName, StringComparison.Ordinal)
                || !IsSystemNamespace(candidate.ContainingNamespace))
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a namespace is the global <c>System</c> namespace.</summary>
    /// <param name="containingNamespace">The namespace to test.</param>
    /// <returns><see langword="true"/> for <c>System</c> and nothing else.</returns>
    private static bool IsSystemNamespace(INamespaceSymbol? containingNamespace)
        => containingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true };
}
