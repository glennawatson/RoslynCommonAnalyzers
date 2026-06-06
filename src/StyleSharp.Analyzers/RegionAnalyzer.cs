// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>#region</c> directives (SST1124) and, more specifically, regions placed inside a code
/// element body (SST1123). Regions hide code from a casual reader; a well-factored type does not
/// need them, and a region buried inside a method body is especially confusing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.DoNotUseRegions,
        ReadabilityRules.RegionWithinElement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports every region directive in the tree.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (!trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
            {
                continue;
            }

            var location = Location.Create(context.Tree, trivia.Span);
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotUseRegions, location));

            if (IsWithinElement(trivia))
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.RegionWithinElement, location));
            }
        }
    }

    /// <summary>Returns whether the region directive sits inside a statement block (a code element body).</summary>
    /// <param name="trivia">The region directive trivia.</param>
    /// <returns><see langword="true"/> when the region is nested inside executable code.</returns>
    private static bool IsWithinElement(SyntaxTrivia trivia)
        => trivia.Token.Parent?.FirstAncestorOrSelf<BlockSyntax>() is not null;
}
