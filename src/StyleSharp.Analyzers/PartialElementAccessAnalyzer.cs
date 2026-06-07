// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a partial type or partial method that omits an access modifier (SST1205). The
/// implicit default (<c>internal</c> for top-level types, otherwise <c>private</c>) is
/// stashed so the shared access-modifier fix can apply it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PartialElementAccessAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The partial-capable declaration kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.MethodDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(OrderingRules.PartialElementAccess);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports a partial declaration that does not declare its accessibility.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(member.Modifiers, SyntaxKind.PartialKeyword) || ModifierOrdering.HasAccess(member.Modifiers))
        {
            return;
        }

        var token = MemberOrder.NameToken(member);
        var defaultModifier = member.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax ? "internal" : "private";
        var properties = ImmutableDictionary<string, string?>.Empty.Add(AccessModifierAnalyzer.ModifierKey, defaultModifier);
        context.ReportDiagnostic(Diagnostic.Create(OrderingRules.PartialElementAccess, token.GetLocation(), properties, token.ValueText));
    }
}
