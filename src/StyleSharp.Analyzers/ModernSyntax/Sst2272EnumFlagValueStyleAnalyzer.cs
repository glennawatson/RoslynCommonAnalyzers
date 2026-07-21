// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a single-bit member of a <c>[Flags]</c> enum whose value is written in the form the codebase did
/// not choose (SST2272): a decimal literal when <c>stylesharp.enum_flag_value_style</c> is <c>shift</c> (the
/// default), or a <c>1 &lt;&lt; n</c> shift when it is <c>decimal</c>. The rule is opt-in and off by default.
/// </summary>
/// <remarks>
/// Only a member whose constant value is a single set bit up to <c>1 &lt;&lt; 30</c> is a candidate; a combined
/// value such as <c>3</c>, a zero, and a member with no explicit value are all left alone. The enum's
/// <c>[Flags]</c> attribute and each member's constant value are resolved only for a candidate shape.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2272EnumFlagValueStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property carrying the replacement expression text.</summary>
    internal const string ReplacementKey = "Replacement";

    /// <summary>The highest single-bit position the rule normalizes, keeping the shift within a positive <c>int</c>.</summary>
    private const int MaxShift = 30;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.NormalizeEnumFlagValueStyle);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Returns whether an expression is a canonical <c>1 &lt;&lt; n</c> single-flag shift.</summary>
    /// <param name="expression">The value expression to inspect.</param>
    /// <returns><see langword="true"/> when it left-shifts the literal <c>1</c> by a literal amount.</returns>
    internal static bool IsSingleFlagShift(ExpressionSyntax expression)
        => expression is BinaryExpressionSyntax shift
            && shift.IsKind(SyntaxKind.LeftShiftExpression)
            && shift.Left is LiteralExpressionSyntax { Token.Value: 1 } left
            && left.IsKind(SyntaxKind.NumericLiteralExpression)
            && shift.Right is LiteralExpressionSyntax right
            && right.IsKind(SyntaxKind.NumericLiteralExpression);

    /// <summary>Reports the members of a <c>[Flags]</c> enum whose value form does not match the configured one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var enumDeclaration = (EnumDeclarationSyntax)context.Node;
        if (context.Compilation.GetTypeByMetadataName("System.FlagsAttribute") is not { } flagsAttribute
            || context.SemanticModel.GetDeclaredSymbol(enumDeclaration, context.CancellationToken) is not { } enumSymbol
            || !HasFlagsAttribute(enumSymbol, flagsAttribute))
        {
            return;
        }

        var style = ModernSyntaxStyleOptions.ReadEnumFlagValueStyle(context.Options.AnalyzerConfigOptionsProvider.GetOptions(enumDeclaration.SyntaxTree));
        foreach (var member in enumDeclaration.Members)
        {
            AnalyzeMember(context, member, style);
        }
    }

    /// <summary>Reports one enum member when its value form differs from the configured one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="member">The enum member to inspect.</param>
    /// <param name="style">The configured value style.</param>
    private static void AnalyzeMember(SyntaxNodeAnalysisContext context, EnumMemberDeclarationSyntax member, EnumFlagValueStyle style)
    {
        if (member.EqualsValue is not { } equalsValue)
        {
            return;
        }

        var value = equalsValue.Value;
        var isDecimalLiteral = value.IsKind(SyntaxKind.NumericLiteralExpression);
        var isShift = IsSingleFlagShift(value);
        if (style == EnumFlagValueStyle.Shift ? !isDecimalLiteral : !isShift)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { ConstantValue: { } constant }
            || !TryGetSingleBit(constant, out var bit)
            || bit > MaxShift)
        {
            return;
        }

        var replacement = style == EnumFlagValueStyle.Shift
            ? $"1 << {bit.ToString(CultureInfo.InvariantCulture)}"
            : (1L << bit).ToString(CultureInfo.InvariantCulture);
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ReplacementKey, replacement);
        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeEnumFlagValueStyle, value.SyntaxTree, value.Span, properties, replacement));
    }

    /// <summary>Returns whether an enum symbol carries the <c>[Flags]</c> attribute.</summary>
    /// <param name="enumSymbol">The enum type symbol.</param>
    /// <param name="flagsAttribute">The resolved <c>System.FlagsAttribute</c> symbol.</param>
    /// <returns><see langword="true"/> when the enum is a flags enum.</returns>
    private static bool HasFlagsAttribute(INamedTypeSymbol enumSymbol, INamedTypeSymbol flagsAttribute)
    {
        foreach (var attribute in enumSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, flagsAttribute))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads a constant as the position of its single set bit, if it has exactly one.</summary>
    /// <param name="constant">The boxed constant value of the enum member.</param>
    /// <param name="bit">The zero-based bit position, when the value is a single bit.</param>
    /// <returns><see langword="true"/> when the value is a positive power of two.</returns>
    private static bool TryGetSingleBit(object constant, out int bit)
    {
        bit = 0;
        long value;
        if (constant is ulong unsigned)
        {
            if (unsigned > long.MaxValue)
            {
                return false;
            }

            value = (long)unsigned;
        }
        else
        {
            value = Convert.ToInt64(constant, CultureInfo.InvariantCulture);
        }

        if (value <= 0 || (value & (value - 1)) != 0)
        {
            return false;
        }

        while ((value & 1L) == 0)
        {
            value >>= 1;
            bit++;
        }

        return true;
    }
}
