// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>SuppressMessage</c> attributes whose check id is disabled by the active analyzer
/// config scope. This is a cheap config lookup on attributes only; it avoids trying to run other
/// analyzers or infer whether a diagnostic would have been produced.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1462DisabledDiagnosticSuppressionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The category and check id required by a suppression attribute.</summary>
    private const int MinimumArgumentCount = 2;

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
            var suppressMessageAttribute = new SuppressionTypes(start.Compilation);

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAttribute(nodeContext, suppressMessageAttribute),
                SyntaxKind.Attribute);
        });
    }

    /// <summary>Reports a suppression for a disabled diagnostic id.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="suppressMessageAttribute">The suppression attribute symbol, resolved on first demand.</param>
    private static void AnalyzeAttribute(in SyntaxNodeAnalysisContext context, SuppressionTypes suppressMessageAttribute)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!IsPossibleSuppression(attribute, context.SemanticModel, context.CancellationToken)
            || suppressMessageAttribute.Get() is not { } resolved
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: var attributeType }
            || !SymbolEqualityComparer.Default.Equals(attributeType, resolved)
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

    /// <summary>Checks the argument shape and written name before requesting the suppression type.</summary>
    /// <param name="attribute">The attribute to inspect.</param>
    /// <param name="model">The semantic model, used only to preserve differently named aliases.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>Whether the attribute can be a suppression.</returns>
    private static bool IsPossibleSuppression(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        if (attribute.ArgumentList is not { Arguments.Count: >= MinimumArgumentCount } arguments
            || arguments.Arguments[1].NameEquals is not null)
        {
            return false;
        }

        var name = attribute.Name switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            _ => string.Empty,
        };

        // An alias can spell the attribute differently and still bind to the framework type.
        return name is "SuppressMessage" or "SuppressMessageAttribute"
            || model.GetAliasInfo(attribute.Name, cancellationToken) is not null;
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
        if (argumentList is null || argumentList.Arguments.Count < MinimumArgumentCount)
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
        return colon > 0 ? checkId[0..(0 + colon)] : checkId;
    }

    /// <summary>Resolves the suppression attribute only for a possible suppression.</summary>
    /// <param name="compilation">The compilation whose suppression attribute is resolved.</param>
    private sealed class SuppressionTypes(Compilation compilation)
    {
        /// <summary>The metadata name for <c>SuppressMessageAttribute</c>.</summary>
        private const string SuppressMessageAttributeMetadataName = "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute";

        /// <summary>The resolved type slot, including null when the type is absent.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the suppression attribute type, caching its absence too.</summary>
        /// <returns>The suppression attribute type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(SuppressMessageAttributeMetadataName)])[0];
    }
}
