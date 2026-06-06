// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Inserts (or removes) documentation stub elements for the coverage rules:
/// <c>&lt;param&gt;</c> (SST1611), <c>&lt;returns&gt;</c> (SST1615), a stray
/// <c>&lt;returns&gt;</c> removal (SST1617), and <c>&lt;typeparam&gt;</c>
/// (SST1618). The summary rules are intentionally not fixed — an empty summary
/// stub would just re-trigger SST1606.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DocumentationStubCodeFixProvider))]
[Shared]
public sealed class DocumentationStubCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        DocumentationRules.ParametersMustBeDocumented.Id,
        DocumentationRules.ReturnValueMustBeDocumented.Id,
        DocumentationRules.VoidMustNotHaveReturn.Id,
        DocumentationRules.TypeParametersMustBeDocumented.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (OwningMember(node) is not { } member)
            {
                continue;
            }

            Register(context, diagnostic, node, member);
        }
    }

    /// <summary>Registers the appropriate stub fix for one diagnostic.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="diagnostic">The diagnostic being fixed.</param>
    /// <param name="node">The node at the diagnostic location.</param>
    /// <param name="member">The owning member declaration.</param>
    private static void Register(CodeFixContext context, Diagnostic diagnostic, SyntaxNode node, SyntaxNode member)
    {
        if (diagnostic.Id == DocumentationRules.VoidMustNotHaveReturn.Id)
        {
            context.RegisterCodeFix(
                CodeAction.Create("Remove <returns>", token => RemoveReturnsAsync(context.Document, member, token), equivalenceKey: "RemoveReturns"),
                diagnostic);
            return;
        }

        var element = ElementFor(diagnostic.Id, node);
        if (element is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create("Add documentation element", token => InsertElementAsync(context.Document, member, element, token), equivalenceKey: "Add:" + element),
            diagnostic);
    }

    /// <summary>Returns the documentation element text to insert for a diagnostic, or <see langword="null"/>.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="node">The node at the diagnostic location.</param>
    /// <returns>The element text, or <see langword="null"/>.</returns>
    private static string? ElementFor(string id, SyntaxNode node)
    {
        if (id == DocumentationRules.ReturnValueMustBeDocumented.Id)
        {
            return "<returns></returns>";
        }

        if (id == DocumentationRules.ParametersMustBeDocumented.Id)
        {
            return node.FirstAncestorOrSelf<ParameterSyntax>() is { } parameter
                ? "<param name=\"" + parameter.Identifier.ValueText + "\"></param>"
                : null;
        }

        if (id != DocumentationRules.TypeParametersMustBeDocumented.Id)
        {
            return null;
        }

        return node.FirstAncestorOrSelf<TypeParameterSyntax>() is { } typeParameter
            ? "<typeparam name=\"" + typeParameter.Identifier.ValueText + "\"></typeparam>"
            : null;
    }

    /// <summary>Inserts a new documentation line for <paramref name="element"/> just above the member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="member">The member declaration.</param>
    /// <param name="element">The documentation element text.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> InsertElementAsync(Document document, SyntaxNode member, string element, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var insertion = "/// " + element + NewLine(member) + Indent(member);
        return document.WithText(text.WithChanges(new TextChange(new(member.GetFirstToken().SpanStart, 0), insertion)));
    }

    /// <summary>Returns the end-of-line sequence used around a member declaration.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The newline string (defaults to "\n").</returns>
    private static string NewLine(SyntaxNode member)
    {
        foreach (var trivia in member.GetLeadingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return trivia.ToString();
            }
        }

        return "\n";
    }

    /// <summary>Removes the <c>&lt;returns&gt;</c> documentation line from the member.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="member">The member declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RemoveReturnsAsync(Document document, SyntaxNode member, CancellationToken cancellationToken)
    {
        var documentation = XmlDocumentationHelper.GetDocumentationComment(member);
        if (documentation is null || XmlDocumentationHelper.FindElement(documentation, "returns") is not { } returns)
        {
            return document;
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var startLine = text.Lines.GetLineFromPosition(returns.SpanStart);
        var endLine = text.Lines.GetLineFromPosition(returns.Span.End);
        return document.WithText(text.WithChanges(new TextChange(TextSpan.FromBounds(startLine.Start, endLine.EndIncludingLineBreak), string.Empty)));
    }

    /// <summary>Returns the nearest ancestor (or self) that owns a documentation comment.</summary>
    /// <param name="node">The starting node.</param>
    /// <returns>The owning member declaration, or <see langword="null"/>.</returns>
    private static SyntaxNode? OwningMember(SyntaxNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or MethodDeclarationSyntax
                or ConstructorDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax
                or EventDeclarationSyntax or EnumMemberDeclarationSyntax)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Returns the leading whitespace indentation of a member declaration.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The indentation string.</returns>
    private static string Indent(SyntaxNode member)
    {
        var leading = member.GetLeadingTrivia();
        for (var i = leading.Count - 1; i >= 0; i--)
        {
            if (leading[i].IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return leading[i].ToString();
            }
        }

        return string.Empty;
    }
}
