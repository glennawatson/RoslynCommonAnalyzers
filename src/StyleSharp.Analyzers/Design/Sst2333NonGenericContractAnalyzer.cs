// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a publicly visible type that implements a generic comparison or equality contract but not its
/// non-generic counterpart (SST2333), so runtime code paths that still use the non-generic form silently skip
/// it. Reported for <c>IComparable&lt;T&gt;</c> without <c>IComparable</c>, <c>IComparer&lt;T&gt;</c> without
/// <c>IComparer</c>, <c>IEqualityComparer&lt;T&gt;</c> without <c>IEqualityComparer</c>, and
/// <c>IEquatable&lt;T&gt;</c> without an override of <c>object.Equals(object)</c>.
/// </summary>
/// <remarks>
/// This is opt-in and disabled by default: many modern types deliberately omit the non-generic contracts. The
/// rule resolves each non-generic counterpart in the compilation and stays silent when it is absent, so a fix
/// it offers always has a target. The clean path resolves the contracts once per compilation and does nothing
/// on a framework that has none of the generic forms.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2333NonGenericContractAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key naming which contract is missing.</summary>
    internal const string ContractKey = "Contract";

    /// <summary>The diagnostic property key carrying the fully-qualified generic type argument.</summary>
    internal const string TypeArgumentKey = "TypeArgument";

    /// <summary>The contract value for a type implementing <c>IComparable&lt;T&gt;</c> without <c>IComparable</c>.</summary>
    internal const string ComparableContract = "IComparable";

    /// <summary>The contract value for a type implementing <c>IComparer&lt;T&gt;</c> without <c>IComparer</c>.</summary>
    internal const string ComparerContract = "IComparer";

    /// <summary>The contract value for a type implementing <c>IEqualityComparer&lt;T&gt;</c> without <c>IEqualityComparer</c>.</summary>
    internal const string EqualityComparerContract = "IEqualityComparer";

    /// <summary>The contract value for a type implementing <c>IEquatable&lt;T&gt;</c> without an <c>object.Equals</c> override.</summary>
    internal const string EquatableContract = "IEquatable";

    /// <summary>The display form of the non-generic <c>object.Equals(object)</c> counterpart.</summary>
    private const string ObjectEqualsDisplay = "object.Equals(object)";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.MissingNonGenericContract);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var contracts = new ContractTypes(start.Compilation);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, contracts), SymbolKind.NamedType);
        });
    }

    /// <summary>Reports each generic contract on a type whose non-generic counterpart is missing.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="contractTypes">The comparison contracts resolved on first demand.</param>
    private static void Analyze(in SymbolAnalysisContext context, ContractTypes contractTypes)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || type.IsStatic
            || !SymbolVisibility.IsExternallyVisible(type)
            || type.Locations.IsEmpty
            || !type.Locations[0].IsInSource
            || type.AllInterfaces.IsEmpty
            || contractTypes.Get() is not { } contracts)
        {
            return;
        }

        ReportComparable(context, contracts, type);
        ReportForInterfaceCounterpart(context, type, contracts.ComparerOfT, contracts.Comparer, ComparerContract);
        ReportForInterfaceCounterpart(context, type, contracts.EqualityComparerOfT, contracts.EqualityComparer, EqualityComparerContract);
        ReportEquatable(context, contracts, type);
    }

    /// <summary>Reports <c>IComparable&lt;T&gt;</c> without the non-generic <c>IComparable</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="contracts">The comparison contracts resolved for the compilation.</param>
    /// <param name="type">The type being analyzed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReportComparable(in SymbolAnalysisContext context, in ComparisonContractTypes contracts, INamedTypeSymbol type) =>
        ReportForInterfaceCounterpart(context, type, contracts.ComparableOfT, contracts.Comparable, ComparableContract);

    /// <summary>Reports a generic interface contract whose non-generic interface counterpart is missing.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The type being analyzed.</param>
    /// <param name="genericContract">The unbound generic contract.</param>
    /// <param name="nonGenericContract">The non-generic interface counterpart.</param>
    /// <param name="contractName">The contract property value.</param>
    private static void ReportForInterfaceCounterpart(
        in SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol? genericContract,
        INamedTypeSymbol? nonGenericContract,
        string contractName)
    {
        if (nonGenericContract is null
            || ComparisonContractTypes.GetImplementedArgument(type, genericContract) is not { } argument
            || ComparisonContractTypes.Implements(type, nonGenericContract))
        {
            return;
        }

        Report(context, type, genericContract!, argument, nonGenericContract.ToDisplayString(), contractName);
    }

    /// <summary>Reports <c>IEquatable&lt;T&gt;</c> without an override of <c>object.Equals(object)</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="contracts">The comparison contracts resolved for the compilation.</param>
    /// <param name="type">The type being analyzed.</param>
    private static void ReportEquatable(in SymbolAnalysisContext context, in ComparisonContractTypes contracts, INamedTypeSymbol type)
    {
        if (ComparisonContractTypes.GetImplementedArgument(type, contracts.EquatableOfT) is not { } argument || OverridesObjectEquals(type))
        {
            return;
        }

        Report(context, type, contracts.EquatableOfT!, argument, ObjectEqualsDisplay, EquatableContract);
    }

    /// <summary>Reports one missing counterpart on a type.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The type being analyzed.</param>
    /// <param name="genericContract">The unbound generic contract the type implements.</param>
    /// <param name="argument">The type argument the contract is bound with.</param>
    /// <param name="counterpartDisplay">The display form of the missing counterpart.</param>
    /// <param name="contractName">The contract property value.</param>
    private static void Report(
        in SymbolAnalysisContext context,
        INamedTypeSymbol type,
        INamedTypeSymbol genericContract,
        ITypeSymbol argument,
        string counterpartDisplay,
        string contractName)
    {
        var boundContract = genericContract.Construct(argument);
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(ContractKey, contractName)
            .Add(TypeArgumentKey, argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.MissingNonGenericContract,
            type.Locations[0],
            properties,
            type.Name,
            boundContract.ToDisplayString(),
            counterpartDisplay));
    }

    /// <summary>Returns whether a type or a base overrides <c>object.Equals(object)</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when an override of <c>object.Equals(object)</c> is in effect.</returns>
    private static bool OverridesObjectEquals(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            var members = current.GetMembers(nameof(Equals));
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol { IsOverride: true, Parameters.Length: 1, ReturnType.SpecialType: SpecialType.System_Boolean } method
                    && method.Parameters[0].Type.SpecialType == SpecialType.System_Object)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Resolves comparison contracts only when a visible type implements interfaces.</summary>
    /// <param name="compilation">The compilation whose contracts are cached.</param>
    private sealed class ContractTypes(Compilation compilation)
    {
        /// <summary>The published result, including a missing set of contracts.</summary>
        private ComparisonContractTypes?[]? _resolved;

        /// <summary>Gets the contracts, resolving them on first demand.</summary>
        /// <returns>The contracts, or null when the framework has none.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComparisonContractTypes? Get() => (_resolved ??= [ComparisonContractTypes.Create(compilation)])[0];
    }
}
