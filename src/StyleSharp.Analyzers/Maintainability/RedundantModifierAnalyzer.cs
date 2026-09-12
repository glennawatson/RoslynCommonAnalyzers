// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped maintainability analyzer that flags modifiers which have no effect and can be removed
/// or replaced. One tree walk reports every id in the family.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item>
/// <description>
/// SST1419 — a provably redundant <c>sealed</c> or single-part <c>partial</c> modifier, or a
/// <c>checked</c>/<c>unchecked</c> context whose contents contain no operation an overflow check could change.
/// </description>
/// </item>
/// <item><description>SST1427 — a <c>protected</c> member of a sealed type, where <c>protected</c> has no effect.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantModifierAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        MaintainabilityRules.NoRedundantModifier,
        MaintainabilityRules.NoProtectedInSealed);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

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
        context.RegisterSyntaxNodeAction(
            AnalyzeRedundantCheckedContext,
            SyntaxKind.CheckedStatement,
            SyntaxKind.UncheckedStatement,
            SyntaxKind.CheckedExpression,
            SyntaxKind.UncheckedExpression);
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

    /// <summary>Reports SST1419 for a <c>checked</c>/<c>unchecked</c> context that guards nothing.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// A <c>checked</c> or <c>unchecked</c> context only changes the result of integer arithmetic and narrowing
    /// numeric conversions written directly inside it. When its contents contain none of those — no <c>+ - *</c>
    /// / <c>%</c>, no increment or decrement, no unary negation, and no cast — the context does nothing and is
    /// removed. The judgement is deliberately conservative: any such operation anywhere beneath the context,
    /// including inside a nested one, keeps it, so the rule never reports a context where overflow is possible.
    /// </remarks>
    private static void AnalyzeRedundantCheckedContext(SyntaxNodeAnalysisContext context)
    {
        var keyword = context.Node switch
        {
            CheckedStatementSyntax statement => statement.Keyword,
            CheckedExpressionSyntax expression => expression.Keyword,
            _ => default,
        };

        if (keyword.RawKind == 0 || ContainsOverflowCapableOperation(context.Node))
        {
            return;
        }

        ReportModifier(context, keyword);
    }

    /// <summary>Returns whether a node contains an operation whose result an overflow check could change.</summary>
    /// <param name="root">The <c>checked</c>/<c>unchecked</c> statement or expression.</param>
    /// <returns><see langword="true"/> when the context guards a real overflow-capable operation.</returns>
    private static bool ContainsOverflowCapableOperation(SyntaxNode root)
    {
        var found = false;
        _ = DescendantTraversalHelper.VisitDescendants(
            root,
            ref found,
            static (SyntaxNode node, ref bool state) =>
            {
                if (!IsOverflowCapable(node.Kind()))
                {
                    return true;
                }

                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Returns whether a node kind is an operation a <c>checked</c>/<c>unchecked</c> context can affect.</summary>
    /// <param name="kind">The node's syntax kind.</param>
    /// <returns><see langword="true"/> for integer arithmetic, increment/decrement, unary negation, and casts.</returns>
    private static bool IsOverflowCapable(SyntaxKind kind) =>
        IsBinaryArithmetic(kind) || IsUnaryArithmetic(kind) || IsArithmeticAssignmentOrCast(kind);

    /// <summary>Returns whether a node kind is a binary arithmetic operator.</summary>
    /// <param name="kind">The node's syntax kind.</param>
    /// <returns><see langword="true"/> for <c>+ - * /</c> and <c>%</c>.</returns>
    private static bool IsBinaryArithmetic(SyntaxKind kind) =>
        kind is SyntaxKind.AddExpression
            or SyntaxKind.SubtractExpression
            or SyntaxKind.MultiplyExpression
            or SyntaxKind.DivideExpression
            or SyntaxKind.ModuloExpression;

    /// <summary>Returns whether a node kind is a unary arithmetic operator.</summary>
    /// <param name="kind">The node's syntax kind.</param>
    /// <returns><see langword="true"/> for negation and increment/decrement.</returns>
    private static bool IsUnaryArithmetic(SyntaxKind kind) =>
        kind is SyntaxKind.UnaryMinusExpression
            or SyntaxKind.PreIncrementExpression
            or SyntaxKind.PreDecrementExpression
            or SyntaxKind.PostIncrementExpression
            or SyntaxKind.PostDecrementExpression;

    /// <summary>Returns whether a node kind is an arithmetic compound assignment or a cast.</summary>
    /// <param name="kind">The node's syntax kind.</param>
    /// <returns><see langword="true"/> for <c>+= -= *= /= %=</c> and a cast expression.</returns>
    private static bool IsArithmeticAssignmentOrCast(SyntaxKind kind) =>
        kind is SyntaxKind.AddAssignmentExpression
            or SyntaxKind.SubtractAssignmentExpression
            or SyntaxKind.MultiplyAssignmentExpression
            or SyntaxKind.DivideAssignmentExpression
            or SyntaxKind.ModuloAssignmentExpression
            or SyntaxKind.CastExpression;

    /// <summary>Returns the symbol declared by a member declaration, handling multi-variable field declarations.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The member declaration.</param>
    /// <returns>The declared symbol, or <see langword="null"/> when none is available.</returns>
    private static ISymbol? GetMemberSymbol(in SyntaxNodeAnalysisContext context, MemberDeclarationSyntax declaration)
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
            ? declaration is not BaseTypeDeclarationSyntax && !SealsAnInterfaceDefault(declaration)
            : declaration.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } type
              && ModifierListHelper.Contains(type.Modifiers, SyntaxKind.SealedKeyword);

    /// <summary>Returns whether <c>sealed</c> suppresses an interface member's implicit virtual.</summary>
    /// <param name="declaration">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is an instance member of an interface.</returns>
    /// <remarks>
    /// An instance member of an interface is virtual by default, so <c>sealed</c> is what stops an
    /// implementer from replacing the default implementation — and the compiler requires a body once it is
    /// written (CS0501). A static member is not: it is non-virtual unless declared <c>static virtual</c> or
    /// <c>static abstract</c>, so <c>sealed</c> seals nothing there. Written on one in a derived interface it
    /// does not even reach the base member, which the compiler reports as hiding it (CS0108).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SealsAnInterfaceDefault(MemberDeclarationSyntax declaration) =>
        declaration.Parent is InterfaceDeclarationSyntax
            && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword);

    /// <summary>Returns whether a partial declaration has no matching part.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The declaration.</param>
    /// <returns><see langword="true"/> when only one declaration exists.</returns>
    private static bool IsSinglePart(in SyntaxNodeAnalysisContext context, MemberDeclarationSyntax declaration) =>
        context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)?.DeclaringSyntaxReferences.Length == 1;

    /// <summary>Reports a redundant modifier (SST1419).</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="modifier">The modifier token.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReportModifier(in SyntaxNodeAnalysisContext context, SyntaxToken modifier) =>
        context.ReportDiagnostic(
            Diagnostic.Create(MaintainabilityRules.NoRedundantModifier, modifier.GetLocation(), modifier.ValueText));
}
