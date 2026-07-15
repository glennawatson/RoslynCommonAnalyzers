// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a remainder comparison against a non-zero constant on a signed operand (SST2416): the classic
/// <c>x % 2 == 1</c> odd-number test, which is false for every negative value because the remainder keeps the
/// sign of the dividend.
/// </summary>
/// <remarks>
/// The clean path is a syntax-kind test: register on <c>==</c> / <c>!=</c> and bail unless one side is a
/// remainder. Only then are the two constants and the operand's type resolved. An operand that cannot be
/// negative — a length, a count, an absolute value — is rejected on the syntax before binding.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2416SignedRemainderTestAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.SignedRemainderTest);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Reports one signed remainder equality test.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (!TryGetModulo(comparison, out var modulo, out var other))
        {
            return;
        }

        var dividend = modulo.Left;
        if (IsProvablyNonNegative(dividend)
            || !IsNonZeroConstant(context, modulo.Right)
            || !IsNonZeroConstant(context, other)
            || !IsSignedType(context.SemanticModel.GetTypeInfo(dividend, context.CancellationToken).Type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.SignedRemainderTest,
            comparison.GetLocation(),
            dividend.ToString()));
    }

    /// <summary>Finds the remainder operand of a comparison, and the constant it is tested against.</summary>
    /// <param name="comparison">The equality comparison.</param>
    /// <param name="modulo">The remainder expression.</param>
    /// <param name="other">The other operand.</param>
    /// <returns><see langword="true"/> when exactly one side is a remainder.</returns>
    private static bool TryGetModulo(BinaryExpressionSyntax comparison, out BinaryExpressionSyntax modulo, out ExpressionSyntax other)
    {
        if (comparison.Left is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } left)
        {
            modulo = left;
            other = comparison.Right;
            return true;
        }

        if (comparison.Right is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.ModuloExpression } right)
        {
            modulo = right;
            other = comparison.Left;
            return true;
        }

        modulo = null!;
        other = null!;
        return false;
    }

    /// <summary>Returns whether an expression is a non-zero integral compile-time constant.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns><see langword="true"/> for a non-zero integral constant.</returns>
    private static bool IsNonZeroConstant(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        return constant is { HasValue: true, Value: { } value }
            && IsIntegral(value)
            && Convert.ToDecimal(value, CultureInfo.InvariantCulture) != 0m;
    }

    /// <summary>Returns whether a boxed constant is an integral value.</summary>
    /// <param name="value">The boxed constant.</param>
    /// <returns><see langword="true"/> for a signed or unsigned integer.</returns>
    private static bool IsIntegral(object value)
        => value is int or long or short or byte or sbyte or ushort or uint or ulong;

    /// <summary>Returns whether an operand is provably non-negative from its syntax.</summary>
    /// <param name="expression">The dividend.</param>
    /// <returns><see langword="true"/> for a length, a count, or an absolute value.</returns>
    private static bool IsProvablyNonNegative(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText is "Length" or "Count",
        InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax abs } => abs.Name.Identifier.ValueText == "Abs",
        _ => false,
    };

    /// <summary>Returns whether a type is a signed numeric type that can be negative.</summary>
    /// <param name="type">The operand's type.</param>
    /// <returns><see langword="true"/> for a signed integer, decimal, or big integer.</returns>
    private static bool IsSignedType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type.SpecialType is SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_Int32
            or SpecialType.System_Int64
            or SpecialType.System_IntPtr
            or SpecialType.System_Decimal)
        {
            return true;
        }

        return type.Name == "BigInteger" && type.ContainingNamespace?.ToDisplayString() == "System.Numerics";
    }
}
