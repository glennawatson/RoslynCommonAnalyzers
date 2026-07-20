// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a Semantic Kernel prompt template disabling the default encoding of its substituted input
/// (SES1604). The rule reports an <c>AllowDangerouslySetContent = true</c> assignment -- written directly
/// (<c>config.AllowDangerouslySetContent = true</c>) or as an object-initializer member
/// (<c>new PromptTemplateConfig { AllowDangerouslySetContent = true }</c>) -- when the assigned member's containing
/// type is one of the Semantic Kernel types that carry the flag: <c>Microsoft.SemanticKernel.PromptTemplateConfig</c>,
/// <c>Microsoft.SemanticKernel.InputVariable</c>, <c>Microsoft.SemanticKernel.KernelPromptTemplateFactory</c>, or
/// <c>Microsoft.SemanticKernel.PromptTemplates.Handlebars.HandlebarsPromptTemplateFactory</c>. A prompt template
/// encodes substituted variables by default so injected content cannot break out of its slot; setting the flag true
/// inserts the raw value and re-opens prompt injection. The member is bound by symbol and containing type, so a
/// same-named property on an unrelated type is ignored. The Semantic Kernel abstraction is probed once per
/// compilation; a project without it registers nothing and never receives a diagnostic it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1604PromptTemplateContentEncodingDisabledAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The property that, when true, disables the encoding of substituted prompt-template content.</summary>
    private const string AllowDangerouslySetContentPropertyName = "AllowDangerouslySetContent";

    /// <summary>
    /// The metadata names of the Semantic Kernel types carrying the flag. The first is the marker the whole rule
    /// gates on: it lives in the always-referenced abstractions assembly, so its absence means the project does not
    /// use Semantic Kernel at all.
    /// </summary>
    private static readonly string[] ContentTypeMetadataNames =
    [
        "Microsoft.SemanticKernel.PromptTemplateConfig",
        "Microsoft.SemanticKernel.InputVariable",
        "Microsoft.SemanticKernel.KernelPromptTemplateFactory",
        "Microsoft.SemanticKernel.PromptTemplates.Handlebars.HandlebarsPromptTemplateFactory"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.PromptTemplateContentEncodingDisabled);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var contentTypes = GetContentTypes(start.Compilation);
            if (contentTypes is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, contentTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1604 for <c>AllowDangerouslySetContent = true</c> on a gated Semantic Kernel type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="contentTypes">The gated Semantic Kernel types resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] contentTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.AllowDangerouslySetContent = true' or the object-initializer member form
        // 'AllowDangerouslySetContent = true'. No semantic model is touched until this cheap shape check passes,
        // so the clean path stays allocation-free.
        if (!assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
            || !IsContentFlagTarget(assignment.Left))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: AllowDangerouslySetContentPropertyName } property
            || GetGatedContentType(property.ContainingType, contentTypes) is not { } contentType)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.PromptTemplateContentEncodingDisabled,
            assignment.SyntaxTree,
            assignment.Span,
            contentType.Name));
    }

    /// <summary>Returns whether an assignment target syntactically names the content flag.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns><see langword="true"/> for <c>x.AllowDangerouslySetContent</c> or the bare initializer form.</returns>
    private static bool IsContentFlagTarget(ExpressionSyntax left)
        => left switch
        {
            // 'config.AllowDangerouslySetContent = true'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: AllowDangerouslySetContentPropertyName } => true,

            // 'new PromptTemplateConfig { AllowDangerouslySetContent = true }' (object-initializer member).
            IdentifierNameSyntax { Identifier.ValueText: AllowDangerouslySetContentPropertyName } => true,

            _ => false,
        };

    /// <summary>Returns the gated Semantic Kernel type when a bound property's container is one of them.</summary>
    /// <param name="containingType">The bound property's containing type.</param>
    /// <param name="contentTypes">The gated Semantic Kernel types resolved for the compilation.</param>
    /// <returns>The gated type, or <see langword="null"/> when the container is not gated.</returns>
    private static INamedTypeSymbol? GetGatedContentType(INamedTypeSymbol containingType, INamedTypeSymbol?[] contentTypes)
    {
        for (var i = 0; i < contentTypes.Length; i++)
        {
            if (contentTypes[i] is { } contentType && SymbolEqualityComparer.Default.Equals(contentType, containingType))
            {
                return contentType;
            }
        }

        return null;
    }

    /// <summary>Resolves the Semantic Kernel types the rule gates on, or nothing when Semantic Kernel is absent.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved type, or <see langword="null"/> when the marker type is absent.</returns>
    private static INamedTypeSymbol?[]? GetContentTypes(Compilation compilation)
    {
        // The marker (index 0) lives in the always-referenced abstractions assembly; without it the project does
        // not use Semantic Kernel, so nothing is registered and a project that cannot set the flag pays nothing.
        var marker = compilation.GetTypeByMetadataName(ContentTypeMetadataNames[0]);
        if (marker is null)
        {
            return null;
        }

        var types = new INamedTypeSymbol?[ContentTypeMetadataNames.Length];
        types[0] = marker;
        for (var i = 1; i < ContentTypeMetadataNames.Length; i++)
        {
            types[i] = compilation.GetTypeByMetadataName(ContentTypeMetadataNames[i]);
        }

        return types;
    }
}
