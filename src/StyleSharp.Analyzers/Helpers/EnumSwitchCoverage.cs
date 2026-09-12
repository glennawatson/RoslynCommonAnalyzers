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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsCaseLabelCovered(
        IFieldSymbol field,
        SwitchStatementSyntax switchStatement,
        SemanticModel model,
        CancellationToken cancellationToken) =>
        AnyLabelNames(field, switchStatement, model, includeGuarded: false, cancellationToken);

    /// <summary>Returns whether any label of a switch statement names an enum value, guard or not.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when a label names the field, including under a <c>when</c> clause.</returns>
    /// <remarks>
    /// A value already named under a guard cannot gain a second, unguarded label without reading as a
    /// duplicate of the one that is there, so a switch holding one is completed with a catch-all.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNamedByAnyLabel(
        IFieldSymbol field,
        SwitchStatementSyntax switchStatement,
        SemanticModel model,
        CancellationToken cancellationToken) =>
        AnyLabelNames(field, switchStatement, model, includeGuarded: true, cancellationToken);

    /// <summary>Writes an enum value the way the switch's own file binds it.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position the name is written at.</param>
    /// <returns>The shortest expression that binds to the field there.</returns>
    /// <remarks>
    /// One file compiled into several projects can sit in a different namespace in each, so a name rooted
    /// at the namespace of the compilation the fix ran in does not bind in the others. The shortest name
    /// is the one the file already writes for its own labels, and it is checked against the field before
    /// it is handed on; a name that does not bind back falls to the fully qualified form.
    /// </remarks>
    internal static string NameFor(IFieldSymbol field, SemanticModel model, int position)
    {
        var minimal = $"{field.ContainingType.ToMinimalDisplayString(model, position)}.{field.Name}";
        return BindsTo(field, minimal, model, position)
            ? minimal
            : $"{field.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{field.Name}";
    }

    /// <summary>Returns whether an expression written at a position binds to one enum value field.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="text">The expression to bind.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position the expression is written at.</param>
    /// <returns><see langword="true"/> when the expression resolves to the field.</returns>
    private static bool BindsTo(IFieldSymbol field, string text, SemanticModel model, int position) =>
        model.GetSpeculativeSymbolInfo(position, SyntaxFactory.ParseExpression(text), SpeculativeBindingOption.BindAsExpression).Symbol is IFieldSymbol bound
            && SymbolEqualityComparer.Default.Equals(bound, field);

    /// <summary>Returns whether some label of a switch statement names an enum value.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="includeGuarded">Whether a label carrying a <c>when</c> clause counts.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when a matching label is found.</returns>
    private static bool AnyLabelNames(
        IFieldSymbol field,
        SwitchStatementSyntax switchStatement,
        SemanticModel model,
        bool includeGuarded,
        CancellationToken cancellationToken)
    {
        var sections = switchStatement.Sections;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (LabelNames(field, labels[labelIndex], model, includeGuarded, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether one label names a value.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="label">The switch label.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="includeGuarded">Whether a label carrying a <c>when</c> clause counts.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the label names the field.</returns>
    private static bool LabelNames(IFieldSymbol field, SwitchLabelSyntax label, SemanticModel model, bool includeGuarded, CancellationToken cancellationToken) =>
        label switch
        {
            CaseSwitchLabelSyntax caseLabel => Names(field, caseLabel.Value, model, cancellationToken),
            CasePatternSwitchLabelSyntax patternLabel when includeGuarded || patternLabel.WhenClause is null
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
