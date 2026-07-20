// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>switch</c> statement that lexically contains another <c>switch</c> statement inside one of
/// its sections (SST2252), where two multi-way branches stack at one point and read better split apart.
/// </summary>
/// <remarks>
/// <para>
/// The check is purely syntactic and needs no semantic model. Every <c>switch</c> statement is treated as a
/// candidate inner switch; its ancestor chain is walked outward until it meets either an enclosing
/// <c>switch</c> statement — in which case the inner one sits in that switch's section, because a statement
/// can live nowhere else inside a switch, and its <c>switch</c> keyword is reported — or a body boundary that
/// ends the search.
/// </para>
/// <para>
/// A lambda, a local function, and a member declaration (a method, a nested type) each end the walk. A switch
/// inside a lambda or local function declared in a section is that body's own concern, not nesting, and no
/// switch statement encloses another across a member boundary. A <c>switch</c> <em>expression</em> is never
/// reported: it is the preferred compact form, and only a switch <em>statement</em> is a candidate on either
/// side of the nesting.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2252NestedSwitchAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.AvoidNestedSwitchStatement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SwitchStatement);
    }

    /// <summary>Reports a switch statement nested inside another switch statement's section.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var inner = (SwitchStatementSyntax)context.Node;
        for (var ancestor = inner.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is SwitchStatementSyntax)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    ModernSyntaxRules.AvoidNestedSwitchStatement,
                    inner.SwitchKeyword.GetLocation()));
                return;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax)
            {
                return;
            }
        }
    }
}
