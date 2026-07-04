// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags trivial expression-bodied forwarder methods and operators that lack
/// <c>MethodImplOptions.AggressiveInlining</c> (PSH1410). A one-expression forwarder — a
/// delegation, member read, or constant — can still be skipped by the JIT's IL-size inlining
/// heuristics; the attribute makes the intent explicit. Virtual, abstract, override, async,
/// extern, partial, and interface members are skipped, as is anything already carrying a
/// MethodImpl attribute. Blanket inlining attributes are an opinionated convention, so the
/// rule is opt-in. Gated on <c>MethodImplOptions.AggressiveInlining</c> existing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1410AggressiveInliningAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute simple names that mark an explicit inlining decision.</summary>
    internal const string MethodImplAttributeShortName = "MethodImpl";

    /// <summary>The metadata name of the options enum the attribute takes.</summary>
    private const string MethodImplOptionsMetadataName = "System.Runtime.CompilerServices.MethodImplOptions";

    /// <summary>The flag member the rule suggests.</summary>
    private const string AggressiveInliningMemberName = "AggressiveInlining";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.InlineTrivialForwarders);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(MethodImplOptionsMetadataName) is not { } options
                || options.GetMembers(AggressiveInliningMemberName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration, SyntaxKind.OperatorDeclaration);
        });
    }

    /// <summary>Returns whether a method declaration is a trivial forwarder eligible for the attribute.</summary>
    /// <param name="declaration">The method or operator declaration.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsEligibleForwarder(BaseMethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is null
            || declaration.Parent is InterfaceDeclarationSyntax
            || HasDisqualifyingModifier(declaration.Modifiers)
            || HasMethodImplAttribute(declaration.AttributeLists))
        {
            return false;
        }

        return IsForwardingExpression(declaration.ExpressionBody.Expression);
    }

    /// <summary>Returns whether an expression is a plain forward: a call, member read, index, or constant.</summary>
    /// <param name="expression">The body expression.</param>
    /// <returns><see langword="true"/> for forwarding shapes.</returns>
    private static bool IsForwardingExpression(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax or MemberAccessExpressionSyntax or IdentifierNameSyntax
            or ElementAccessExpressionSyntax or ConditionalAccessExpressionSyntax or LiteralExpressionSyntax
            or ObjectCreationExpressionSyntax;

    /// <summary>Returns whether the modifier list rules the member out.</summary>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <returns><see langword="true"/> for virtual-dispatch, async, extern, and partial members.</returns>
    private static bool HasDisqualifyingModifier(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            var kind = modifiers[i].Kind();
            if (kind is SyntaxKind.VirtualKeyword or SyntaxKind.AbstractKeyword or SyntaxKind.OverrideKeyword
                or SyntaxKind.AsyncKeyword or SyntaxKind.ExternKeyword or SyntaxKind.PartialKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any attribute is a MethodImpl attribute, by simple name.</summary>
    /// <param name="attributeLists">The member's attribute lists.</param>
    /// <returns><see langword="true"/> when an explicit inlining decision exists.</returns>
    private static bool HasMethodImplAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var name = attribute.Name;
                while (name is QualifiedNameSyntax qualified)
                {
                    name = qualified.Right;
                }

                if (name is SimpleNameSyntax simple
                    && simple.Identifier.ValueText is MethodImplAttributeShortName or MethodImplAttributeShortName + "Attribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Reports PSH1410 for an eligible forwarder.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (BaseMethodDeclarationSyntax)context.Node;
        if (!IsEligibleForwarder(declaration))
        {
            return;
        }

        var identifier = declaration is MethodDeclarationSyntax method
            ? method.Identifier
            : ((OperatorDeclarationSyntax)declaration).OperatorToken;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.InlineTrivialForwarders,
            identifier.GetLocation(),
            identifier.ValueText));
    }
}
