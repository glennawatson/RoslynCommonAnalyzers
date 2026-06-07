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
            for (var index = 0; index < usings.Count; index++)
            {
                var directive = usings[index];
                context.ReportDiagnostic(DiagnosticHelper.Create(OrderingRules.UsingDirectivesPlacement, directive.SyntaxTree, directive.Span));
            }
        }

        if (usings.Count == 1)
        {
            return;
        }

        var previous = CreateDirectiveData(usings[0]);
        for (var index = 1; index < usings.Count; index++)
        {
            var current = CreateDirectiveData(usings[index]);
            CheckPair(context, previous, current);
            previous = current;
        }
    }

    /// <summary>Precomputes the ordering metadata for one using directive.</summary>
    /// <param name="directive">The using directive.</param>
    /// <returns>The computed ordering metadata.</returns>
    private static UsingDirectiveData CreateDirectiveData(UsingDirectiveSyntax directive)
    {
        var group = UsingClassification.Group(directive);
        return new(directive, group, group == UsingClassification.RegularGroup && UsingClassification.IsSystem(directive));
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
    private static void CheckPair(SyntaxNodeAnalysisContext context, in UsingDirectiveData previous, in UsingDirectiveData current)
    {
        var previousGroup = previous.Group;
        var currentGroup = current.Group;
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
        if (UsingClassification.CompareSortKey(previous.Directive, current.Directive) <= 0)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(rule, current.Directive.SyntaxTree, current.Directive.Span));
    }

    /// <summary>Reports an out-of-order group (alias or static directive that appears too early).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="previous">The earlier directive.</param>
    /// <param name="previousGroup">The earlier directive's group.</param>
    /// <param name="currentGroup">The later directive's group.</param>
    private static void CheckGroupOrder(SyntaxNodeAnalysisContext context, in UsingDirectiveData previous, int previousGroup, int currentGroup)
    {
        if (previousGroup <= currentGroup)
        {
            return;
        }

        var rule = previousGroup == UsingClassification.AliasGroup
            ? OrderingRules.AliasUsingsLast
            : OrderingRules.StaticUsingsPlacement;
        context.ReportDiagnostic(DiagnosticHelper.Create(rule, previous.Directive.SyntaxTree, previous.Directive.Span));
    }

    /// <summary>Reports a System-first or alphabetical violation between two regular directives.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="previous">The earlier directive.</param>
    /// <param name="current">The later directive.</param>
    private static void CheckRegular(SyntaxNodeAnalysisContext context, in UsingDirectiveData previous, in UsingDirectiveData current)
    {
        var previousSystem = previous.IsSystem;
        var currentSystem = current.IsSystem;
        if (previousSystem != currentSystem)
        {
            if (!previousSystem && currentSystem)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(OrderingRules.SystemUsingsFirst, current.Directive.SyntaxTree, current.Directive.Span));
            }

            return;
        }

        if (UsingClassification.CompareSortKey(previous.Directive, current.Directive) <= 0)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(OrderingRules.RegularUsingsAlphabetical, current.Directive.SyntaxTree, current.Directive.Span));
    }

    /// <summary>Precomputed ordering data for one using directive.</summary>
    /// <param name="Directive">The using directive syntax.</param>
    /// <param name="Group">The directive's ordering group.</param>
    /// <param name="IsSystem">Whether the directive targets <c>System</c>.</param>
    private readonly record struct UsingDirectiveData(UsingDirectiveSyntax Directive, int Group, bool IsSystem);
}
