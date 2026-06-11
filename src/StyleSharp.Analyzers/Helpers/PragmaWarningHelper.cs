// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared classification of <c>#pragma warning</c> error codes. A compiler warning (<c>CS####</c>
/// or a bare numeric code) can only be silenced with a <c>#pragma</c>, so SST1426 leaves it alone;
/// any other code (an analyzer id such as <c>SST1309</c>, <c>the rule</c>, <c>the rule</c>) can move to a
/// scoped <c>[SuppressMessage]</c> attribute.
/// </summary>
internal static class PragmaWarningHelper
{
    /// <summary>Returns whether a pragma error code identifies a compiler warning.</summary>
    /// <param name="code">The error-code expression from a pragma directive.</param>
    /// <returns><see langword="true"/> for a numeric literal or a <c>CS####</c> identifier.</returns>
    public static bool IsCompilerWarningCode(ExpressionSyntax code) =>
        code switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NumericLiteralExpression),
            IdentifierNameSyntax identifier => IsCompilerWarningId(identifier.Identifier.ValueText),
            _ => false
        };

    /// <summary>Returns the source text of a pragma error code.</summary>
    /// <param name="code">The error-code expression.</param>
    /// <returns>The identifier or literal text.</returns>
    public static string CodeText(ExpressionSyntax code) =>
        code is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : code.ToString();

    /// <summary>
    /// Builds the comma-separated list of suppressible (non-compiler) codes in a disable directive,
    /// or <see langword="null"/> when every code is a compiler warning.
    /// </summary>
    /// <param name="directive">The pragma warning directive.</param>
    /// <returns>The joined suppressible codes, or <see langword="null"/> when there are none.</returns>
    public static string? GetSuppressibleCodeList(PragmaWarningDirectiveTriviaSyntax directive)
    {
        var codes = directive.ErrorCodes;
        string? single = null;
        StringBuilder? builder = null;
        for (var i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            if (IsCompilerWarningCode(code))
            {
                continue;
            }

            var text = CodeText(code);
            if (single is null)
            {
                single = text;
            }
            else
            {
                builder ??= new StringBuilder(single);
                builder.Append(", ").Append(text);
            }
        }

        return builder?.ToString() ?? single;
    }

    /// <summary>Returns whether an identifier names a compiler warning (<c>CS</c> followed by digits).</summary>
    /// <param name="code">The identifier text.</param>
    /// <returns><see langword="true"/> when the identifier is a <c>CS####</c> code.</returns>
    private static bool IsCompilerWarningId(string code)
    {
        if (code.Length < 3
            || (code[0] != 'C' && code[0] != 'c')
            || (code[1] != 'S' && code[1] != 's'))
        {
            return false;
        }

        for (var i = 2; i < code.Length; i++)
        {
            if (!char.IsDigit(code[i]))
            {
                return false;
            }
        }

        return true;
    }
}
