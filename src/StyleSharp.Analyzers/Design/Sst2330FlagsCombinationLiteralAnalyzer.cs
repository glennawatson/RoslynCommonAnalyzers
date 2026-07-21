// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>[Flags]</c> enum member assigned a bare number that equals a combination of the enum's own
/// single-bit members (SST2330). <c>All = 7</c>, on an enum that declares <c>Read = 1</c>, <c>Write = 2</c>,
/// <c>Execute = 4</c>, hides what the value is behind arithmetic the reader has to redo, and goes silently
/// wrong the moment one of those members is renumbered. Written as <c>Read | Write | Execute</c> it says what
/// it means and survives the change.
/// </summary>
/// <remarks>
/// The member is reported only when its literal decomposes exactly into two or more of the enum's single-bit
/// members — every bit of the literal owned by a declared bit, and nothing left over. A member already
/// written as <c>Read | Write</c>, one that is itself a single bit, and one carrying a bit no member declares
/// are all left alone. The clean path is a type-kind and attribute check, so only a flags enum pays anything.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2330FlagsCombinationLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the comma-separated member names the literal combines.</summary>
    internal const string MembersKey = "Members";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.FlagsCombinationLiteralShouldNameMembers);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports each flags-enum member whose literal hides a combination of other members.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Enum || !EnumFlagValues.HasFlagsAttribute(type))
        {
            return;
        }

        var members = type.GetMembers();
        var singleBits = CollectSingleBits(members);
        if (singleBits.Count < 2)
        {
            return;
        }

        for (var i = 0; i < members.Length; i++)
        {
            AnalyzeMember(context, members[i], singleBits);
        }
    }

    /// <summary>Reports one member when its literal decomposes into two or more declared single bits.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="member">The candidate member.</param>
    /// <param name="singleBits">The enum's single-bit members in declaration order.</param>
    private static void AnalyzeMember(SymbolAnalysisContext context, ISymbol member, List<SingleBitMember> singleBits)
    {
        if (!EnumFlagValues.TryGetValue(member, out var value)
            || value == 0
            || EnumFlagValues.IsSingleBit(value)
            || GetLiteralInitializer(member, context.CancellationToken) is not { } literal)
        {
            return;
        }

        var combined = 0UL;
        var names = new List<string>();
        for (var i = 0; i < singleBits.Count; i++)
        {
            var bit = singleBits[i];
            if ((bit.Value & value) == bit.Value)
            {
                combined |= bit.Value;
                names.Add(bit.Name);
            }
        }

        if (names.Count < 2 || combined != value)
        {
            return;
        }

        var display = string.Join(" | ", names);
        var properties = ImmutableDictionary<string, string?>.Empty.Add(MembersKey, string.Join(",", names));
        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.FlagsCombinationLiteralShouldNameMembers,
            literal.GetLocation(),
            properties,
            member.Name,
            display));
    }

    /// <summary>Collects the enum's single-bit members, in declaration order.</summary>
    /// <param name="members">The enum's members.</param>
    /// <returns>The single-bit members.</returns>
    private static List<SingleBitMember> CollectSingleBits(ImmutableArray<ISymbol> members)
    {
        var singleBits = new List<SingleBitMember>();
        for (var i = 0; i < members.Length; i++)
        {
            if (EnumFlagValues.TryGetValue(members[i], out var value) && EnumFlagValues.IsSingleBit(value))
            {
                singleBits.Add(new SingleBitMember(members[i].Name, value));
            }
        }

        return singleBits;
    }

    /// <summary>Gets a member's initializer when it is a plain numeric literal.</summary>
    /// <param name="member">The enum member.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The numeric-literal expression, or <see langword="null"/> when the initializer is anything else.</returns>
    private static LiteralExpressionSyntax? GetLiteralInitializer(ISymbol member, CancellationToken cancellationToken)
    {
        var references = member.DeclaringSyntaxReferences;
        if (references.Length == 0
            || references[0].GetSyntax(cancellationToken) is not EnumMemberDeclarationSyntax { EqualsValue.Value: { } value }
            || value is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            return null;
        }

        return literal;
    }

    /// <summary>One single-bit member of a flags enum.</summary>
    /// <param name="Name">The member's name.</param>
    /// <param name="Value">The member's single-bit value.</param>
    private readonly record struct SingleBitMember(string Name, ulong Value);
}
