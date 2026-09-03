// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>as</c> conversion whose result is dereferenced without a null check (SST2454):
/// <c>(value as Widget).Name</c> throws a <see cref="NullReferenceException"/> when the conversion fails,
/// which is the outcome <c>as</c> was chosen to avoid.
/// </summary>
/// <remarks>
/// The whole match is syntactic — the shape of the expression around the <c>as</c> — so nothing binds and the
/// clean path costs one walk up through the enclosing parentheses.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2454UncheckedAsDereferenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.UncheckedAsDereference);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AsExpression);
    }

    /// <summary>Reports one <c>as</c> conversion that is dereferenced straight away.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var conversion = (BinaryExpressionSyntax)context.Node;
        if (!IsDereferencedWithoutCheck(conversion))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.UncheckedAsDereference,
            conversion.SyntaxTree,
            conversion.Span));
    }

    /// <summary>Returns whether an expression's enclosing context dereferences it with no null check.</summary>
    /// <param name="conversion">The <c>as</c> conversion.</param>
    /// <returns><see langword="true"/> when a member access, index, or call reads through the result.</returns>
    /// <remarks>
    /// A conditional access (<c>?.</c>, <c>?[]</c>) is the null check this rule asks for, so an <c>as</c> that
    /// feeds one is left alone. Enclosing parentheses are walked through, because they are what makes the
    /// dereference parse in the first place.
    /// </remarks>
    private static bool IsDereferencedWithoutCheck(ExpressionSyntax conversion)
    {
        var current = (SyntaxNode)conversion;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == current,
            ElementAccessExpressionSyntax element => element.Expression == current,
            _ => false,
        };
    }
}
