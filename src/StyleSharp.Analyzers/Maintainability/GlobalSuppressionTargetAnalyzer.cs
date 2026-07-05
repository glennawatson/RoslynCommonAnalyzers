// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Checks assembly-level <c>SuppressMessage</c> targets without walking the whole tree more than
/// Roslyn already does for attributes. The analyzer binds only global suppression attributes,
/// extracts the constant <c>Target</c> value, and uses Roslyn's documentation-id resolver for the
/// validity check. That keeps the clean path allocation-light and avoids reimplementing symbol-id
/// parsing by hand.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GlobalSuppressionTargetAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name for <c>SuppressMessageAttribute</c>.</summary>
    private const string SuppressMessageAttributeMetadataName = "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.ValidGlobalSuppressionTarget,
        MaintainabilityRules.UseDeclarationIdSuppressionTarget);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var suppressMessageAttribute = start.Compilation.GetTypeByMetadataName(SuppressMessageAttributeMetadataName);
            if (suppressMessageAttribute is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAttribute(nodeContext, suppressMessageAttribute),
                SyntaxKind.Attribute);
        });
    }

    /// <summary>Reports invalid or legacy global suppression targets.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="suppressMessageAttribute">The suppression attribute symbol.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol suppressMessageAttribute)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.Parent is not AttributeListSyntax { Target.Identifier.RawKind: (int)SyntaxKind.AssemblyKeyword }
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: var attributeType }
            || !SymbolEqualityComparer.Default.Equals(attributeType, suppressMessageAttribute)
            || TryGetNamedString(attribute.ArgumentList, "Target", context.SemanticModel, context.CancellationToken) is not { } target
            || target.Length == 0)
        {
            return;
        }

        if (target[0] == '~')
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.UseDeclarationIdSuppressionTarget,
                attribute.GetLocation()));
            return;
        }

        if (!LooksLikeDeclarationId(target))
        {
            return;
        }

        if (DocumentationCommentId.GetFirstSymbolForDeclarationId(target, context.Compilation) is not null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.ValidGlobalSuppressionTarget,
            attribute.GetLocation(),
            target));
    }

    /// <summary>Reads a constant string named argument from an attribute argument list.</summary>
    /// <param name="argumentList">The attribute argument list.</param>
    /// <param name="name">The named argument to read.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The constant string value, or <see langword="null"/>.</returns>
    private static string? TryGetNamedString(
        AttributeArgumentListSyntax? argumentList,
        string name,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (argumentList is null)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (argument.NameEquals?.Name.Identifier.ValueText != name)
            {
                continue;
            }

            var constant = model.GetConstantValue(argument.Expression, cancellationToken);
            return constant.HasValue ? constant.Value as string : null;
        }

        return null;
    }

    /// <summary>Returns whether a target has the shape of a declaration documentation id.</summary>
    /// <param name="target">The target string.</param>
    /// <returns><see langword="true"/> when Roslyn can reasonably resolve the target.</returns>
    private static bool LooksLikeDeclarationId(string target)
        => target.Length > 2
            && target[1] == ':'
            && target[0] is 'E' or 'F' or 'M' or 'N' or 'P' or 'T';
}
