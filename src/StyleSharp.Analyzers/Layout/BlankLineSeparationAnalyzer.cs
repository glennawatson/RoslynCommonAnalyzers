// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a blank line left inside a construct that should read as one thing.
/// </summary>
/// <remarks>
/// <para>
/// Reports SST1535 (a blank line follows a constructor initializer's <c>:</c>), SST1536 (a blank line
/// follows a conditional operator's <c>?</c> or <c>:</c>), and SST1537 (a blank line follows an expression
/// body's <c>=&gt;</c>).
/// </para>
/// <para>
/// The three ids share one analyzer because they ask the same question — how many lines separate two
/// adjacent tokens — and answering it once per declaration beats three separate walks. Each check is a pair
/// of line-table lookups, which are binary searches over a cached table rather than allocations, so a file
/// with no findings does no work beyond those lookups.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlankLineSeparationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The line delta between two tokens that means at least one whole line sits between them.</summary>
    private const int BlankLineDelta = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        LayoutRules.BlankLineAfterConstructorInitializerColon,
        LayoutRules.BlankLineAfterConditionalToken,
        LayoutRules.BlankLineAfterArrow);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeConstructorInitializer, SyntaxKind.BaseConstructorInitializer, SyntaxKind.ThisConstructorInitializer);
        context.RegisterSyntaxNodeAction(AnalyzeConditional, SyntaxKind.ConditionalExpression);
        context.RegisterSyntaxNodeAction(AnalyzeArrow, SyntaxKind.ArrowExpressionClause);
    }

    /// <summary>Reports a blank line after a constructor initializer's colon (SST1535).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConstructorInitializer(SyntaxNodeAnalysisContext context)
    {
        var initializer = (ConstructorInitializerSyntax)context.Node;
        ReportIfBlankLineFollows(
            context,
            initializer.ColonToken,
            initializer.ThisOrBaseKeyword,
            LayoutRules.BlankLineAfterConstructorInitializerColon);
    }

    /// <summary>Reports a blank line after a conditional operator's '?' or ':' (SST1536).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConditional(SyntaxNodeAnalysisContext context)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        ReportIfBlankLineFollows(
            context,
            conditional.QuestionToken,
            conditional.WhenTrue.GetFirstToken(),
            LayoutRules.BlankLineAfterConditionalToken,
            "?");
        ReportIfBlankLineFollows(
            context,
            conditional.ColonToken,
            conditional.WhenFalse.GetFirstToken(),
            LayoutRules.BlankLineAfterConditionalToken,
            ":");
    }

    /// <summary>Reports a blank line after an expression body's arrow (SST1537).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeArrow(SyntaxNodeAnalysisContext context)
    {
        var arrow = (ArrowExpressionClauseSyntax)context.Node;
        ReportIfBlankLineFollows(
            context,
            arrow.ArrowToken,
            arrow.Expression.GetFirstToken(),
            LayoutRules.BlankLineAfterArrow);
    }

    /// <summary>Reports when a whole blank line sits between a token and its continuation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="token">The token that should be followed by its continuation.</param>
    /// <param name="next">The first token of the continuation.</param>
    /// <param name="descriptor">The descriptor to report.</param>
    /// <param name="argument">The optional message argument naming the token.</param>
    /// <remarks>
    /// The continuation token is passed in rather than found with <c>GetNextToken</c>: the caller already
    /// holds the node that owns it, so a descent into that node replaces a walk up and across the tree.
    /// </remarks>
    private static void ReportIfBlankLineFollows(
        SyntaxNodeAnalysisContext context,
        SyntaxToken token,
        SyntaxToken next,
        DiagnosticDescriptor descriptor,
        string? argument = null)
    {
        if (next.IsKind(SyntaxKind.None))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var tokenLine = LayoutHelpers.EndLine(text, token);
        if (LayoutHelpers.StartLine(text, next) - tokenLine < BlankLineDelta)
        {
            return;
        }

        // A comment between the two is content, not a gap, so only report a genuinely empty line.
        if (!LayoutHelpers.IsBlankLine(text, tokenLine + 1))
        {
            return;
        }

        context.ReportDiagnostic(argument is null
            ? Diagnostic.Create(descriptor, token.GetLocation())
            : Diagnostic.Create(descriptor, token.GetLocation(), argument));
    }
}
