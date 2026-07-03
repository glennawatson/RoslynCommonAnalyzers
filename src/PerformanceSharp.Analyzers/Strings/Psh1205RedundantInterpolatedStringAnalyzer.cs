// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports interpolated strings whose interpolation does no work (PSH1205). Two
/// shapes qualify: a single hole with no alignment and no format clause whose
/// expression is already a <see cref="string"/> (the value itself is cheaper), and
/// an interpolated string with no holes at all (a plain literal is free). Strings
/// whose converted type is not <see cref="string"/> — <c>FormattableString</c>,
/// <c>IFormattable</c>, or a custom interpolated-string handler — are skipped
/// because the interpolated form carries structure a plain value cannot.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1205RedundantInterpolatedStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The message argument used for the single-string-hole shape.</summary>
    internal const string ValueItselfArgument = "the value itself";

    /// <summary>The message argument used for the no-holes shape.</summary>
    internal const string PlainLiteralArgument = "a plain string literal";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.RedundantInterpolatedString);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInterpolatedString, SyntaxKind.InterpolatedStringExpression);
    }

    /// <summary>Classifies an interpolated string into one of the redundant shapes, syntactically.</summary>
    /// <param name="interpolated">The interpolated string to probe.</param>
    /// <param name="singleInterpolation">The lone bare interpolation for the single-hole shape; <see langword="null"/> for the text-only shape.</param>
    /// <returns><see langword="true"/> for exactly one hole without alignment or format clause, or for contents that are only literal text (possibly empty).</returns>
    internal static bool TryClassify(InterpolatedStringExpressionSyntax interpolated, out InterpolationSyntax? singleInterpolation)
    {
        var contents = interpolated.Contents;
        if (contents.Count == 1 && contents[0] is InterpolationSyntax { AlignmentClause: null, FormatClause: null } interpolation)
        {
            singleInterpolation = interpolation;
            return true;
        }

        singleInterpolation = null;
        for (var i = 0; i < contents.Count; i++)
        {
            if (contents[i] is not InterpolatedStringTextSyntax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports PSH1205 for an interpolated string whose interpolation does no work.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInterpolatedString(SyntaxNodeAnalysisContext context)
    {
        var interpolated = (InterpolatedStringExpressionSyntax)context.Node;
        if (!TryClassify(interpolated, out var singleInterpolation))
        {
            return;
        }

        var model = context.SemanticModel;
        if (singleInterpolation is not null
            && model.GetTypeInfo(singleInterpolation.Expression, context.CancellationToken).Type is not { SpecialType: SpecialType.System_String })
        {
            return;
        }

        if (model.GetTypeInfo(interpolated, context.CancellationToken).ConvertedType is not { SpecialType: SpecialType.System_String })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.RedundantInterpolatedString,
            interpolated.SyntaxTree,
            interpolated.Span,
            singleInterpolation is not null ? ValueItselfArgument : PlainLiteralArgument));
    }
}
