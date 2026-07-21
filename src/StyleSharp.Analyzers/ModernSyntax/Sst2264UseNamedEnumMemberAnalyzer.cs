// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a numeric literal cast to an enum type (SST2264): <c>(RegexOptions)1</c> hides which member it means
/// behind its underlying value. Reported only when the literal resolves to exactly one named member; a value
/// that combines members or names none is left alone.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2264UseNamedEnumMemberAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseNamedEnumMember);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CastExpression);
    }

    /// <summary>Resolves a numeric enum cast to the <c>Type.Member</c> text that names the same value.</summary>
    /// <param name="cast">The cast expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="memberAccessText">The <c>Type.Member</c> replacement text.</param>
    /// <returns><see langword="true"/> when the literal names exactly one enum member.</returns>
    internal static bool TryGetNamedMemberAccess(
        CastExpressionSyntax cast,
        SemanticModel model,
        CancellationToken cancellationToken,
        out string memberAccessText)
    {
        memberAccessText = string.Empty;

        if (Unparenthesize(cast.Expression) is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal
            || model.GetTypeInfo(cast.Type, cancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            return false;
        }

        var constant = model.GetConstantValue(literal, cancellationToken);
        if (!constant.HasValue || constant.Value is not { } value || !IsIntegral(value)
            || !TryFindSingleMember(enumType, value, out var member))
        {
            return false;
        }

        var text = enumType.ToMinimalDisplayString(model, cast.SpanStart) + "." + member.Name;
        if (model.GetSpeculativeSymbolInfo(cast.SpanStart, SyntaxFactory.ParseExpression(text), SpeculativeBindingOption.BindAsExpression).Symbol is not IFieldSymbol bound
            || !SymbolEqualityComparer.Default.Equals(bound, member))
        {
            return false;
        }

        memberAccessText = text;
        return true;
    }

    /// <summary>Reports a numeric enum cast that names exactly one member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var cast = (CastExpressionSyntax)context.Node;
        if (!TryGetNamedMemberAccess(cast, context.SemanticModel, context.CancellationToken, out var memberAccessText))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseNamedEnumMember, cast.GetLocation(), memberAccessText));
    }

    /// <summary>Finds the one enum member whose constant value equals the literal value.</summary>
    /// <param name="enumType">The enum type.</param>
    /// <param name="value">The literal's value.</param>
    /// <param name="member">The single matching member.</param>
    /// <returns><see langword="true"/> when exactly one member matches.</returns>
    private static bool TryFindSingleMember(INamedTypeSymbol enumType, object value, out IFieldSymbol member)
    {
        member = null!;
        var target = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        var found = false;
        foreach (var symbol in enumType.GetMembers())
        {
            if (symbol is not IFieldSymbol { HasConstantValue: true, ConstantValue: { } constantValue } field || !IsIntegral(constantValue))
            {
                continue;
            }

            if (Convert.ToDecimal(constantValue, CultureInfo.InvariantCulture) != target)
            {
                continue;
            }

            if (found)
            {
                member = null!;
                return false;
            }

            member = field;
            found = true;
        }

        return found;
    }

    /// <summary>Returns whether a boxed value is one of the integral types an enum can use.</summary>
    /// <param name="value">The boxed value.</param>
    /// <returns><see langword="true"/> for the integral types.</returns>
    private static bool IsIntegral(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong;

    /// <summary>Strips redundant parentheses from the cast operand.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns>The operand with any surrounding parentheses removed.</returns>
    private static ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
