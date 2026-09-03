// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a control-flow statement whose child statement is not wrapped in braces (SST1503),
/// whether the child is on one line or many. An <c>else if</c> chain is not treated as an
/// unbraced child. This is the strict always-braces rule; the repository disables it by
/// default in favour of SST1519/SST1520.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1503RequireBracesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(LayoutRules.BracesRequired);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, LayoutHelpers.EmbeddedStatementKinds());
    }

    /// <summary>Reports an embedded statement that omits its braces.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!LayoutHelpers.TryGetEmbeddedStatement(context.Node, out var child)
            || child is BlockSyntax or IfStatementSyntax
            || IsStackedUsing(context.Node, child))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesRequired, context.Node.GetFirstToken().GetLocation()));
    }

    /// <summary>Returns whether a using statement carries another using statement as its body.</summary>
    /// <param name="node">The control-flow node being inspected.</param>
    /// <param name="child">Its embedded statement.</param>
    /// <returns><see langword="true"/> when the pair is a stacked using.</returns>
    /// <remarks>
    /// Stacking is how the language opens several resources over a single body, and the braces belong on the
    /// innermost statement — which this rule still reports if it goes without them. Flagging the outer one asks
    /// for exactly the nesting the shape exists to avoid, and there is no flatter rewrite to offer: a using
    /// declaration only replaces the resource form, and it changes the scope the block was pinning.
    /// </remarks>
    private static bool IsStackedUsing(SyntaxNode node, StatementSyntax child)
        => node is UsingStatementSyntax && child is UsingStatementSyntax;
}
