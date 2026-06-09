// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Reports switch statements with more than the configured number of sections (SST1423).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1423TooManySwitchLabelsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default maximum number of switch sections.</summary>
    private const int DefaultMaximum = 30;

    /// <summary>The rule-specific editorconfig key.</summary>
    private const string RuleOption = "stylesharp.SST1423.max_switch_sections";

    /// <summary>The general editorconfig key.</summary>
    private const string GeneralOption = "stylesharp.max_switch_sections";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.TooManySwitchSections);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // The configured maximum can be overridden per syntax tree via .editorconfig, but it
        // does not change within a tree. Resolving options is comparatively expensive, so cache
        // the parsed maximum per tree for the lifetime of the compilation instead of re-reading
        // and re-parsing it on every switch statement (the clean path included).
        context.RegisterCompilationStartAction(start =>
        {
            var provider = start.Options.AnalyzerConfigOptionsProvider;
            var maximumByTree = new ConditionalWeakTable<SyntaxTree, StrongBox<int>>();

            // The factory is created once per compilation start (cold path) and reused for
            // every cache miss, so resolving a tree's maximum never allocates a per-node closure.
            ConditionalWeakTable<SyntaxTree, StrongBox<int>>.CreateValueCallback factory =
                tree => new StrongBox<int>(ReadMaximum(provider.GetOptions(tree)));

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, maximumByTree, factory),
                SyntaxKind.SwitchStatement);
        });
    }

    /// <summary>Reports a switch statement whose section count exceeds the configured maximum.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="maximumByTree">The per-tree cache of resolved maximums for this compilation.</param>
    /// <param name="factory">The cache-miss factory that resolves and parses a tree's maximum.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ConditionalWeakTable<SyntaxTree, StrongBox<int>> maximumByTree,
        ConditionalWeakTable<SyntaxTree, StrongBox<int>>.CreateValueCallback factory)
    {
        var statement = (SwitchStatementSyntax)context.Node;
        if (statement.Sections.Count <= 1)
        {
            return;
        }

        // Resolved once per tree and memoized; the clean path no longer re-reads or re-parses
        // editorconfig per switch statement. Per-tree overrides remain honored.
        var maximum = maximumByTree.GetValue(statement.SyntaxTree, factory).Value;
        if (statement.Sections.Count <= maximum)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MaintainabilityRules.TooManySwitchSections,
                statement.SwitchKeyword.GetLocation(),
                statement.Sections.Count,
                maximum));
    }

    /// <summary>Reads the rule-specific or general maximum, falling back to the default.</summary>
    /// <param name="options">The analyzer configuration options.</param>
    /// <returns>The positive configured maximum, or 30.</returns>
    private static int ReadMaximum(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(RuleOption, out var value) && int.TryParse(value, out var maximum) && maximum > 0)
        {
            return maximum;
        }

        return options.TryGetValue(GeneralOption, out value) && int.TryParse(value, out maximum) && maximum > 0
            ? maximum
            : DefaultMaximum;
    }
}
