// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports constant composite format strings whose holes cannot bind to supplied arguments (SST1454).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1454CompositeFormatStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The base used for decimal placeholder indexes.</summary>
    private const int DecimalBase = 10;

    /// <summary>The number of characters in an escaped brace pair.</summary>
    private const int EscapedBraceLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.ValidCompositeFormatString);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports string.Format calls whose literal format string cannot match the argument count.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                Name: "Format",
                ContainingType.SpecialType: SpecialType.System_String
            })
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (!TryGetFormatArgument(arguments, out var formatIndex, out var formatExpression)
            || context.SemanticModel.GetConstantValue(formatExpression, context.CancellationToken) is not { HasValue: true, Value: string format }
            || !TryGetMaximumPlaceholderIndex(format, out var maximumIndex))
        {
            return;
        }

        var suppliedCount = arguments.Count - formatIndex - 1;
        if (maximumIndex < suppliedCount)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.ValidCompositeFormatString, formatExpression.GetLocation()));
    }

    /// <summary>Finds the literal format-string argument in a string.Format invocation.</summary>
    /// <param name="arguments">The invocation arguments.</param>
    /// <param name="formatIndex">The index of the format argument.</param>
    /// <param name="formatExpression">The format expression.</param>
    /// <returns><see langword="true"/> when the direct arguments contain a format string.</returns>
    private static bool TryGetFormatArgument(
        in SeparatedSyntaxList<ArgumentSyntax> arguments,
        out int formatIndex,
        out ExpressionSyntax formatExpression)
    {
        formatIndex = 0;
        formatExpression = null!;
        if (arguments.Count == 0)
        {
            return false;
        }

        if (IsPositionalValueArgument(arguments[0]))
        {
            formatExpression = arguments[0].Expression;
            return true;
        }

        if (arguments.Count <= 1 || !IsPositionalValueArgument(arguments[1]))
        {
            return false;
        }

        formatIndex = 1;
        formatExpression = arguments[1].Expression;
        return true;
    }

    /// <summary>Returns whether an argument is positional and does not use ref-like syntax.</summary>
    /// <param name="argument">The argument.</param>
    /// <returns><see langword="true"/> for normal positional value arguments.</returns>
    private static bool IsPositionalValueArgument(ArgumentSyntax argument)
        => argument.NameColon is null && argument.RefKindKeyword.RawKind == 0;

    /// <summary>Advances past an escaped brace pair.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="index">The current index.</param>
    /// <param name="brace">The brace character.</param>
    /// <returns><see langword="true"/> when the current and next characters are the escaped brace pair.</returns>
    private static bool TrySkipEscapedBrace(string format, ref int index, char brace)
    {
        if (index + 1 >= format.Length || format[index + 1] != brace)
        {
            return false;
        }

        index += EscapedBraceLength;
        return true;
    }

    /// <summary>Parses the maximum placeholder index from a composite format string.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="maximumIndex">The largest placeholder index found.</param>
    /// <returns><see langword="true"/> when the format text is syntactically usable.</returns>
    private static bool TryGetMaximumPlaceholderIndex(string format, out int maximumIndex)
    {
        maximumIndex = -1;
        var index = 0;
        while (index < format.Length)
        {
            var ch = format[index];
            if (ch == '}')
            {
                if (TrySkipEscapedBrace(format, ref index, '}'))
                {
                    continue;
                }

                return false;
            }

            if (ch != '{')
            {
                index++;
                continue;
            }

            if (TrySkipEscapedBrace(format, ref index, '{'))
            {
                continue;
            }

            if (!TryReadPlaceholder(format, ref index, ref maximumIndex))
            {
                return false;
            }

            index++;
        }

        return maximumIndex >= 0;
    }

    /// <summary>Reads one placeholder and advances the scan index to its closing brace.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="index">The current scan index.</param>
    /// <param name="maximumIndex">The largest placeholder index found so far.</param>
    /// <returns><see langword="true"/> when the placeholder is valid.</returns>
    private static bool TryReadPlaceholder(string format, ref int index, ref int maximumIndex)
    {
        index++;
        if (index >= format.Length || !char.IsDigit(format[index]))
        {
            return false;
        }

        var placeholderIndex = 0;
        do
        {
            placeholderIndex = (placeholderIndex * DecimalBase) + format[index] - '0';
            index++;
        }
        while (index < format.Length && char.IsDigit(format[index]));

        if (placeholderIndex > maximumIndex)
        {
            maximumIndex = placeholderIndex;
        }

        while (index < format.Length && format[index] != '}')
        {
            if (format[index] == '{')
            {
                return false;
            }

            index++;
        }

        return index < format.Length;
    }
}
