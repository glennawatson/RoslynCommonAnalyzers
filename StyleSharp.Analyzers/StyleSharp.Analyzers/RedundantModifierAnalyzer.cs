// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports provably redundant <c>sealed</c> and single-part <c>partial</c> modifiers (SST1419).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantModifierAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoRedundantModifier);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration);
    }

    /// <summary>Reports redundant modifier tokens on one declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberDeclarationSyntax declaration)
        {
            return;
        }

        var modifiers = declaration.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier.IsKind(SyntaxKind.SealedKeyword) && IsRedundantSealed(declaration))
            {
                Report(context, modifier);
                continue;
            }

            if (modifier.IsKind(SyntaxKind.PartialKeyword) && IsSinglePart(context, declaration))
            {
                Report(context, modifier);
            }
        }
    }

    /// <summary>Returns whether a sealed modifier has no effect.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns><see langword="true"/> when the modifier is redundant.</returns>
    private static bool IsRedundantSealed(MemberDeclarationSyntax declaration)
    {
        if (!declaration.Modifiers.Any(SyntaxKind.OverrideKeyword))
        {
            return declaration is not BaseTypeDeclarationSyntax;
        }

        return declaration.FirstAncestorOrSelf<TypeDeclarationSyntax>()?.Modifiers.Any(SyntaxKind.SealedKeyword) == true;
    }

    /// <summary>Returns whether a partial declaration has no matching part.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The declaration.</param>
    /// <returns><see langword="true"/> when only one declaration exists.</returns>
    private static bool IsSinglePart(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax declaration)
        => context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)?.DeclaringSyntaxReferences.Length == 1;

    /// <summary>Reports a redundant modifier.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="modifier">The modifier token.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxToken modifier)
        => context.ReportDiagnostic(
            Diagnostic.Create(MaintainabilityRules.NoRedundantModifier, modifier.GetLocation(), modifier.ValueText));
}
