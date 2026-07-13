// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Documents the partial types and methods that the main documentation analyzer skips:
/// a partial element in the documentation-coverage scope should be documented (SST1601), its
/// documentation should have a summary (SST1605) with text (SST1607), and a partial generic
/// type's parameters should be documented (SST1619). All but SST1601 are opt-in. The coverage
/// scope honours the shared <see cref="DocumentationCoverage"/> options.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PartialDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The partial-capable declaration kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.MethodDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.PartialMustBeDocumented,
        DocumentationRules.PartialMustHaveSummary,
        DocumentationRules.PartialSummaryMustHaveText,
        DocumentationRules.PartialTypeParametersDocumented);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Checks a partial element's documentation coverage and contents.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(member.Modifiers, SyntaxKind.PartialKeyword))
        {
            return;
        }

        var name = MemberOrder.NameToken(member);
        var documentation = XmlDocumentationHelper.GetDocumentationComment(member);
        if (documentation is null)
        {
            var coverage = DocumentationOptions.ReadCoverage(context.Options.AnalyzerConfigOptionsProvider.GetOptions(member.SyntaxTree));
            if (DocumentationVisibility.NeedsDocumentation(member, coverage))
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.PartialMustBeDocumented, name.GetLocation(), name.ValueText));
            }

            return;
        }

        if (XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        CheckSummary(context, documentation, name);
        CheckTypeParameters(context, context.Node, documentation);
    }

    /// <summary>Reports a missing or empty summary on a documented partial element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="name">The element name token.</param>
    private static void CheckSummary(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation, SyntaxToken name)
    {
        var summary = XmlDocumentationHelper.FindElement(documentation, "summary");
        if (summary is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.PartialMustHaveSummary, name.GetLocation(), name.ValueText));
            return;
        }

        if (XmlDocumentationHelper.HasText(summary))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.PartialSummaryMustHaveText, name.GetLocation(), name.ValueText));
    }

    /// <summary>Reports any generic type parameter of a partial type that lacks a typeparam element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The declaration node.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <remarks>
    /// A type parameter is documented once, on whichever part carries it, because the compiler rejects a
    /// second <c>&lt;typeparam&gt;</c> for the same name (CS1710). Asking every part for its own copy
    /// would be asking for code that does not build, so a part is satisfied by a sibling's documentation.
    /// </remarks>
    private static void CheckTypeParameters(SyntaxNodeAnalysisContext context, SyntaxNode node, DocumentationCommentTriviaSyntax documentation)
    {
        if (node is not TypeDeclarationSyntax { TypeParameterList: { } typeParameters })
        {
            return;
        }

        foreach (var parameter in typeParameters.Parameters)
        {
            var name = parameter.Identifier.ValueText;
            if (XmlDocumentationHelper.FindTypeParameterElement(documentation, name) is null
                && !IsDocumentedOnAnotherPart(context, node, name))
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.PartialTypeParametersDocumented, parameter.Identifier.GetLocation(), name));
            }
        }
    }

    /// <summary>Returns whether another part of the partial type already documents a type parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The declaration node.</param>
    /// <param name="name">The type parameter's name.</param>
    /// <returns><see langword="true"/> when a sibling declaration carries the <c>&lt;typeparam&gt;</c>.</returns>
    private static bool IsDocumentedOnAnotherPart(SyntaxNodeAnalysisContext context, SyntaxNode node, string name)
    {
        if (context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not { } symbol)
        {
            return false;
        }

        var declarations = symbol.DeclaringSyntaxReferences;
        for (var i = 0; i < declarations.Length; i++)
        {
            var declaration = declarations[i].GetSyntax(context.CancellationToken);
            if (declaration == node)
            {
                continue;
            }

            if (XmlDocumentationHelper.GetDocumentationComment(declaration) is { } sibling
                && XmlDocumentationHelper.FindTypeParameterElement(sibling, name) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
