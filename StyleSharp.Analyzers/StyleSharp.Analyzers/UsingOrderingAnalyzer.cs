// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports using directives that are placed inside a namespace (SST1200) or that break the
/// canonical ordering: System directives first (SST1208), alias directives last (SST1209),
/// regular directives alphabetical (SST1210), alias directives alphabetical (SST1211),
/// static directives in the correct location (SST1216) and alphabetical (SST1217).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsingOrderingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The containers whose using lists are inspected.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.CompilationUnit,
        SyntaxKind.NamespaceDeclaration,
        SyntaxKind.FileScopedNamespaceDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        OrderingRules.UsingDirectivesPlacement,
        OrderingRules.SystemUsingsFirst,
        OrderingRules.AliasUsingsLast,
        OrderingRules.RegularUsingsAlphabetical,
        OrderingRules.AliasUsingsAlphabetical,
        OrderingRules.StaticUsingsPlacement,
        OrderingRules.StaticUsingsAlphabetical);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Checks a container's using list for placement and ordering violations.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var usings = Usings(context.Node);
        if (usings.Count == 0)
        {
            return;
        }

        if (context.Node is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax)
        {
            foreach (var directive in usings)
            {
                context.ReportDiagnostic(Diagnostic.Create(OrderingRules.UsingDirectivesPlacement, directive.GetLocation()));
            }
        }

        for (var index = 1; index < usings.Count; index++)
        {
            CheckPair(context, usings[index - 1], usings[index]);
        }
    }

    /// <summary>Returns the using list of a supported container.</summary>
    /// <param name="node">The container node.</param>
    /// <returns>The container's using directives.</returns>
    private static SyntaxList<UsingDirectiveSyntax> Usings(SyntaxNode node) => node switch
    {
        CompilationUnitSyntax unit => unit.Usings,
        NamespaceDeclarationSyntax ns => ns.Usings,
        FileScopedNamespaceDeclarationSyntax file => file.Usings,
        _ => default,
    };

    /// <summary>Reports the ordering violation, if any, between two adjacent using directives.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="previous">The earlier directive.</param>
    /// <param name="current">The later directive.</param>
    private static void CheckPair(SyntaxNodeAnalysisContext context, UsingDirectiveSyntax previous, UsingDirectiveSyntax current)
    {
        var previousGroup = UsingClassification.Group(previous);
        var currentGroup = UsingClassification.Group(current);
        if (previousGroup != currentGroup)
        {
            CheckGroupOrder(context, previous, previousGroup, currentGroup);
            return;
        }

        if (previousGroup == UsingClassification.RegularGroup)
        {
            CheckRegular(context, previous, current);
            return;
        }

        var rule = previousGroup == UsingClassification.StaticGroup
            ? OrderingRules.StaticUsingsAlphabetical
            : OrderingRules.AliasUsingsAlphabetical;
        if (string.CompareOrdinal(UsingClassification.SortKey(previous), UsingClassification.SortKey(current)) <= 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, current.GetLocation()));
    }

    /// <summary>Reports an out-of-order group (alias or static directive that appears too early).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="previous">The earlier directive.</param>
    /// <param name="previousGroup">The earlier directive's group.</param>
    /// <param name="currentGroup">The later directive's group.</param>
    private static void CheckGroupOrder(SyntaxNodeAnalysisContext context, UsingDirectiveSyntax previous, int previousGroup, int currentGroup)
    {
        if (previousGroup <= currentGroup)
        {
            return;
        }

        var rule = previousGroup == UsingClassification.AliasGroup
            ? OrderingRules.AliasUsingsLast
            : OrderingRules.StaticUsingsPlacement;
        context.ReportDiagnostic(Diagnostic.Create(rule, previous.GetLocation()));
    }

    /// <summary>Reports a System-first or alphabetical violation between two regular directives.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="previous">The earlier directive.</param>
    /// <param name="current">The later directive.</param>
    private static void CheckRegular(SyntaxNodeAnalysisContext context, UsingDirectiveSyntax previous, UsingDirectiveSyntax current)
    {
        var previousSystem = UsingClassification.IsSystem(previous);
        var currentSystem = UsingClassification.IsSystem(current);
        if (previousSystem != currentSystem)
        {
            if (!previousSystem && currentSystem)
            {
                context.ReportDiagnostic(Diagnostic.Create(OrderingRules.SystemUsingsFirst, current.GetLocation()));
            }

            return;
        }

        if (string.CompareOrdinal(UsingClassification.SortKey(previous), UsingClassification.SortKey(current)) <= 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(OrderingRules.RegularUsingsAlphabetical, current.GetLocation()));
    }
}
