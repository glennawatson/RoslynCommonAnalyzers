// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped maintainability analyzer that flags modifiers which have no effect and can be removed
/// or replaced. One tree walk reports every id in the family.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1419 — a provably redundant <c>sealed</c> or single-part <c>partial</c> modifier.</description></item>
/// <item><description>SST1427 — a <c>protected</c> member of a sealed type, where <c>protected</c> has no effect.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantModifierAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.NoRedundantModifier,
        MaintainabilityRules.NoProtectedInSealed);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeRedundantSealedOrPartial,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration);
        context.RegisterSyntaxNodeAction(
            AnalyzeProtectedInSealed,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.EventFieldDeclaration);
    }

    /// <summary>Reports SST1419 for redundant <c>sealed</c> or single-part <c>partial</c> modifiers on one declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeRedundantSealedOrPartial(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberDeclarationSyntax declaration)
        {
            return;
        }

        var modifiers = declaration.Modifiers;
        var sealedModifier = default(SyntaxToken);
        var partialModifier = default(SyntaxToken);
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier.IsKind(SyntaxKind.SealedKeyword))
            {
                sealedModifier = modifier;
            }
            else if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                partialModifier = modifier;
            }
        }

        if (sealedModifier.RawKind != 0 && IsRedundantSealed(declaration))
        {
            ReportModifier(context, sealedModifier);
        }

        if (partialModifier.RawKind == 0 || !IsSinglePart(context, declaration))
        {
            return;
        }

        ReportModifier(context, partialModifier);
    }

    /// <summary>Reports SST1427 for a <c>protected</c> member of a sealed type.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeProtectedInSealed(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MemberDeclarationSyntax)context.Node;

        var protectedModifier = default(SyntaxToken);
        var modifiers = declaration.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.ProtectedKeyword))
            {
                protectedModifier = modifiers[i];
            }
            else if (modifiers[i].IsKind(SyntaxKind.OverrideKeyword))
            {
                // An override of a protected base member cannot reduce its accessibility, so leave it alone.
                return;
            }
        }

        if (protectedModifier.RawKind == 0)
        {
            return;
        }

        var symbol = GetMemberSymbol(context, declaration);
        if (symbol is not { IsOverride: false, ContainingType.IsSealed: true })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoProtectedInSealed, protectedModifier.GetLocation()));
    }

    /// <summary>Returns the symbol declared by a member declaration, handling multi-variable field declarations.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The member declaration.</param>
    /// <returns>The declared symbol, or <see langword="null"/> when none is available.</returns>
    private static ISymbol? GetMemberSymbol(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax declaration)
    {
        if (declaration is BaseFieldDeclarationSyntax field)
        {
            var variables = field.Declaration.Variables;
            return variables.Count == 0 ? null : context.SemanticModel.GetDeclaredSymbol(variables[0], context.CancellationToken);
        }

        return context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
    }

    /// <summary>Returns whether a sealed modifier has no effect.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns><see langword="true"/> when the modifier is redundant.</returns>
    private static bool IsRedundantSealed(MemberDeclarationSyntax declaration) =>
        !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.OverrideKeyword)
            ? declaration is not BaseTypeDeclarationSyntax
            : declaration.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } type
              && ModifierListHelper.Contains(type.Modifiers, SyntaxKind.SealedKeyword);

    /// <summary>Returns whether a partial declaration has no matching part.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The declaration.</param>
    /// <returns><see langword="true"/> when only one declaration exists.</returns>
    private static bool IsSinglePart(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax declaration)
        => context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)?.DeclaringSyntaxReferences.Length == 1;

    /// <summary>Reports a redundant modifier (SST1419).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="modifier">The modifier token.</param>
    private static void ReportModifier(SyntaxNodeAnalysisContext context, SyntaxToken modifier)
        => context.ReportDiagnostic(
            Diagnostic.Create(MaintainabilityRules.NoRedundantModifier, modifier.GetLocation(), modifier.ValueText));
}
