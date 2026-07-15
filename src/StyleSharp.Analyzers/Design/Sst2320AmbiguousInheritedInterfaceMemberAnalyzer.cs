// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an interface that inherits a member of the same name and signature from two different base
/// interfaces that do not share it through a common root (SST2320). The interface compiles, but every
/// consumer that accesses the member through it gets a hard compiler ambiguity error, because the compiler
/// cannot choose between the two declarations. The rule moves that failure to the author of the interface.
/// </summary>
/// <remarks>
/// <para>
/// A diamond where both members trace to a single shared base interface is fine — there is only one member,
/// reached by two paths — and is not reported. An interface that re-declares the member itself with
/// <c>new</c> has already resolved the ambiguity, so it is left alone; that re-declaration is the fix the
/// diagnostic points at.
/// </para>
/// <para>
/// The rule only runs for an interface with at least two direct base interfaces, so nearly every type pays
/// nothing. Members are matched by kind, name, and signature, and only two <em>distinct</em> declarations
/// under one signature raise the diagnostic.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2320AmbiguousInheritedInterfaceMemberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>An interface needs at least this many direct base interfaces to inherit a member ambiguously.</summary>
    private const int MinimumBaseInterfaces = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.AmbiguousInheritedInterfaceMember);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports each member an interface inherits ambiguously from two unrelated base interfaces.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Interface
            || type.Interfaces.Length < MinimumBaseInterfaces
            || type.Locations.Length == 0
            || !type.Locations[0].IsInSource)
        {
            return;
        }

        ReportAmbiguousMembers(context, type);
    }

    /// <summary>Walks the interface's base members and reports each signature declared by two of them.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The interface under analysis.</param>
    private static void ReportAmbiguousMembers(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        var declaredNames = CollectDeclaredMemberNames(type);
        var firstByKey = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        var reportedKeys = new HashSet<string>(StringComparer.Ordinal);

        var baseInterfaces = type.AllInterfaces;
        for (var i = 0; i < baseInterfaces.Length; i++)
        {
            var members = baseInterfaces[i].GetMembers();
            for (var j = 0; j < members.Length; j++)
            {
                TryReportMember(context, type, members[j], declaredNames, firstByKey, reportedKeys);
            }
        }
    }

    /// <summary>Records a base member's signature, reporting the first time a second interface declares it.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="type">The interface under analysis.</param>
    /// <param name="member">The base-interface member to consider.</param>
    /// <param name="declaredNames">The names the interface re-declares itself, which resolve the ambiguity.</param>
    /// <param name="firstByKey">The first member seen for each signature key.</param>
    /// <param name="reportedKeys">The signature keys already reported, so each member reports once.</param>
    private static void TryReportMember(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ISymbol member,
        HashSet<string> declaredNames,
        Dictionary<string, ISymbol> firstByKey,
        HashSet<string> reportedKeys)
    {
        if (!IsEligibleMember(member) || declaredNames.Contains(member.Name))
        {
            return;
        }

        var key = BuildSignatureKey(member);
        if (!firstByKey.TryGetValue(key, out var first))
        {
            firstByKey[key] = member;
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(first, member) || !reportedKeys.Add(key))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.AmbiguousInheritedInterfaceMember,
            type.Locations[0],
            type.Name,
            member.Name,
            first.ContainingType.Name,
            member.ContainingType.Name));
    }

    /// <summary>Collects the names of the members the interface declares itself, including any re-declarations.</summary>
    /// <param name="type">The interface under analysis.</param>
    /// <returns>The set of member names declared directly on the interface.</returns>
    private static HashSet<string> CollectDeclaredMemberNames(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < members.Length; i++)
        {
            names.Add(members[i].Name);
        }

        return names;
    }

    /// <summary>Returns whether a member is one whose duplicate inheritance would confuse a caller.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><see langword="true"/> for an ordinary method, a property, or an event.</returns>
    /// <remarks>Accessor methods are skipped so a property is reported once rather than three times.</remarks>
    private static bool IsEligibleMember(ISymbol member)
        => member switch
        {
            IMethodSymbol method => method.MethodKind == MethodKind.Ordinary,
            IPropertySymbol => true,
            IEventSymbol => true,
            _ => false,
        };

    /// <summary>Builds a key identifying a member by kind, name, and signature.</summary>
    /// <param name="member">The member to key.</param>
    /// <returns>The signature key.</returns>
    private static string BuildSignatureKey(ISymbol member)
    {
        var builder = new StringBuilder();
        switch (member)
        {
            case IMethodSymbol method:
            {
                builder.Append("M:").Append(method.Name).Append(':').Append(method.TypeParameters.Length).Append(':');
                AppendParameters(builder, method.Parameters);
                break;
            }

            case IPropertySymbol property:
            {
                builder.Append("P:").Append(property.Name).Append(':').Append(property.Type.ToDisplayString()).Append(':');
                AppendParameters(builder, property.Parameters);
                break;
            }

            case IEventSymbol @event:
            {
                builder.Append("E:").Append(@event.Name).Append(':').Append(@event.Type.ToDisplayString());
                break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Appends the type and ref kind of each parameter to a signature key.</summary>
    /// <param name="builder">The key under construction.</param>
    /// <param name="parameters">The parameters to append.</param>
    private static void AppendParameters(StringBuilder builder, ImmutableArray<IParameterSymbol> parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            builder.Append((int)parameters[i].RefKind).Append(' ').Append(parameters[i].Type.ToDisplayString()).Append(',');
        }
    }
}
