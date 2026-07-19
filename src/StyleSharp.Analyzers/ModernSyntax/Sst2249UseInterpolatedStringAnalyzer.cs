// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a composite <c>string.Format</c> call, or a concatenation that splices values between
/// string literals, that reads more clearly as an interpolated string (SST2249):
/// <c>string.Format("{0} {1}", a, b)</c> and <c>"Hello " + name + "!"</c> become <c>$"{a} {b}"</c>
/// and <c>$"Hello {name}!"</c>.
/// </summary>
/// <remarks>
/// The rule reports only when it has already built the interpolated string and proved it compiles to
/// the same value, so a <c>string.Format</c> that passes an explicit format provider, a chain whose
/// leading operands add as numbers, and a format whose placeholders it cannot reproduce are all left
/// alone. The clean path is syntactic: the semantic model is consulted only after a call or chain
/// looks convertible.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2249UseInterpolatedStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 6 language-version value that first allowed interpolated strings.</summary>
    private const int CSharp6 = 600;

    /// <summary>The message argument naming a composite format call.</summary>
    private const string FormatSourceDescription = "'string.Format' call";

    /// <summary>The message argument naming a concatenation chain.</summary>
    private const string ConcatenationSourceDescription = "string concatenation";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseInterpolatedString);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeConcatenation, SyntaxKind.AddExpression);
    }

    /// <summary>Reports a composite <c>string.Format</c> call that can be an interpolated string.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!SupportsInterpolation(invocation.SyntaxTree)
            || !InterpolatedStringConversion.IsFormatShape(invocation)
            || InterpolatedStringConversion.TryConvertFormat(context.SemanticModel, invocation, context.CancellationToken) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseInterpolatedString, invocation.GetLocation(), FormatSourceDescription));
    }

    /// <summary>Reports a literal-plus-value concatenation that can be an interpolated string.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConcatenation(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!SupportsInterpolation(binary.SyntaxTree)
            || !InterpolatedStringConversion.IsConcatenationCandidate(binary)
            || InterpolatedStringConversion.TryConvertConcatenation(context.SemanticModel, binary, context.CancellationToken) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseInterpolatedString, binary.GetLocation(), ConcatenationSourceDescription));
    }

    /// <summary>Returns whether a syntax tree is parsed at a language version that has interpolated strings.</summary>
    /// <param name="tree">The syntax tree.</param>
    /// <returns><see langword="true"/> for C# 6 or later.</returns>
    private static bool SupportsInterpolation(SyntaxTree tree)
        => tree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= CSharp6;
}
