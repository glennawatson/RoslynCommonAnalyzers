// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>SuppressMessage</c> attributes whose check id is disabled by the active analyzer
/// config scope. This is a cheap config lookup on attributes only; it avoids trying to run other
/// analyzers or infer whether a diagnostic would have been produced.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1462DisabledDiagnosticSuppressionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name for <c>SuppressMessageAttribute</c>.</summary>
    private const string SuppressMessageAttributeMetadataName = "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.RemoveDisabledDiagnosticSuppression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

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

    /// <summary>Reports a suppression for a disabled diagnostic id.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="suppressMessageAttribute">The suppression attribute symbol.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol suppressMessageAttribute)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: var attributeType }
            || !SymbolEqualityComparer.Default.Equals(attributeType, suppressMessageAttribute)
            || TryGetCheckId(attribute.ArgumentList, context.SemanticModel, context.CancellationToken) is not { } diagnosticId
            || !DiagnosticSeverityConfiguration.IsOff(diagnosticId, attribute.SyntaxTree, context.Options, context.Compilation, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.RemoveDisabledDiagnosticSuppression,
            attribute.GetLocation(),
            diagnosticId));
    }

    /// <summary>Reads the diagnostic id from the second positional suppression argument.</summary>
    /// <param name="argumentList">The attribute argument list.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The check id before any colon suffix, or <see langword="null"/>.</returns>
    private static string? TryGetCheckId(
        AttributeArgumentListSyntax? argumentList,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (argumentList is null || argumentList.Arguments.Count < 2)
        {
            return null;
        }

        var argument = argumentList.Arguments[1];
        if (argument.NameEquals is not null)
        {
            return null;
        }

        var constant = model.GetConstantValue(argument.Expression, cancellationToken);
        if (!constant.HasValue || constant.Value is not string checkId)
        {
            return null;
        }

        var colon = checkId.IndexOf(':');
        return colon > 0 ? checkId.Substring(0, colon) : checkId;
    }
}
