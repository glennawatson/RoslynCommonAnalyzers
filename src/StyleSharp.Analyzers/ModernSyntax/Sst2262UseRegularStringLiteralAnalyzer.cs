// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a single-line raw string literal whose content needs none of what raw syntax is for (SST2262):
/// no quote and no backslash and no character a regular literal would have to escape, so
/// <c>"""plain text"""</c> reads the same as <c>"plain text"</c>. The inverse of promoting a literal to raw
/// syntax when doubled quotes or line breaks make raw pay off.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2262UseRegularStringLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseRegularStringLiteral);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Returns whether a raw string's content can be written verbatim in a regular literal without escapes.</summary>
    /// <param name="value">The raw string's content.</param>
    /// <returns><see langword="true"/> when no character needs a backslash escape.</returns>
    internal static bool IsPlainContent(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            // A quote or backslash would need escaping, and a control character (below the space) cannot be
            // written verbatim in a regular literal, so any of those means raw syntax is earning its keep.
            if (value[i] is '"' or '\\' or < ' ')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports a single-line raw string literal that a regular literal would state identically.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (!literal.Token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) || !IsPlainContent(literal.Token.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseRegularStringLiteral, literal.GetLocation()));
    }
}
