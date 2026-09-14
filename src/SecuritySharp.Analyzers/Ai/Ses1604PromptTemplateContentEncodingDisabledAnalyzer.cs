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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.PromptTemplateContentEncodingDisabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ContentTypeMetadataNames[0]) is null)
            {
                return;
            }

            var contentTypes = MetadataTypeLookup.ResolveAll(start.Compilation, ContentTypeMetadataNames);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, contentTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1604 for <c>AllowDangerouslySetContent = true</c> on a gated Semantic Kernel type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="contentTypes">The gated Semantic Kernel types resolved for the compilation.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, INamedTypeSymbol[] contentTypes)
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
            || !TypeRelations.IsOneOf(property.ContainingType, contentTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.PromptTemplateContentEncodingDisabled,
            assignment.SyntaxTree,
            assignment.Span,
            property.ContainingType.Name));
    }

    /// <summary>Returns whether an assignment target syntactically names the content flag.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns><see langword="true"/> for <c>x.AllowDangerouslySetContent</c> or the bare initializer form.</returns>
    private static bool IsContentFlagTarget(ExpressionSyntax left) =>
        left switch
        {
            // 'config.AllowDangerouslySetContent = true'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: AllowDangerouslySetContentPropertyName }
                or IdentifierNameSyntax { Identifier.ValueText: AllowDangerouslySetContentPropertyName } => true,

            _ => false,
        };
}
