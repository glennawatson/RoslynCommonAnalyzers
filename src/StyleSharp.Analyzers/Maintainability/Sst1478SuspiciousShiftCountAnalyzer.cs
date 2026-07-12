// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a shift whose constant count is zero, negative, or at least the operand's width (SST1478).
/// A shift by zero is allowed with <c>stylesharp.SST1478.allow_zero_shift</c>.
/// </summary>
/// <remarks>
/// <para>
/// C# does not shift a value out of existence. The count is masked to the operand's width before the shift
/// runs — five bits for a 32-bit operand, six for a 64-bit one — so <c>x &lt;&lt; 32</c> on an <c>int</c>
/// shifts by 0 and hands back <c>x</c> unchanged, which is the opposite of what the code appears to say.
/// A negative count masks the same way: <c>x &lt;&lt; -1</c> shifts left by 31.
/// </para>
/// <para>
/// The width is the width of the operand the shift actually runs on, which is not always the width of the
/// operand as written. The built-in shift operators are defined for <c>int</c>, <c>uint</c>, <c>long</c>
/// and <c>ulong</c> only, so a <c>byte</c>, <c>sbyte</c>, <c>short</c>, <c>ushort</c> or <c>char</c> is
/// promoted to <c>int</c> first — which is why <c>b &lt;&lt; 8</c> on a <c>byte</c> is a perfectly ordinary
/// shift of a 32-bit value and is not reported. Only <c>long</c> and <c>ulong</c> are 64 bits wide.
/// <c>nint</c> and <c>nuint</c> are never reported, because their width depends on the process.
/// </para>
/// <para>
/// Ordered so almost every shift in real code is rejected without touching the model: no operand this rule
/// measures is narrower than 32 bits, so a plain literal count of 1 through 31 is in range whatever it is
/// shifting, and is thrown out on the token's digits. Only a count outside that window is bound, and the
/// settings are read only for a count that has already turned out to be zero.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1478SuspiciousShiftCountAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The width of an operand promoted to, or declared as, a 32-bit integer.</summary>
    private const int NarrowWidth = 32;

    /// <summary>The width of a 64-bit operand.</summary>
    private const int WideWidth = 64;

    /// <summary>The smallest count the syntactic fast path can clear without the model.</summary>
    private const int SmallestSafeCount = 1;

    /// <summary>The largest count the syntactic fast path can clear without the model.</summary>
    /// <remarks>One less than <see cref="NarrowWidth"/>, the narrowest width the rule ever measures.</remarks>
    private const int LargestSafeCount = NarrowWidth - 1;

    /// <summary>The most digits a count in the fast path's window can have.</summary>
    private const int SafeCountDigits = 2;

    /// <summary>The radix used when reading a literal's digits.</summary>
    private const int DecimalRadix = 10;

    /// <summary>The clause used for a shift that is masked away to nothing.</summary>
    private const string DoesNothingClause = "does nothing";

    /// <summary>The clause used for a shift whose masked count is zero.</summary>
    private const string MasksToZeroClause = "shifts by 0 because C# masks the count, so the value is returned unchanged";

    /// <summary>The start of the clause used for a shift whose masked count is not zero.</summary>
    private const string MasksToPrefix = "shifts by ";

    /// <summary>The end of the clause used for a shift whose masked count is not zero.</summary>
    private const string MasksToSuffix = " because C# masks the count";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(MaintainabilityRules.SuspiciousShiftCount);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the per-compilation settings cache, then analyzes every shift expression.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// <c>&gt;&gt;&gt;</c> only exists from C# 11, so on an older language version the parser never produces
    /// the kind and the registration costs nothing.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, ShiftCountOptions>();
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, optionsByTree),
            SyntaxKind.LeftShiftExpression,
            SyntaxKind.RightShiftExpression,
            SyntaxKind.UnsignedRightShiftExpression);
    }

    /// <summary>Reports one shift whose constant count cannot mean what it says.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, ShiftCountOptions> optionsByTree)
    {
        var shift = (BinaryExpressionSyntax)context.Node;
        if (IsCountAlwaysInRange(shift.Right))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        var constant = model.GetConstantValue(shift.Right, cancellationToken);
        if (!constant.HasValue || !TryGetCount(constant.Value, out var count))
        {
            return;
        }

        if (GetWidth(model.GetTypeInfo(shift.Left, cancellationToken).Type) is not { } width)
        {
            return;
        }

        if (count > 0 && count < width)
        {
            return;
        }

        if (count == 0 && GetOptions(context, optionsByTree).AllowZeroShift)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.SuspiciousShiftCount,
            shift.GetLocation(),
            width.ToString(CultureInfo.InvariantCulture),
            count.ToString(CultureInfo.InvariantCulture),
            DescribeEffect(count, width)));
    }

    /// <summary>Reads the settings for the shift's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static ShiftCountOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, ShiftCountOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = ShiftCountOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Returns whether a count is a plain literal that is in range for every operand this rule measures.</summary>
    /// <param name="count">The shift's right operand.</param>
    /// <returns><see langword="true"/> for a literal count of 1 through 31.</returns>
    /// <remarks>
    /// The narrowest operand the rule measures is 32 bits wide — everything smaller is promoted to
    /// <c>int</c> — so a count in this window is valid whatever the left operand turns out to be, and the
    /// shift can be cleared on its digits alone. A hexadecimal, separated or suffixed literal falls through
    /// to the model rather than being parsed here; it is rare enough not to be worth the extra text scan.
    /// </remarks>
    private static bool IsCountAlwaysInRange(ExpressionSyntax count)
    {
        if (count is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal)
        {
            return false;
        }

        var text = literal.Token.Text;
        if (text.Length is 0 or > SafeCountDigits)
        {
            return false;
        }

        var value = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character is < '0' or > '9')
            {
                return false;
            }

            value = (value * DecimalRadix) + (character - '0');
        }

        return value is >= SmallestSafeCount and <= LargestSafeCount;
    }

    /// <summary>Reads a constant shift count.</summary>
    /// <param name="value">The count's constant value.</param>
    /// <param name="count">The count.</param>
    /// <returns><see langword="true"/> when the constant is one of the types a shift count can have.</returns>
    /// <remarks>
    /// The built-in shift operators take an <c>int</c> count, so a constant that reaches one is either an
    /// <c>int</c> or something implicitly convertible to it. A <c>long</c> count does not compile.
    /// </remarks>
    private static bool TryGetCount(object? value, out long count)
    {
        switch (value)
        {
            case int number:
            {
                count = number;
                return true;
            }

            case short number:
            {
                count = number;
                return true;
            }

            case ushort number:
            {
                count = number;
                return true;
            }

            case byte number:
            {
                count = number;
                return true;
            }

            case sbyte number:
            {
                count = number;
                return true;
            }

            case char number:
            {
                count = number;
                return true;
            }

            default:
            {
                count = 0;
                return false;
            }
        }
    }

    /// <summary>Gets the width of the operand the shift actually runs on.</summary>
    /// <param name="type">The left operand's type.</param>
    /// <returns>The width in bits, or <see langword="null"/> when the rule does not measure the type.</returns>
    /// <remarks>
    /// Everything narrower than an <c>int</c> is promoted to one by the built-in operator, so it is 32 bits
    /// wide by the time the shift happens — a <c>byte</c> shifted by 8 is not masked away. A native integer
    /// is left alone because its width is a property of the process, not of the program. A nullable operand
    /// lifts the same operator, so it is measured by the type it wraps.
    /// </remarks>
    private static int? GetWidth(ITypeSymbol? type)
    {
        var special = UnwrapNullable(type)?.SpecialType;
        if (IsNarrowOperand(special))
        {
            return NarrowWidth;
        }

        return IsWideOperand(special) ? WideWidth : null;
    }

    /// <summary>Returns whether an operand is shifted as a 32-bit value.</summary>
    /// <param name="special">The left operand's special type, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> for an <c>int</c>, a <c>uint</c>, and everything the operator promotes to one.</returns>
    private static bool IsNarrowOperand(SpecialType? special) => special
        is SpecialType.System_SByte
        or SpecialType.System_Byte
        or SpecialType.System_Int16
        or SpecialType.System_UInt16
        or SpecialType.System_Char
        or SpecialType.System_Int32
        or SpecialType.System_UInt32;

    /// <summary>Returns whether an operand is shifted as a 64-bit value.</summary>
    /// <param name="special">The left operand's special type, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> for a <c>long</c> and a <c>ulong</c>.</returns>
    private static bool IsWideOperand(SpecialType? special)
        => special is SpecialType.System_Int64 or SpecialType.System_UInt64;

    /// <summary>Unwraps a nullable value type to the type it wraps.</summary>
    /// <param name="type">The left operand's type.</param>
    /// <returns>The underlying type, or the type itself when it is not nullable.</returns>
    private static ITypeSymbol? UnwrapNullable(ITypeSymbol? type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    /// <summary>Describes what the shift actually does, once the count has been masked.</summary>
    /// <param name="count">The constant count as written.</param>
    /// <param name="width">The operand's width in bits.</param>
    /// <returns>The clause that completes "Shifting a {width}-bit value by {count} ...".</returns>
    private static string DescribeEffect(long count, int width)
    {
        if (count == 0)
        {
            return DoesNothingClause;
        }

        var effective = (int)(count & (width - 1));
        return effective == 0
            ? MasksToZeroClause
            : MasksToPrefix + effective.ToString(CultureInfo.InvariantCulture) + MasksToSuffix;
    }
}
