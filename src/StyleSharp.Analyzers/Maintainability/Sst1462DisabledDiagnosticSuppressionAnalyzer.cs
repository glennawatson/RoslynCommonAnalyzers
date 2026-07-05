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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RemoveDisabledDiagnosticSuppression);

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
            || !IsConfiguredOff(diagnosticId, attribute.SyntaxTree, context.Options, context.Compilation))
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

    /// <summary>Returns whether the active analyzer config disables a diagnostic id.</summary>
    /// <param name="diagnosticId">The diagnostic id.</param>
    /// <param name="tree">The syntax tree.</param>
    /// <param name="options">The analyzer options.</param>
    /// <param name="compilation">The active compilation.</param>
    /// <returns><see langword="true"/> when severity is configured to none or silent.</returns>
    private static bool IsConfiguredOff(string diagnosticId, SyntaxTree tree, AnalyzerOptions options, Compilation compilation)
    {
        if (compilation.Options.SpecificDiagnosticOptions.TryGetValue(diagnosticId, out var reportDiagnostic)
            && reportDiagnostic == ReportDiagnostic.Suppress)
        {
            return true;
        }

        var config = options.AnalyzerConfigOptionsProvider.GetOptions(tree);
        return config.TryGetValue("dotnet_diagnostic." + diagnosticId + ".severity", out var severity)
            && (StringComparer.OrdinalIgnoreCase.Equals(severity, "none")
                || StringComparer.OrdinalIgnoreCase.Equals(severity, "silent"));
    }
}
