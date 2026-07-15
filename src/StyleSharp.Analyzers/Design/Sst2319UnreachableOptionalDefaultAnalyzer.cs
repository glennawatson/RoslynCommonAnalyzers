// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an optional parameter whose default value can never bind, because a sibling overload of the same
/// name takes exactly the parameters that come before it (SST2319). Every call that omits the optional
/// argument matches the shorter overload, so overload resolution binds that one and the default is dead API
/// surface — visible in completion and documentation, reachable by no caller.
/// </summary>
/// <remarks>
/// <para>
/// The rule looks at each ordinary method that declares an optional parameter, takes the required parameters
/// before the first optional one as a prefix, and searches the type's same-named methods for one whose whole
/// parameter list is exactly that prefix — same count, same types, same ref kinds. When such an overload
/// exists, the optional parameter is reported at its own location.
/// </para>
/// <para>
/// A method with no optional parameter — nearly every method — is skipped before any overload set is scanned,
/// so the clean path costs a single parameter walk per method and nothing more.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2319UnreachableOptionalDefaultAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.UnreachableOptionalDefault);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports each optional default a shorter same-named overload always shadows.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary)
            {
                continue;
            }

            var firstOptional = FirstOptionalParameterIndex(method.Parameters);
            if (firstOptional < 0 || !HasExactPrefixOverload(type, method, firstOptional))
            {
                continue;
            }

            var parameter = method.Parameters[firstOptional];
            if (parameter.Locations.Length == 0)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                DesignRules.UnreachableOptionalDefault,
                parameter.Locations[0],
                parameter.Name,
                BuildPrefixText(method.Parameters, firstOptional)));
        }
    }

    /// <summary>Returns the index of the first parameter carrying an explicit default, or -1 when none does.</summary>
    /// <param name="parameters">The method's parameters.</param>
    /// <returns>The zero-based index of the first optional parameter, or -1.</returns>
    /// <remarks>
    /// A <c>params</c> array is not optional in this sense — it has no explicit default — so it is skipped and
    /// never triggers a report.
    /// </remarks>
    private static int FirstOptionalParameterIndex(ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].HasExplicitDefaultValue)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns whether a same-named overload takes exactly the parameters before the first optional one.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="method">The method whose optional default is under test.</param>
    /// <param name="prefixLength">The number of required parameters before the first optional one.</param>
    /// <returns><see langword="true"/> when an overload consumes every call that omits the optional argument.</returns>
    private static bool HasExactPrefixOverload(INamedTypeSymbol type, IMethodSymbol method, int prefixLength)
    {
        var siblings = type.GetMembers(method.Name);
        for (var i = 0; i < siblings.Length; i++)
        {
            if (siblings[i] is not IMethodSymbol other
                || other.MethodKind != MethodKind.Ordinary
                || SymbolEqualityComparer.Default.Equals(other, method)
                || other.Parameters.Length != prefixLength)
            {
                continue;
            }

            if (PrefixMatches(method.Parameters, other.Parameters, prefixLength))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether two parameter lists agree on type and ref kind across a shared prefix.</summary>
    /// <param name="parameters">The parameters of the method with the optional default.</param>
    /// <param name="candidate">The parameters of the shorter overload.</param>
    /// <param name="prefixLength">The number of leading parameters to compare.</param>
    /// <returns><see langword="true"/> when every compared parameter matches.</returns>
    private static bool PrefixMatches(
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<IParameterSymbol> candidate,
        int prefixLength)
    {
        for (var i = 0; i < prefixLength; i++)
        {
            if (parameters[i].RefKind != candidate[i].RefKind
                || !SymbolEqualityComparer.Default.Equals(parameters[i].Type, candidate[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Renders the required-prefix parameter types for the diagnostic message.</summary>
    /// <param name="parameters">The method's parameters.</param>
    /// <param name="prefixLength">The number of leading parameters the shorter overload takes.</param>
    /// <returns>A comma-separated list of the prefix parameter types.</returns>
    private static string BuildPrefixText(ImmutableArray<IParameterSymbol> parameters, int prefixLength)
    {
        if (prefixLength == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < prefixLength; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(parameters[i].Type.ToDisplayString());
        }

        return builder.ToString();
    }
}
