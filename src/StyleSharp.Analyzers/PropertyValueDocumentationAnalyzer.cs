// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documented property whose documentation omits a <c>&lt;value&gt;</c> element
/// (SST1609) or whose <c>&lt;value&gt;</c> element has no text (SST1610). Both rules are
/// opt-in, matching StyleCop's defaults.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PropertyValueDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.PropertyMustHaveValue,
        DocumentationRules.PropertyValueMustHaveText);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Checks a documented property's value element coverage and contents.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var documentation = XmlDocumentationHelper.GetDocumentationComment(property);
        if (documentation is null || XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        var value = XmlDocumentationHelper.FindElement(documentation, "value");
        if (value is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.PropertyMustHaveValue, property.Identifier.GetLocation(), property.Identifier.ValueText));
            return;
        }

        if (XmlDocumentationHelper.HasText(value))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.PropertyValueMustHaveText, property.Identifier.GetLocation(), property.Identifier.ValueText));
    }
}
