// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports missing or invalid documentation on C# 14 extension blocks that are in the
/// documentation-coverage scope (decided by the block's containing types and the shared
/// <see cref="DocumentationCoverage"/> options): a block without a <c>&lt;summary&gt;</c> (SST1654),
/// an undocumented receiver parameter (SST1655) or type parameter (SST1656), and a
/// <c>&lt;param&gt;</c>/<c>&lt;typeparam&gt;</c> element that references a name the block does not
/// declare (SST1657). A
/// class with no extension block bails after a single membership scan, and on the Roslyn 4.8 floor
/// the syntax cannot occur, so nothing is reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionBlockDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.ExtensionBlockMustBeDocumented,
        DocumentationRules.ExtensionBlockParametersMustBeDocumented,
        DocumentationRules.ExtensionBlockTypeParametersMustBeDocumented,
        DocumentationRules.ExtensionBlockDocumentationReferenceMustMatch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    /// <summary>Checks the extension blocks declared directly in a class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var members = ((ClassDeclarationSyntax)context.Node).Members;

        // The container's visibility is shared by every block, so it is resolved once on the first block.
        var exposureResolved = false;
        var exposed = false;
        for (var index = 0; index < members.Count; index++)
        {
            if (members[index] is not TypeDeclarationSyntax block || !ExtensionBlockHelper.IsExtensionBlock(block))
            {
                continue;
            }

            if (!exposureResolved)
            {
                exposureResolved = true;
                var coverage = DocumentationOptions.ReadCoverage(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree));
                exposed = DocumentationVisibility.NeedsContainerDocumentation(block, coverage);
            }

            if (!exposed)
            {
                return;
            }

            CheckBlock(context, block);
        }
    }

    /// <summary>Checks one extension block's documentation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    private static void CheckBlock(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block)
    {
        var documentation = XmlDocumentationHelper.GetDocumentationComment(block);
        if (documentation is null)
        {
            ReportMissingSummary(context, block);
            return;
        }

        if (XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        if (!XmlDocumentationHelper.HasElement(documentation, "summary"))
        {
            ReportMissingSummary(context, block);
        }

        CheckParameters(context, block, documentation);
        CheckTypeParameters(context, block, documentation);
        CheckReferences(context, block, documentation);
    }

    /// <summary>Reports the missing-summary diagnostic on the <c>extension</c> keyword.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    private static void ReportMissingSummary(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block)
        => context.ReportDiagnostic(Diagnostic.Create(
            DocumentationRules.ExtensionBlockMustBeDocumented,
            block.GetFirstToken().GetLocation()));

    /// <summary>Reports any receiver parameter without a matching <c>&lt;param&gt;</c> element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="documentation">The block's documentation comment.</param>
    private static void CheckParameters(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block, DocumentationCommentTriviaSyntax documentation)
    {
        if (block.ParameterList is not { } parameterList)
        {
            return;
        }

        foreach (var parameter in parameterList.Parameters)
        {
            var name = parameter.Identifier.ValueText;
            if (name.Length == 0)
            {
                continue;
            }

            if (XmlDocumentationHelper.FindParameterElement(documentation, name) is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DocumentationRules.ExtensionBlockParametersMustBeDocumented,
                    parameter.Identifier.GetLocation(),
                    name));
            }
        }
    }

    /// <summary>Reports any type parameter without a matching <c>&lt;typeparam&gt;</c> element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="documentation">The block's documentation comment.</param>
    private static void CheckTypeParameters(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block, DocumentationCommentTriviaSyntax documentation)
    {
        if (block.TypeParameterList is not { } typeParameterList)
        {
            return;
        }

        foreach (var typeParameter in typeParameterList.Parameters)
        {
            var name = typeParameter.Identifier.ValueText;
            if (name.Length == 0)
            {
                continue;
            }

            if (XmlDocumentationHelper.FindTypeParameterElement(documentation, name) is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DocumentationRules.ExtensionBlockTypeParametersMustBeDocumented,
                    typeParameter.Identifier.GetLocation(),
                    name));
            }
        }
    }

    /// <summary>Reports any <c>&lt;param&gt;</c>/<c>&lt;typeparam&gt;</c> element naming an entry the block does not declare.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="block">The extension block.</param>
    /// <param name="documentation">The block's documentation comment.</param>
    private static void CheckReferences(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax block, DocumentationCommentTriviaSyntax documentation)
    {
        foreach (var node in documentation.Content)
        {
            var elementName = XmlDocumentationHelper.GetElementName(node);
            var isParameter = elementName == "param";
            if (!isParameter && elementName != "typeparam")
            {
                continue;
            }

            if (NameAttributeSyntax(node) is not { } nameAttribute)
            {
                continue;
            }

            var name = nameAttribute.Identifier.Identifier.ValueText;
            var matches = isParameter ? DeclaresParameter(block, name) : DeclaresTypeParameter(block, name);
            if (!matches)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DocumentationRules.ExtensionBlockDocumentationReferenceMustMatch,
                    nameAttribute.Identifier.GetLocation(),
                    name));
            }
        }
    }

    /// <summary>Returns whether the block declares a receiver parameter named <paramref name="name"/>.</summary>
    /// <param name="block">The extension block.</param>
    /// <param name="name">The parameter name to look for.</param>
    /// <returns><see langword="true"/> when the parameter is declared.</returns>
    private static bool DeclaresParameter(TypeDeclarationSyntax block, string name)
    {
        if (block.ParameterList is not { } parameterList)
        {
            return false;
        }

        foreach (var parameter in parameterList.Parameters)
        {
            if (parameter.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the block declares a type parameter named <paramref name="name"/>.</summary>
    /// <param name="block">The extension block.</param>
    /// <param name="name">The type parameter name to look for.</param>
    /// <returns><see langword="true"/> when the type parameter is declared.</returns>
    private static bool DeclaresTypeParameter(TypeDeclarationSyntax block, string name)
    {
        if (block.TypeParameterList is not { } typeParameterList)
        {
            return false;
        }

        foreach (var typeParameter in typeParameterList.Parameters)
        {
            if (typeParameter.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns a node's <c>name</c> attribute syntax, or <see langword="null"/> when absent.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>The name attribute syntax, or <see langword="null"/>.</returns>
    private static XmlNameAttributeSyntax? NameAttributeSyntax(XmlNodeSyntax node)
    {
        var attributes = node switch
        {
            XmlElementSyntax element => element.StartTag.Attributes,
            XmlEmptyElementSyntax element => element.Attributes,
            _ => default
        };

        foreach (var attribute in attributes)
        {
            if (attribute is XmlNameAttributeSyntax nameAttribute)
            {
                return nameAttribute;
            }
        }

        return null;
    }
}
