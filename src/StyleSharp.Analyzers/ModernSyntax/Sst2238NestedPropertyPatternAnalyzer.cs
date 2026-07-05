// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports nested property patterns that can be flattened into an extended property pattern (SST2238).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2238NestedPropertyPatternAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 10 language-version value.</summary>
    private const int CSharp10 = 1000;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.SimplifyNestedPropertyPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeSubpattern, SyntaxKind.Subpattern);
    }

    /// <summary>Reports subpatterns whose value is another property-only recursive pattern.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSubpattern(SyntaxNodeAnalysisContext context)
    {
        var subpattern = (SubpatternSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(subpattern, CSharp10)
            || subpattern.NameColon is null
            || subpattern.Pattern is not RecursivePatternSyntax nested
            || !IsPropertyOnlyPattern(nested))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.SimplifyNestedPropertyPattern, nested.GetLocation()));
    }

    /// <summary>Returns whether a recursive pattern contains only property subpatterns.</summary>
    /// <param name="pattern">The recursive pattern.</param>
    /// <returns><see langword="true"/> when it can be flattened into an extended property path.</returns>
    private static bool IsPropertyOnlyPattern(RecursivePatternSyntax pattern)
        => pattern.PositionalPatternClause is null
            && pattern.PropertyPatternClause is { Subpatterns.Count: > 0 }
            && pattern.Designation is null;

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
