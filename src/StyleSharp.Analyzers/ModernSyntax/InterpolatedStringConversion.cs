// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Turns a composite format call or a literal-plus-value concatenation into the interpolated string
/// that says the same thing (SST2249), and refuses every shape where the two would not be identical.
/// </summary>
/// <remarks>
/// <para>
/// The build is shared by the analyzer and its code fix so a diagnostic is only raised when a
/// compiling, meaning-preserving rewrite exists: both entry points construct the candidate string,
/// reparse it, and speculatively bind it to prove it is still a <see cref="string"/> before either
/// reports or offers a fix.
/// </para>
/// <para>
/// <b>The culture the call selected is never dropped.</b> A composite format with no provider and a
/// plain interpolated string both format with the current culture, so that rewrite is exact. A
/// composite format that passes an explicit <see cref="IFormatProvider"/> is left alone entirely,
/// because a plain interpolated string would format with the current culture instead.
/// </para>
/// <para>
/// <b>Only genuine string concatenation is rewritten.</b> Each <c>+</c> in the chain must bind to
/// string concatenation; a chain whose leading operands add as numbers (<c>1 + 2 + " items"</c>) or a
/// user-defined <c>operator +</c> that returns a string is left alone, because folding those into
/// interpolation holes would change the value.
/// </para>
/// </remarks>
internal static class InterpolatedStringConversion
{
    /// <summary>The method name a composite format call carries.</summary>
    private const string FormatMethodName = "Format";

    /// <summary>The fewest arguments a rewritable composite format call carries: a format and one value.</summary>
    private const int MinFormatArguments = 2;

    /// <summary>The length of the shortest plain string literal token, <c>""</c>.</summary>
    private const int EmptyRegularLiteralLength = 2;

    /// <summary>The length of a doubled-brace escape, <c>{{</c> or <c>}}</c>.</summary>
    private const int EscapeLength = 2;

    /// <summary>The index of the format-string argument when the call passes an explicit provider first.</summary>
    private const int ProviderFormatIndex = 1;

    /// <summary>Returns whether an invocation is syntactically a <c>string.Format</c> call worth binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches, before any binding.</returns>
    internal static bool IsFormatShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count >= MinFormatArguments
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == FormatMethodName
            && LooksLikeStringReceiver(access.Expression);

    /// <summary>Returns whether a binary expression is the top of a literal-plus-value concatenation chain.</summary>
    /// <param name="node">The binary expression to inspect.</param>
    /// <returns><see langword="true"/> when the chain mixes at least one string literal with at least one value.</returns>
    internal static bool IsConcatenationCandidate(BinaryExpressionSyntax node)
    {
        if (!node.IsKind(SyntaxKind.AddExpression) || node.Parent.IsKind(SyntaxKind.AddExpression))
        {
            return false;
        }

        var hasLiteral = false;
        var hasValue = false;
        var current = (ExpressionSyntax)node;
        while (current is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } add)
        {
            Classify(add.Right, ref hasLiteral, ref hasValue);
            if (hasLiteral && hasValue)
            {
                return true;
            }

            current = add.Left;
        }

        Classify(current, ref hasLiteral, ref hasValue);
        return hasLiteral && hasValue;
    }

    /// <summary>Builds the interpolated string that replaces a <c>string.Format</c> call, when the rewrite is exact.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The composite format invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The interpolated string, or <see langword="null"/> when the call must be left alone.</returns>
    internal static InterpolatedStringExpressionSyntax? TryConvertFormat(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        if (!IsFormatShape(invocation) || HasNonPositionalArgument(invocation) || BindStringFormat(model, invocation, cancellationToken) is not { } method)
        {
            return null;
        }

        return TryBuildFormatBody(model, method, invocation, out var inner) ? BuildVerified(model, invocation, inner) : null;
    }

    /// <summary>Builds the interpolated string that replaces a concatenation chain, when the rewrite is exact.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="top">The top of the concatenation chain.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The interpolated string, or <see langword="null"/> when the chain must be left alone.</returns>
    internal static InterpolatedStringExpressionSyntax? TryConvertConcatenation(SemanticModel model, BinaryExpressionSyntax top, CancellationToken cancellationToken)
    {
        if (!IsConcatenationCandidate(top) || !TryFlattenStringConcatenation(model, top, cancellationToken, out var operands))
        {
            return null;
        }

        var builder = new StringBuilder();
        var hasLiteral = false;
        var hasValue = false;
        for (var i = 0; i < operands.Count; i++)
        {
            var operand = operands[i];
            if (operand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal)
            {
                if (!IsRegularStringLiteral(literal))
                {
                    return null;
                }

                AppendEscapedText(builder, literal.Token.ValueText);
                hasLiteral = true;
            }
            else
            {
                builder.Append('{').Append(HoleText(operand)).Append('}');
                hasValue = true;
            }
        }

        return hasLiteral && hasValue ? BuildVerified(model, top, builder.ToString()) : null;
    }

    /// <summary>Validates a bound format call and builds the interpolated-string body it maps to.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="method">The bound format method.</param>
    /// <param name="invocation">The composite format invocation.</param>
    /// <param name="inner">The interpolated-string body.</param>
    /// <returns><see langword="true"/> when the call has a supported, provider-free shape.</returns>
    private static bool TryBuildFormatBody(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, out string inner)
    {
        inner = null!;

        // A provider argument selects a culture a plain interpolated string cannot preserve, so it is left alone.
        var formatIndex = IsFormatProvider(method.Parameters[0].Type) ? ProviderFormatIndex : 0;
        var arguments = invocation.ArgumentList.Arguments;
        if (formatIndex == ProviderFormatIndex
            || method.Parameters[formatIndex].Type.SpecialType != SpecialType.System_String
            || arguments.Count <= formatIndex + 1
            || arguments[formatIndex].Expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            || !IsRegularStringLiteral(literal)
            || !TryGetFormatValues(model, method, invocation, formatIndex, out var values))
        {
            return false;
        }

        return TryBuildFormatInnerText(literal.Token.ValueText, values, out inner);
    }

    /// <summary>Classifies one concatenation operand as a string literal or a value.</summary>
    /// <param name="operand">The operand to classify.</param>
    /// <param name="hasLiteral">Set when the operand is a string literal.</param>
    /// <param name="hasValue">Set when the operand is anything else.</param>
    private static void Classify(ExpressionSyntax operand, ref bool hasLiteral, ref bool hasValue)
    {
        if (operand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression })
        {
            hasLiteral = true;
        }
        else
        {
            hasValue = true;
        }
    }

    /// <summary>Returns whether a receiver spelling could be the <see cref="string"/> type.</summary>
    /// <param name="expression">The member-access receiver.</param>
    /// <returns><see langword="true"/> for <c>string</c>, <c>String</c>, or a qualified name ending in <c>String</c>.</returns>
    private static bool LooksLikeStringReceiver(ExpressionSyntax expression)
        => expression switch
        {
            PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.StringKeyword } => true,
            IdentifierNameSyntax { Identifier.ValueText: "String" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "String" } => true,
            _ => false
        };

    /// <summary>Returns whether any argument is named or passed by reference.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when an argument is not a plain positional value.</returns>
    private static bool HasNonPositionalArgument(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null || !arguments[i].RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Binds a call and keeps it only when it is the framework's own <c>string.Format</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The bound method, or <see langword="null"/>.</returns>
    private static IMethodSymbol? BindStringFormat(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method && IsStringFormatMethod(method) ? method : null;

    /// <summary>Returns whether a symbol is the framework's own static <c>string.Format</c>.</summary>
    /// <param name="method">The bound method.</param>
    /// <returns><see langword="true"/> for a static <c>string.Format</c> returning a string.</returns>
    private static bool IsStringFormatMethod(IMethodSymbol method)
        => method is
        {
            IsStatic: true,
            Name: FormatMethodName,
            ReturnType.SpecialType: SpecialType.System_String,
            ContainingType.SpecialType: SpecialType.System_String,
            Parameters.Length: > 0
        };

    /// <summary>Collects the value expressions a composite format call passes after its format string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="method">The bound format method.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="formatIndex">The index of the format-string argument.</param>
    /// <param name="values">The collected value expressions.</param>
    /// <returns><see langword="true"/> unless a single array is passed as the params array itself.</returns>
    private static bool TryGetFormatValues(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, int formatIndex, out List<ExpressionSyntax> values)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var count = arguments.Count - formatIndex - 1;

        // A lone object[] handed to a params parameter is spread across the placeholders, not printed as one value.
        var last = method.Parameters[method.Parameters.Length - 1];
        if (count == 1 && last.IsParams && last.Type is IArrayTypeSymbol arrayType)
        {
            var conversion = model.ClassifyConversion(arguments[formatIndex + 1].Expression, arrayType);
            if (conversion.Exists && conversion.IsImplicit)
            {
                values = null!;
                return false;
            }
        }

        values = new List<ExpressionSyntax>(count);
        for (var i = formatIndex + 1; i < arguments.Count; i++)
        {
            values.Add(arguments[i].Expression);
        }

        return true;
    }

    /// <summary>Collects the operands of a concatenation chain, left to right, proving each <c>+</c> is string concatenation.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="top">The top of the chain.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="operands">The collected operands, in source order.</param>
    /// <returns><see langword="true"/> when every <c>+</c> binds to string concatenation.</returns>
    private static bool TryFlattenStringConcatenation(SemanticModel model, BinaryExpressionSyntax top, CancellationToken cancellationToken, out List<ExpressionSyntax> operands)
    {
        operands = null!;
        var rights = new List<ExpressionSyntax>();
        var current = (ExpressionSyntax)top;
        while (current is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } add)
        {
            if (model.GetSymbolInfo(add, cancellationToken).Symbol is not IMethodSymbol { ContainingType.SpecialType: SpecialType.System_String })
            {
                return false;
            }

            rights.Add(add.Right);
            current = add.Left;
        }

        operands = new List<ExpressionSyntax>(rights.Count + 1) { current };
        for (var i = rights.Count - 1; i >= 0; i--)
        {
            operands.Add(rights[i]);
        }

        return true;
    }

    /// <summary>Builds the interpolated-string body for a composite format string, mapping each placeholder to its value.</summary>
    /// <param name="format">The format string's runtime value.</param>
    /// <param name="values">The value expressions the placeholders reference.</param>
    /// <param name="inner">The interpolated-string body, without the leading <c>$"</c> and trailing quote.</param>
    /// <returns><see langword="true"/> when the placeholders form a gap-free set that uses every value once.</returns>
    private static bool TryBuildFormatInnerText(string format, List<ExpressionSyntax> values, out string inner)
    {
        inner = null!;
        var builder = new StringBuilder();
        var used = new bool[values.Count];
        var usedCount = 0;
        var index = 0;
        while (index < format.Length)
        {
            var current = format[index];
            if (current == '{')
            {
                if (index + 1 < format.Length && format[index + 1] == '{')
                {
                    builder.Append("{{");
                    index += EscapeLength;
                }
                else if (!TryAppendPlaceholder(format, ref index, values, used, ref usedCount, builder))
                {
                    return false;
                }
            }
            else if (current == '}')
            {
                if (index + 1 >= format.Length || format[index + 1] != '}')
                {
                    return false;
                }

                builder.Append("}}");
                index += EscapeLength;
            }
            else
            {
                builder.Append(Escape(current));
                index++;
            }
        }

        if (usedCount != values.Count)
        {
            return false;
        }

        inner = builder.ToString();
        return true;
    }

    /// <summary>Reads one <c>{index[,alignment][:format]}</c> placeholder and appends its interpolation hole.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="index">The scan position, standing on the opening brace and advanced past the closing one.</param>
    /// <param name="values">The value expressions the placeholder can reference.</param>
    /// <param name="used">Tracks which values have already been consumed.</param>
    /// <param name="usedCount">The running count of distinct values consumed.</param>
    /// <param name="builder">The interpolated-string body under construction.</param>
    /// <returns><see langword="true"/> when a supported placeholder was consumed.</returns>
    private static bool TryAppendPlaceholder(string format, ref int index, List<ExpressionSyntax> values, bool[] used, ref int usedCount, StringBuilder builder)
    {
        var position = index + 1;
        if (!TryReadReference(format, ref position, values.Count, used, out var reference))
        {
            return false;
        }

        SkipSpaces(format, ref position);
        var alignment = string.Empty;
        if (position < format.Length && format[position] == ',' && !TryReadAlignment(format, ref position, out alignment))
        {
            return false;
        }

        var formatSpecifier = string.Empty;
        if (position < format.Length && format[position] == ':' && !TryReadFormatSpecifier(format, ref position, out formatSpecifier))
        {
            return false;
        }

        if (position >= format.Length || format[position] != '}')
        {
            return false;
        }

        used[reference] = true;
        usedCount++;
        builder.Append('{').Append(HoleText(values[reference])).Append(alignment).Append(formatSpecifier).Append('}');
        index = position + 1;
        return true;
    }

    /// <summary>Reads a placeholder's numeric index and confirms it references an unused value.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="position">The scan position, standing after the opening brace and advanced past the digits.</param>
    /// <param name="valueCount">The number of values the placeholder can reference.</param>
    /// <param name="used">Tracks which values have already been consumed.</param>
    /// <param name="reference">The referenced value index.</param>
    /// <returns><see langword="true"/> when a fresh, in-range index was read.</returns>
    private static bool TryReadReference(string format, ref int position, int valueCount, bool[] used, out int reference)
    {
        reference = 0;
        var start = position;
        while (position < format.Length && format[position] >= '0' && format[position] <= '9')
        {
            position++;
        }

        return position != start
            && int.TryParse(format.Substring(start, position - start), out reference)
            && reference >= 0
            && reference < valueCount
            && !used[reference];
    }

    /// <summary>Reads a <c>,[-]digits</c> alignment clause into its canonical spelling.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="position">The scan position, standing on the comma and advanced past the clause.</param>
    /// <param name="alignment">The canonical alignment, such as <c>,-5</c>.</param>
    /// <returns><see langword="true"/> when a well-formed alignment was read.</returns>
    private static bool TryReadAlignment(string format, ref int position, out string alignment)
    {
        alignment = null!;
        position++;
        SkipSpaces(format, ref position);
        var negative = position < format.Length && format[position] == '-';
        if (negative)
        {
            position++;
        }

        var start = position;
        while (position < format.Length && format[position] >= '0' && format[position] <= '9')
        {
            position++;
        }

        if (position == start)
        {
            return false;
        }

        var digits = format.Substring(start, position - start);
        SkipSpaces(format, ref position);
        alignment = negative ? ",-" + digits : "," + digits;
        return true;
    }

    /// <summary>Reads a <c>:format</c> clause, refusing anything a plain interpolated string could not carry verbatim.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="position">The scan position, standing on the colon and advanced to the closing brace.</param>
    /// <param name="formatSpecifier">The clause, such as <c>:X2</c>.</param>
    /// <returns><see langword="true"/> when the clause holds only characters valid inside a hole.</returns>
    private static bool TryReadFormatSpecifier(string format, ref int position, out string formatSpecifier)
    {
        formatSpecifier = null!;
        position++;
        var start = position;
        while (position < format.Length && format[position] != '}')
        {
            var current = format[position];
            if (current is '{' or '"' or '\\' or < ' ')
            {
                return false;
            }

            position++;
        }

        if (position >= format.Length)
        {
            return false;
        }

        formatSpecifier = ":" + format.Substring(start, position - start);
        return true;
    }

    /// <summary>Advances past any run of spaces.</summary>
    /// <param name="format">The format string.</param>
    /// <param name="position">The scan position.</param>
    private static void SkipSpaces(string format, ref int position)
    {
        while (position < format.Length && format[position] == ' ')
        {
            position++;
        }
    }

    /// <summary>Renders a value expression as the text of an interpolation hole.</summary>
    /// <param name="expression">The value expression.</param>
    /// <returns>The hole text, parenthesized when a bare conditional would collide with the hole's format separator.</returns>
    private static string HoleText(ExpressionSyntax expression)
    {
        var text = expression.WithoutTrivia().ToString();
        return expression is ConditionalExpressionSyntax ? "(" + text + ")" : text;
    }

    /// <summary>Appends a run of literal characters, escaped for a plain interpolated string.</summary>
    /// <param name="builder">The interpolated-string body under construction.</param>
    /// <param name="text">The literal text.</param>
    private static void AppendEscapedText(StringBuilder builder, string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            builder.Append(Escape(text[i]));
        }
    }

    /// <summary>Escapes one literal character for a plain interpolated string.</summary>
    /// <param name="value">The literal character.</param>
    /// <returns>The character's escaped spelling, or the character itself when no escape is needed.</returns>
    private static string Escape(char value)
        => value switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            '{' => "{{",
            '}' => "}}",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ when char.IsControl(value) => "\\u" + ((int)value).ToString("X4", CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture)
        };

    /// <summary>Reparses and speculatively binds the candidate string to prove it still compiles to a <see cref="string"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="original">The expression being replaced, used as the binding position.</param>
    /// <param name="inner">The interpolated-string body.</param>
    /// <returns>The verified interpolated string, or <see langword="null"/>.</returns>
    private static InterpolatedStringExpressionSyntax? BuildVerified(SemanticModel model, ExpressionSyntax original, string inner)
    {
        if (SyntaxFactory.ParseExpression("$\"" + inner + "\"") is not InterpolatedStringExpressionSyntax interpolated || interpolated.ContainsDiagnostics)
        {
            return null;
        }

        var type = model.GetSpeculativeTypeInfo(original.SpanStart, interpolated, SpeculativeBindingOption.BindAsExpression).Type;
        return type is { SpecialType: SpecialType.System_String } ? interpolated : null;
    }

    /// <summary>Returns whether a string literal is a plain (non-verbatim, non-raw) literal whose text is easy to re-escape.</summary>
    /// <param name="literal">The string literal.</param>
    /// <returns><see langword="true"/> for an ordinary <c>"..."</c> literal.</returns>
    private static bool IsRegularStringLiteral(LiteralExpressionSyntax literal)
    {
        // A plain literal opens with a single quote; a verbatim literal opens with '@' and a raw literal with '"""'.
        var text = literal.Token.Text;
        return text.Length >= EmptyRegularLiteralLength && text[0] == '"' && !text.StartsWith("\"\"\"", StringComparison.Ordinal);
    }

    /// <summary>Returns whether a type is <see cref="IFormatProvider"/>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for <c>System.IFormatProvider</c>.</returns>
    private static bool IsFormatProvider(ITypeSymbol type)
        => type is INamedTypeSymbol
        {
            Name: "IFormatProvider",
            TypeKind: TypeKind.Interface,
            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
        };
}
