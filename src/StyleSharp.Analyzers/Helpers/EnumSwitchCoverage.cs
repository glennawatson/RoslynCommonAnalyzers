// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared building blocks for the rules that check whether an enum <c>switch</c> names every enum value:
/// recognizing an enum's value fields and testing whether a switch statement already has a case label for
/// a given value.
/// </summary>
internal static class EnumSwitchCoverage
{
    /// <summary>Returns whether a symbol is an enum value field.</summary>
    /// <param name="symbol">The candidate member.</param>
    /// <param name="field">The enum value field, when the symbol is one.</param>
    /// <returns><see langword="true"/> for enum value fields.</returns>
    internal static bool IsEnumValue(ISymbol symbol, out IFieldSymbol field)
    {
        if (symbol is IFieldSymbol { HasConstantValue: true } candidate)
        {
            field = candidate;
            return true;
        }

        field = null!;
        return false;
    }

    /// <summary>Returns whether a switch statement already covers an enum value with an explicit case label.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when a case label names the field.</returns>
    /// <remarks>
    /// A value is covered whether it is written as <c>case Value:</c> or inside a pattern that names it
    /// unconditionally, including one alternative of an <c>or</c>. A label carrying a <c>when</c> clause
    /// covers nothing on its own, because the guard decides whether the section runs.
    /// </remarks>
    internal static bool IsCaseLabelCovered(
        IFieldSymbol field,
        SwitchStatementSyntax switchStatement,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var sections = switchStatement.Sections;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (LabelCovers(field, labels[labelIndex], model, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether one label names a value with nothing guarding it.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="label">The switch label.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the label names the field outright.</returns>
    private static bool LabelCovers(IFieldSymbol field, SwitchLabelSyntax label, SemanticModel model, CancellationToken cancellationToken) =>
        label switch
        {
            CaseSwitchLabelSyntax caseLabel => Names(field, caseLabel.Value, model, cancellationToken),
            CasePatternSwitchLabelSyntax { WhenClause: null } patternLabel
                => PatternNames(field, patternLabel.Pattern, model, cancellationToken),
            _ => false,
        };

    /// <summary>Returns whether a pattern names a value on every path through it.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="pattern">The pattern to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when matching the pattern can only mean the field.</returns>
    /// <remarks>
    /// Only <c>or</c> is walked. An <c>and</c> narrows what matches and a <c>not</c> inverts it, so naming
    /// the value inside either of those says nothing about the value being handled.
    /// </remarks>
    private static bool PatternNames(IFieldSymbol field, PatternSyntax pattern, SemanticModel model, CancellationToken cancellationToken) =>
        pattern switch
        {
            ConstantPatternSyntax constant => Names(field, constant.Expression, model, cancellationToken),
            ParenthesizedPatternSyntax parenthesized => PatternNames(field, parenthesized.Pattern, model, cancellationToken),
            BinaryPatternSyntax { RawKind: (int)SyntaxKind.OrPattern } alternatives
                => PatternNames(field, alternatives.Left, model, cancellationToken)
                    || PatternNames(field, alternatives.Right, model, cancellationToken),
            _ => false,
        };

    /// <summary>Returns whether an expression binds to one enum value field.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="expression">The expression naming a value.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the expression names the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Names(IFieldSymbol field, ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken) =>
        SymbolEqualityComparer.Default.Equals(field, model.GetSymbolInfo(expression, cancellationToken).Symbol);
}
