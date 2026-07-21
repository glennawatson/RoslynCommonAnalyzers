// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a wrapped fluent call-chain link — a member access <c>.</c> or a conditional access
/// <c>?.</c> — whose line break sits on the wrong side (SST1529), configured with
/// <c>stylesharp.null_conditional_new_line</c> (<c>before</c> | <c>after</c>; default <c>before</c>).
/// Each chain link is checked once, so a wrapped chain is held to one placement; a link with no
/// adjacent break is never touched.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1529NullConditionalNewLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the call-chain operator placement (SST1529).</summary>
    internal const string SpecificKey = "stylesharp.SST1529.null_conditional_new_line";

    /// <summary>General editorconfig key for the call-chain operator placement.</summary>
    internal const string GeneralKey = "stylesharp.null_conditional_new_line";

    /// <summary>The chain-link node kinds whose wrapped operator placement is checked.</summary>
    private static readonly ImmutableArray<SyntaxKind> LinkKinds = ImmutableArrays.Of(
        SyntaxKind.SimpleMemberAccessExpression,
        SyntaxKind.ConditionalAccessExpression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.NullConditionalNewLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, LinkKinds);
    }

    /// <summary>Reports a wrapped chain link on the wrong side of its line break.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!LayoutHelpers.TryGetChainLink(context.Node, out var leadToken, out var afterToken, out _))
        {
            return;
        }

        var breakBefore = LayoutHelpers.HasLineBreakBefore(leadToken);
        var breakAfter = LayoutHelpers.HasLineBreakAfter(afterToken);
        if (!breakBefore && !breakAfter)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        var wantBreakBefore = LayoutStyleOptions.ReadBreakBefore(options, SpecificKey, GeneralKey, defaultBreakBefore: true);
        if (wantBreakBefore ? !breakAfter : !breakBefore)
        {
            return;
        }

        var display = context.Node.IsKind(SyntaxKind.ConditionalAccessExpression) ? "?." : ".";
        context.ReportDiagnostic(Diagnostic.Create(
            LayoutRules.NullConditionalNewLine,
            leadToken.GetLocation(),
            LayoutHelpers.PlacementProperties(wantBreakBefore),
            display,
            wantBreakBefore ? "start" : "end"));
    }
}
