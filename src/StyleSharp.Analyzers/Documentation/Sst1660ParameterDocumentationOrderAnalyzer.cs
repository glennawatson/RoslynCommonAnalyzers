// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member whose <c>&lt;param&gt;</c> elements document exactly its parameters but list them in a
/// different order than the parameters are declared (SST1660). The mismatch is only flagged when the set of
/// documented names already equals the parameter set, so a missing or unmatched <c>&lt;param&gt;</c> — a
/// different concern — is left to the rules that own it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1660ParameterDocumentationOrderAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.ParameterDocumentationOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Reports a member whose documented parameters are out of declaration order.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = context.Node;
        var parameters = DocumentedParameterList.Of(member);
        if (parameters.Count < 2)
        {
            // Zero or one parameter can never be out of order.
            return;
        }

        var documentation = XmlDocumentationHelper.GetDocumentationComment(member);
        if (documentation is null || XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        if (FindFirstOutOfOrderParam(documentation, parameters) is not { } outOfOrder)
        {
            return;
        }

        var name = MemberName(member);
        context.ReportDiagnostic(DiagnosticHelper.Create(DocumentationRules.ParameterDocumentationOrder, outOfOrder.GetLocation(), name));
    }

    /// <summary>
    /// Returns the first <c>&lt;param&gt;</c> element that is out of declaration order, or <see langword="null"/>
    /// when the documented names do not form an exact one-to-one match with the parameters (out of scope) or are
    /// already in order.
    /// </summary>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="parameters">The member's parameters.</param>
    /// <returns>The first misordered element, or <see langword="null"/>.</returns>
    private static XmlNodeSyntax? FindFirstOutOfOrderParam(DocumentationCommentTriviaSyntax documentation, in SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        var documentedElements = new List<XmlNodeSyntax>(parameters.Count);
        var documentedNames = new List<string>(parameters.Count);
        foreach (var node in documentation.Content)
        {
            if (XmlDocumentationHelper.GetElementName(node) != "param")
            {
                continue;
            }

            var name = XmlDocumentationHelper.NameAttribute(node);
            if (name is null)
            {
                // A nameless <param> means the documented set cannot match the parameters; leave it to the naming rule.
                return null;
            }

            documentedElements.Add(node);
            documentedNames.Add(name);
        }

        if (documentedNames.Count != parameters.Count || !IsExactParameterSet(documentedNames, parameters))
        {
            return null;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            if (documentedNames[i] != parameters[i].Identifier.ValueText)
            {
                return documentedElements[i];
            }
        }

        return null;
    }

    /// <summary>Returns whether the documented names are a distinct, exact match for the parameter names.</summary>
    /// <param name="documentedNames">The <c>&lt;param&gt;</c> names in document order.</param>
    /// <param name="parameters">The member's parameters.</param>
    /// <returns><see langword="true"/> when every parameter is documented exactly once and no extra name appears.</returns>
    private static bool IsExactParameterSet(List<string> documentedNames, in SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        var parameterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            parameterNames.Add(parameter.Identifier.ValueText);
        }

        if (parameterNames.Count != parameters.Count)
        {
            // A duplicated parameter name is invalid C#; stay out of it.
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in documentedNames)
        {
            if (!parameterNames.Contains(name) || !seen.Add(name))
            {
                return false;
            }
        }

        return seen.Count == parameterNames.Count;
    }

    /// <summary>Returns the reported name of a member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The member's identifier text.</returns>
    private static string MemberName(SyntaxNode member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        IndexerDeclarationSyntax => "this[]",
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        _ => string.Empty,
    };
}
