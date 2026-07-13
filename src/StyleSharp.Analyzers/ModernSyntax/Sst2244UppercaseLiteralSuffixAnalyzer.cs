// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports numeric literals whose type suffix is written in lower case (SST2244): <c>1l</c>,
/// <c>1u</c>, <c>1f</c>, <c>1d</c>, <c>1m</c>, <c>1ul</c>, <c>1lu</c>, and the mixed forms such as
/// <c>1Ul</c>. The rule works entirely on the literal token's text and never touches the semantic
/// model.
/// </summary>
/// <remarks>
/// Only the suffix is the rule's business. The digits are left exactly as written, so a hex literal
/// keeps its lower-case digits (<c>0xffL</c> is reported for the <c>l</c> alone, and the fix leaves
/// <c>ff</c> untouched). Digit casing is a separate concern and this rule takes no position on it.
/// Because <c>u</c> and <c>l</c> are not hex or binary digits, a base-prefixed literal can only
/// carry an integer suffix, and <c>d</c>/<c>f</c> trailing a hex literal are digits rather than a
/// suffix.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2244UppercaseLiteralSuffixAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The shortest token text that can carry a base prefix.</summary>
    private const int BasePrefixLength = 2;

    /// <summary>The suffix characters an integer literal can carry; none of them is a hex or binary digit.</summary>
    private const string IntegerSuffixCharacters = "uUlL";

    /// <summary>The suffix characters only a base-ten literal can carry; in a hex literal these are digits.</summary>
    private const string RealSuffixCharacters = "fFdDmM";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UppercaseLiteralSuffix);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeNumericLiteral, SyntaxKind.NumericLiteralExpression);
    }

    /// <summary>Returns whether a numeric literal token's text ends in a suffix carrying a lower-case letter.</summary>
    /// <param name="text">The numeric literal token's text.</param>
    /// <param name="suffixStart">The index of the first suffix character when one is reported.</param>
    /// <returns><see langword="true"/> when the literal has a suffix and at least one of its characters is lower case.</returns>
    internal static bool TryGetLowercaseSuffix(string text, out int suffixStart)
    {
        suffixStart = FindSuffixStart(text);
        for (var index = suffixStart; index < text.Length; index++)
        {
            if (text[index] is >= 'a' and <= 'z')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports a numeric literal whose suffix is not already upper case.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeNumericLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var token = literal.Token;
        var text = token.Text;
        if (!TryGetLowercaseSuffix(text, out var suffixStart))
        {
            return;
        }

        var span = new TextSpan(token.SpanStart + suffixStart, text.Length - suffixStart);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UppercaseLiteralSuffix,
            literal.SyntaxTree,
            span,
            text.Substring(suffixStart)));
    }

    /// <summary>Returns the index at which the token text's type suffix starts, or its length when there is none.</summary>
    /// <param name="text">The numeric literal token's text.</param>
    /// <returns>The index of the first suffix character.</returns>
    private static int FindSuffixStart(string text)
    {
        var integerOnly = HasBasePrefix(text);
        var index = text.Length;
        while (index > 0 && IsSuffixCharacter(text[index - 1], integerOnly))
        {
            index--;
        }

        return index;
    }

    /// <summary>Returns whether the token text opens with a hexadecimal or binary base prefix.</summary>
    /// <param name="text">The numeric literal token's text.</param>
    /// <returns><see langword="true"/> for <c>0x</c>, <c>0X</c>, <c>0b</c>, and <c>0B</c>.</returns>
    private static bool HasBasePrefix(string text)
        => text.Length >= BasePrefixLength && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B';

    /// <summary>Returns whether a character can appear in a numeric literal's type suffix.</summary>
    /// <param name="character">The candidate suffix character.</param>
    /// <param name="integerOnly">Whether the literal is base-prefixed, and so can only carry an integer suffix.</param>
    /// <returns><see langword="true"/> when the character belongs to the suffix rather than the digits.</returns>
    private static bool IsSuffixCharacter(char character, bool integerOnly)
        => IntegerSuffixCharacters.IndexOf(character) >= 0
            || (!integerOnly && RealSuffixCharacters.IndexOf(character) >= 0);
}
