// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds a <c>[System.Diagnostics.DebuggerDisplay]</c> skeleton above a publicly visible type that has none
/// (SST2334), giving the developer a working starting point to refine. The display string names the best
/// member the type has to identify an instance by: its first public property, else any other readable
/// property, else its first field — a display string is evaluated in the type's own context, so naming a
/// private field is legitimate and beats saying nothing. A type with none of those falls back to
/// <c>ToString()</c>, which the rule only reports when it is overridden.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2334MissingDebuggerDisplayCodeFixProvider))]
[Shared]
public sealed class Sst2334MissingDebuggerDisplayCodeFixProvider : CodeFixProvider
{
    /// <summary>The fully-qualified attribute name, emitted so the fix needs no <c>using</c>.</summary>
    private const string DebuggerDisplayAttributeName = "System.Diagnostics.DebuggerDisplay";

    /// <summary>The display string used when the type has no member to name.</summary>
    private const string ToStringDisplay = "{ToString(),nq}";

    /// <summary>The rank of a public instance property — the clearest thing to identify an instance by.</summary>
    private const int PublicPropertyRank = 0;

    /// <summary>The rank of a readable instance property the type does not expose publicly.</summary>
    private const int PropertyRank = 1;

    /// <summary>The rank of an instance field, nameable because the display string binds in the type's own context.</summary>
    private const int FieldRank = 2;

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
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName(DebuggerDisplayAttributeName), SyntaxFactory.AttributeArgumentList(
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

    /// <summary>Builds the display string, naming the best member the type has to identify an instance by.</summary>
    /// <param name="declaration">The type declaration.</param>
    /// <returns>The debugger-display format string.</returns>
    private static string DisplayString(TypeDeclarationSyntax declaration)
    {
        string? best = null;
        var bestRank = int.MaxValue;
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (TryNameMember(members[i], out var name, out var rank) && rank < bestRank)
            {
                best = name;
                bestRank = rank;
            }
        }

        return best is null ? ToStringDisplay : "{" + best + "}";
    }

    /// <summary>Names a member a display string could use, and ranks how well it identifies an instance.</summary>
    /// <param name="member">The member to consider.</param>
    /// <param name="name">The member's name, when it can be named.</param>
    /// <param name="rank">The member's rank, lower being a better thing to show.</param>
    /// <returns><see langword="true"/> when the member is an instance field or a readable instance property.</returns>
    private static bool TryNameMember(MemberDeclarationSyntax member, out string name, out int rank)
    {
        name = string.Empty;
        rank = int.MaxValue;
        if (ModifierListHelper.ContainsEither(member.Modifiers, SyntaxKind.StaticKeyword, SyntaxKind.ConstKeyword))
        {
            return false;
        }

        switch (member)
        {
            case PropertyDeclarationSyntax property when HasGetter(property):
            {
                name = property.Identifier.ValueText;
                rank = ModifierListHelper.Contains(property.Modifiers, SyntaxKind.PublicKeyword) ? PublicPropertyRank : PropertyRank;
                return true;
            }

            case FieldDeclarationSyntax field when field.Declaration.Variables.Count > 0:
            {
                name = field.Declaration.Variables[0].Identifier.ValueText;
                rank = FieldRank;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Returns whether a property can be read, and so shown.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> for an expression-bodied property or one with a get accessor.</returns>
    private static bool HasGetter(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody is not null)
        {
            return true;
        }

        var accessors = property.AccessorList?.Accessors ?? default;
        for (var i = 0; i < accessors.Count; i++)
        {
            if (accessors[i].IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the indentation trivia (the whitespace immediately before the type) of its leading trivia.</summary>
    /// <param name="leading">The type's leading trivia.</param>
    /// <returns>The indentation trivia list, or an empty list when the type starts at column zero.</returns>
    private static SyntaxTriviaList IndentTrivia(SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxTriviaList.Empty;
}
