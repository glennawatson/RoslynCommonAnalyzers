// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member of a <c>[Flags]</c> enum whose value sets a bit that no single-bit member of the same
/// enum declares (SST2461): <c>All = 7</c> where only <c>1</c> and <c>2</c> exist owns a bit belonging to
/// nothing.
/// </summary>
/// <remarks>
/// The enum's declared bits are collected in one pass and reused for every member, so an enum is walked
/// twice however many members it has, and an enum without the attribute is rejected before either pass.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2461UndefinedFlagInCompositeValueAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.UndefinedFlagInCompositeValue);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Reports every member of one flags enum that sets an undeclared bit.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;
        if (!Sst1222EnumMemberOrderAnalyzer.IsFlagsEnum(declaration))
        {
            return;
        }

        var members = declaration.Members;
        var declaredBits = 0UL;
        for (var i = 0; i < members.Count; i++)
        {
            if (TryGetValue(context, members[i]) is { } value && IsSingleBit(value))
            {
                declaredBits |= value;
            }
        }

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (TryGetValue(context, member) is not { } value || value == 0 || IsSingleBit(value))
            {
                continue;
            }

            var undefined = value & ~declaredBits;
            if (undefined == 0)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.UndefinedFlagInCompositeValue,
                member.SyntaxTree,
                member.Identifier.Span,
                member.Identifier.ValueText,
                LowestBitIndex(undefined).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>Gets an enum member's declared value as a bit pattern.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="member">The enum member.</param>
    /// <returns>The value's bits, or <see langword="null"/> when it has no constant value.</returns>
    /// <remarks>
    /// The value is read as an unsigned bit pattern because that is what a flags comparison cares about; a
    /// negative signed value simply sets its high bits.
    /// </remarks>
    private static ulong? TryGetValue(SyntaxNodeAnalysisContext context, EnumMemberDeclarationSyntax member)
    {
        if (context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { ConstantValue: { } constant })
        {
            return null;
        }

        try
        {
            return constant switch
            {
                ulong value => value,
                _ => unchecked((ulong)Convert.ToInt64(constant, System.Globalization.CultureInfo.InvariantCulture)),
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>Returns whether a value has exactly one bit set.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when the value names a single flag.</returns>
    private static bool IsSingleBit(ulong value) => value != 0 && (value & (value - 1)) == 0;

    /// <summary>Gets the index of the lowest set bit.</summary>
    /// <param name="value">A non-zero value.</param>
    /// <returns>The zero-based bit index.</returns>
    private static int LowestBitIndex(ulong value)
    {
        var index = 0;
        while ((value & 1) == 0)
        {
            value >>= 1;
            index++;
        }

        return index;
    }
}
