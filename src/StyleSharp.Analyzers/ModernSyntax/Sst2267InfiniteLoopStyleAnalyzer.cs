// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an infinite loop written in the form the codebase did not choose (SST2267): a
/// <c>for (;;)</c> when <c>stylesharp.infinite_loop_style</c> is <c>while</c> (the default), or a
/// <c>while (true)</c> when it is <c>for</c>. The rule is opt-in and off by default because the two
/// forms compile identically and the choice between them is a house-style preference.
/// </summary>
/// <remarks>
/// Only the two canonical infinite loops are candidates: a <c>for</c> with no declaration, initializer,
/// condition, or incrementor, and a <c>while</c> whose condition is the literal <c>true</c>. The option is
/// read only after one of those shapes is matched, so a source with no infinite loops pays nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2267InfiniteLoopStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The target text reported and offered when normalizing to a <c>while</c> loop.</summary>
    internal const string WhileTarget = "while (true)";

    /// <summary>The target text reported and offered when normalizing to a <c>for</c> loop.</summary>
    internal const string ForTarget = "for (;;)";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.NormalizeInfiniteLoopStyle);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeFor, SyntaxKind.ForStatement);
        context.RegisterSyntaxNodeAction(AnalyzeWhile, SyntaxKind.WhileStatement);
    }

    /// <summary>Returns whether a <c>for</c> statement is the canonical <c>for (;;)</c> infinite loop.</summary>
    /// <param name="statement">The loop to inspect.</param>
    /// <returns><see langword="true"/> when the loop has no clauses at all.</returns>
    internal static bool IsForeverFor(ForStatementSyntax statement)
        => statement.Condition is null
            && statement.Declaration is null
            && statement.Initializers.Count == 0
            && statement.Incrementors.Count == 0;

    /// <summary>Returns whether a <c>while</c> statement is the canonical <c>while (true)</c> infinite loop.</summary>
    /// <param name="statement">The loop to inspect.</param>
    /// <returns><see langword="true"/> when the condition is the literal <c>true</c>.</returns>
    internal static bool IsForeverWhile(WhileStatementSyntax statement)
        => statement.Condition.IsKind(SyntaxKind.TrueLiteralExpression);

    /// <summary>Reports a <c>for (;;)</c> loop when the codebase prefers <c>while (true)</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeFor(SyntaxNodeAnalysisContext context)
    {
        var statement = (ForStatementSyntax)context.Node;
        if (!IsForeverFor(statement))
        {
            return;
        }

        var style = ModernSyntaxStyleOptions.ReadInfiniteLoopStyle(context.Options.AnalyzerConfigOptionsProvider.GetOptions(statement.SyntaxTree));
        if (style != InfiniteLoopStyle.While)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeInfiniteLoopStyle, statement.ForKeyword.GetLocation(), WhileTarget));
    }

    /// <summary>Reports a <c>while (true)</c> loop when the codebase prefers <c>for (;;)</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeWhile(SyntaxNodeAnalysisContext context)
    {
        var statement = (WhileStatementSyntax)context.Node;
        if (!IsForeverWhile(statement))
        {
            return;
        }

        var style = ModernSyntaxStyleOptions.ReadInfiniteLoopStyle(context.Options.AnalyzerConfigOptionsProvider.GetOptions(statement.SyntaxTree));
        if (style != InfiniteLoopStyle.For)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeInfiniteLoopStyle, statement.WhileKeyword.GetLocation(), ForTarget));
    }
}
