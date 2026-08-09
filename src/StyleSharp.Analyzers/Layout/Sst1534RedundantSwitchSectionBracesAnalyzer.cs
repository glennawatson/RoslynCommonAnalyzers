// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a switch section whose single statement is a block that scopes nothing (SST1534). The section is
/// already a statement list, so the braces only add an indentation level.
/// </summary>
/// <remarks>
/// Every section of a switch shares one declaration space, so a block that declares a local, a local
/// function, or a pattern or <c>out</c> variable is load-bearing — without it the name collides with the same
/// name in a sibling section. Only a block that declares nothing is reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1534RedundantSwitchSectionBracesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(LayoutRules.RedundantSwitchSectionBraces);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchSection);
    }

    /// <summary>Returns whether a block introduces a name that needs the block's own scope.</summary>
    /// <param name="block">The switch section's block.</param>
    /// <returns><see langword="true"/> when removing the braces could collide with a sibling section.</returns>
    internal static bool DeclaresAnything(BlockSyntax block)
    {
        var state = false;
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, bool>(
            block,
            ref state,
            static (node, ref found) =>
            {
                if (node is not (LocalDeclarationStatementSyntax
                    or LocalFunctionStatementSyntax
                    or SingleVariableDesignationSyntax
                    or DeclarationExpressionSyntax))
                {
                    return true;
                }

                found = true;
                return false;
            });

        return state;
    }

    /// <summary>Reports one switch section whose braces scope nothing.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var section = (SwitchSectionSyntax)context.Node;
        if (section.Statements.Count != 1
            || section.Statements[0] is not BlockSyntax { Statements.Count: > 0 } block
            || DeclaresAnything(block))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            LayoutRules.RedundantSwitchSectionBraces,
            block.SyntaxTree,
            block.OpenBraceToken.Span));
    }
}
