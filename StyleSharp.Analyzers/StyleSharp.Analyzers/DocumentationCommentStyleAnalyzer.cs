// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports documentation-comment style issues: a <c>///</c> comment that does not document a
/// type or member (SST1626), and a generated <c>&lt;placeholder&gt;</c> documentation element
/// (SST1651).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocumentationCommentStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The placeholder element name flagged by SST1651.</summary>
    private const string PlaceholderName = "placeholder";

    /// <summary>The node kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
        SyntaxKind.XmlElement,
        SyntaxKind.XmlEmptyElement);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.NoDocumentationStyleComment,
        DocumentationRules.NoPlaceholderElements);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Dispatches each handled node to the relevant documentation-style check.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case DocumentationCommentTriviaSyntax comment:
            {
                CheckPlacement(context, comment);
                break;
            }

            case XmlElementSyntax element:
            {
                CheckPlaceholder(context, element.StartTag.Name, element);
                break;
            }

            case XmlEmptyElementSyntax element:
            {
                CheckPlaceholder(context, element.Name, element);
                break;
            }
        }
    }

    /// <summary>Reports a documentation comment that is not attached to a documentable element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="comment">The documentation comment node.</param>
    private static void CheckPlacement(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax comment)
    {
        if (DocumentsElement(comment.ParentTrivia.Token))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoDocumentationStyleComment, comment.GetLocation()));
    }

    /// <summary>Returns whether the documentation comment's owning token begins a documentable element.</summary>
    /// <param name="token">The token the comment is attached to.</param>
    /// <returns><see langword="true"/> when the comment documents a member or local function.</returns>
    private static bool DocumentsElement(SyntaxToken token)
    {
        var member = token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (member is not null && member.GetFirstToken() == token)
        {
            return true;
        }

        var local = token.Parent?.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
        return local is not null && local.GetFirstToken() == token;
    }

    /// <summary>Reports a generated placeholder documentation element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="name">The element name.</param>
    /// <param name="element">The element node.</param>
    private static void CheckPlaceholder(SyntaxNodeAnalysisContext context, XmlNameSyntax name, SyntaxNode element)
    {
        if (!string.Equals(name.LocalName.ValueText, PlaceholderName, System.StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoPlaceholderElements, element.GetLocation()));
    }
}
