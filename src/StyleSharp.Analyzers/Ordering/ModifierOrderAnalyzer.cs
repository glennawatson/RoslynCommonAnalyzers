// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports modifiers that are not in the canonical order (SST1206) and the special case of
/// <c>internal</c> appearing before <c>protected</c> (SST1207). The relative order of access
/// modifiers is handled by SST1207; everything else by SST1206.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModifierOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The declaration kinds whose modifier lists are inspected.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.FieldDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ConversionOperatorDeclaration,
        SyntaxKind.GetAccessorDeclaration,
        SyntaxKind.SetAccessorDeclaration,
        SyntaxKind.InitAccessorDeclaration,
        SyntaxKind.AddAccessorDeclaration,
        SyntaxKind.RemoveAccessorDeclaration,
        SyntaxKind.LocalFunctionStatement);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        OrderingRules.DeclarationKeywordOrder,
        OrderingRules.ProtectedBeforeInternal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports the first out-of-order modifier and any internal-before-protected pair.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var modifiers = ModifierOrdering.Modifiers(context.Node);
        if (modifiers.Count < 2)
        {
            return;
        }

        ReportKeywordOrder(context, modifiers);
        ReportAccessOrder(context, modifiers);
    }

    /// <summary>Reports the first modifier whose rank is lower than that of the modifier before it.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="modifiers">The modifier list.</param>
    private static void ReportKeywordOrder(SyntaxNodeAnalysisContext context, SyntaxTokenList modifiers)
    {
        for (var index = 1; index < modifiers.Count; index++)
        {
            if (ModifierOrdering.Rank(modifiers[index - 1]) > ModifierOrdering.Rank(modifiers[index]))
            {
                context.ReportDiagnostic(Diagnostic.Create(OrderingRules.DeclarationKeywordOrder, modifiers[index].GetLocation(), modifiers[index].Text));
                return;
            }
        }
    }

    /// <summary>Reports the first access modifier that should precede an earlier access modifier.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="modifiers">The modifier list.</param>
    private static void ReportAccessOrder(SyntaxNodeAnalysisContext context, SyntaxTokenList modifiers)
    {
        var previousAccess = -1;
        foreach (var modifier in modifiers)
        {
            if (!ModifierOrdering.IsAccess(modifier))
            {
                continue;
            }

            var rank = ModifierOrdering.AccessRank(modifier);
            if (previousAccess >= 0 && rank < previousAccess)
            {
                context.ReportDiagnostic(Diagnostic.Create(OrderingRules.ProtectedBeforeInternal, modifier.GetLocation()));
                return;
            }

            previousAccess = rank;
        }
    }
}
