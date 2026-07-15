// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    public static bool IsEnumValue(ISymbol symbol, out IFieldSymbol field)
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
    public static bool IsCaseLabelCovered(
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
                if (labels[labelIndex] is CaseSwitchLabelSyntax caseLabel
                    && SymbolEqualityComparer.Default.Equals(field, model.GetSymbolInfo(caseLabel.Value, cancellationToken).Symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
