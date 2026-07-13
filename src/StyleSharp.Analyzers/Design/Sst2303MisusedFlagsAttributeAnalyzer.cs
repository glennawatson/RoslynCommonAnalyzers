// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum marked <c>[Flags]</c> whose members are not distinct bit values (SST2303).
/// </summary>
/// <remarks>
/// <para>
/// A member passes when it is one of three things: <b>zero</b> (the empty set — <c>None = 0</c> is
/// expected, not a violation); a <b>single bit</b>; or a <b>declared combination</b> — a member with an
/// explicit initializer whose value is made up entirely of bits the enum already declares, whether it is
/// written as <c>ReadWrite = Read | Write</c> or as the literal <c>3</c>. Anything else is reported.
/// </para>
/// <para>
/// The explicit initializer is what separates a deliberate combination from the bug the rule is here for.
/// <c>[Flags] enum Status { None, Active, Pending, Done }</c> looks harmless and is not: the compiler
/// numbers those 0, 1, 2, 3, so <c>Done</c> silently becomes <c>Active | Pending</c>, prints as
/// <c>"Active, Pending"</c>, and matches a <c>HasFlag(Done)</c> test on a value that is not <c>Done</c>.
/// Nobody wrote that 3 — the compiler counted to it. A 3 someone typed, on an enum that already declares
/// bits 1 and 2, is the combination the attribute exists for and is left alone. A value carrying a bit no
/// member declares (<c>Mixed = 5</c> with only 1 and 2 declared) can never be a combination of anything,
/// and is reported however it was written.
/// </para>
/// <para>
/// One diagnostic per enum, on the enum. The clean path is a type-kind check, so a compilation's
/// non-enum types cost one comparison each, and an enum without the attribute costs one attribute scan.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2303MisusedFlagsAttributeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The unqualified name of the attribute that promises the members combine.</summary>
    private const string FlagsAttributeName = "FlagsAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.MisusedFlagsAttribute);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Checks that every member of a flags enum owns a bit, or is built from ones that do.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Enum || !HasFlagsAttribute(type) || type.Locations.Length == 0 || !type.Locations[0].IsInSource)
        {
            return;
        }

        var members = type.GetMembers();
        var declaredBits = GetDeclaredSingleBits(members);
        for (var i = 0; i < members.Length; i++)
        {
            if (IsBadMember(members[i], declaredBits, context.CancellationToken))
            {
                context.ReportDiagnostic(Diagnostic.Create(DesignRules.MisusedFlagsAttribute, type.Locations[0], type.Name));
                return;
            }
        }
    }

    /// <summary>Returns whether a type carries the <c>[Flags]</c> attribute.</summary>
    /// <param name="type">The enum to test.</param>
    /// <returns><see langword="true"/> when the type promises its members combine with bitwise or.</returns>
    private static bool HasFlagsAttribute(INamedTypeSymbol type)
    {
        var attributes = type.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass is { Name: FlagsAttributeName } attribute
                && attribute.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the mask of every bit the enum's single-bit members own.</summary>
    /// <param name="members">The enum's members.</param>
    /// <returns>The union of the enum's single-bit values.</returns>
    private static ulong GetDeclaredSingleBits(ImmutableArray<ISymbol> members)
    {
        var declaredBits = 0UL;
        for (var i = 0; i < members.Length; i++)
        {
            if (TryGetValue(members[i], out var value) && IsSingleBit(value))
            {
                declaredBits |= value;
            }
        }

        return declaredBits;
    }

    /// <summary>Returns whether one member breaks the promise the attribute makes.</summary>
    /// <param name="member">The candidate member.</param>
    /// <param name="declaredBits">The union of the enum's single-bit values.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the member is neither zero, nor a bit, nor a declared combination.</returns>
    private static bool IsBadMember(ISymbol member, ulong declaredBits, CancellationToken cancellationToken)
    {
        if (!TryGetValue(member, out var value) || value == 0 || IsSingleBit(value))
        {
            return false;
        }

        // A combination has to be spelled out. The same value the compiler arrives at by counting is the
        // bug this rule exists for, and it is indistinguishable from a deliberate combination by value alone.
        return (value & ~declaredBits) != 0 || !HasExplicitValue(member, cancellationToken);
    }

    /// <summary>Reads a member's constant value as a bit pattern.</summary>
    /// <param name="member">The candidate member.</param>
    /// <param name="value">The member's value, reinterpreted as bits.</param>
    /// <returns><see langword="true"/> when the member is an enum member with a constant value.</returns>
    /// <remarks>
    /// The underlying type can be any integral type, signed or not. Every value is read as the bit pattern
    /// it actually stores, so a negative member on a signed enum is measured on its bits rather than its
    /// arithmetic value — which is what <c>HasFlag</c> and <c>ToString</c> do too.
    /// </remarks>
    private static bool TryGetValue(ISymbol member, out ulong value)
    {
        if (member is not IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant })
        {
            value = 0;
            return false;
        }

        value = constant is ulong bits ? bits : unchecked((ulong)Convert.ToInt64(constant, CultureInfo.InvariantCulture));
        return true;
    }

    /// <summary>Returns whether a member was given its value rather than counted into it.</summary>
    /// <param name="member">The enum member.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the declaration carries an initializer.</returns>
    private static bool HasExplicitValue(ISymbol member, CancellationToken cancellationToken)
    {
        var references = member.DeclaringSyntaxReferences;
        return references.Length != 0
            && references[0].GetSyntax(cancellationToken) is EnumMemberDeclarationSyntax { EqualsValue: not null };
    }

    /// <summary>Returns whether a value owns exactly one bit.</summary>
    /// <param name="value">The member's value.</param>
    /// <returns><see langword="true"/> for a power of two.</returns>
    private static bool IsSingleBit(ulong value) => value != 0 && (value & (value - 1)) == 0;
}
