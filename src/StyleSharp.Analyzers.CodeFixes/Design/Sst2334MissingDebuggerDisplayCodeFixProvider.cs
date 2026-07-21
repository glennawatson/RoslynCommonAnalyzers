// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds a <c>[System.Diagnostics.DebuggerDisplay]</c> skeleton above a publicly visible type that has none
/// (SST2334). The display string names the type's first public property when it has one, and otherwise falls
/// back to its <c>ToString()</c>, giving the developer a working starting point to refine.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2334MissingDebuggerDisplayCodeFixProvider))]
[Shared]
public sealed class Sst2334MissingDebuggerDisplayCodeFixProvider : CodeFixProvider
{
    /// <summary>The fully-qualified attribute name, emitted so the fix needs no <c>using</c>.</summary>
    private const string DebuggerDisplayAttributeName = "System.Diagnostics.DebuggerDisplay";

    /// <summary>The display string used when the type has no public property to name.</summary>
    private const string ToStringDisplay = "{ToString(),nq}";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.MissingDebuggerDisplay.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add [DebuggerDisplay]",
                    cancellationToken => AddDebuggerDisplayAsync(context.Document, declaration, cancellationToken),
                    equivalenceKey: nameof(Sst2334MissingDebuggerDisplayCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Prepends a <c>[DebuggerDisplay(...)]</c> attribute list to the type.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="declaration">The type declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> AddDebuggerDisplayAsync(Document document, TypeDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var leading = declaration.GetLeadingTrivia();
        var indent = IndentTrivia(leading);
        var newLine = LineEndingHelper.GetLineBreak(declaration);

        var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName(DebuggerDisplayAttributeName))
                        .WithArgumentList(SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(DisplayString(declaration)))))))))
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(newLine);

        var relocated = declaration.WithLeadingTrivia(indent);
        var updated = relocated.WithAttributeLists(relocated.AttributeLists.Insert(0, attributeList));
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
    }

    /// <summary>Builds the display string, naming the type's first public instance property when it has one.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <returns>The debugger-display format string.</returns>
    private static string DisplayString(TypeDeclarationSyntax declaration)
    {
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is PropertyDeclarationSyntax property
                && ModifierListHelper.Contains(property.Modifiers, SyntaxKind.PublicKeyword)
                && !ModifierListHelper.Contains(property.Modifiers, SyntaxKind.StaticKeyword))
            {
                return "{" + property.Identifier.ValueText + "}";
            }
        }

        return ToStringDisplay;
    }

    /// <summary>Returns the indentation trivia (the whitespace immediately before the type) of its leading trivia.</summary>
    /// <param name="leading">The type's leading trivia.</param>
    /// <returns>The indentation trivia list, or an empty list when the type starts at column zero.</returns>
    private static SyntaxTriviaList IndentTrivia(SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxTriviaList.Empty;
}
