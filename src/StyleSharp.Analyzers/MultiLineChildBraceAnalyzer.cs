// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a control-flow statement whose embedded child spans multiple lines but omits its
/// braces (SST1519). A single-line child is left to Sonar's S121; an <c>else if</c> chain is
/// not treated as an unbraced child.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultiLineChildBraceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.BracesForMultiLineChild);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, LayoutHelpers.EmbeddedStatementKinds());
    }

    /// <summary>Reports a multi-line embedded statement that is not wrapped in braces.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!LayoutHelpers.TryGetEmbeddedStatement(context.Node, out var child)
            || child is BlockSyntax or IfStatementSyntax)
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.StartLine(text, child.GetFirstToken()) == LayoutHelpers.EndLine(text, child.GetLastToken()))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.BracesForMultiLineChild, context.Node.GetFirstToken().GetLocation()));
    }
}
