// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Threading;

namespace StyleSharp.Analyzers;

/// <summary>
/// Gives every implicitly-valued member of an enum its explicit value (SST2331), pinning the mapping so a
/// later insertion or reordering cannot silently shift it. Each missing initializer is filled with the value
/// the compiler currently assigns, so the fix changes no runtime value.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2331ImplicitEnumValueCodeFixProvider))]
[Shared]
public sealed class Sst2331ImplicitEnumValueCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.EnumMembersShouldBeExplicit.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<EnumDeclarationSyntax>() is not { } declaration)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Assign explicit values",
                    cancellationToken => AssignExplicitValuesAsync(context.Document, declaration, cancellationToken),
                    equivalenceKey: nameof(Sst2331ImplicitEnumValueCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Fills every member with no initializer with the value the compiler currently gives it.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="declaration">The enum declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> AssignExplicitValuesAsync(Document document, EnumDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (model is null || root is null)
        {
            return document;
        }

        var values = new Dictionary<EnumMemberDeclarationSyntax, string>();
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member.EqualsValue is null
                && model.GetDeclaredSymbol(member, cancellationToken) is { HasConstantValue: true, ConstantValue: { } constant })
            {
                values.Add(member, Convert.ToString(constant, CultureInfo.InvariantCulture) ?? "0");
            }
        }

        if (values.Count == 0)
        {
            return document;
        }

        var updated = declaration.ReplaceNodes(values.Keys, (original, _) => WithExplicitValue(original, values[original]));
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated));
    }

    /// <summary>Adds an explicit <c>= value</c> initializer to an enum member.</summary>
    /// <param name="member">The enum member.</param>
    /// <param name="value">The value text to assign.</param>
    /// <returns>The member with an explicit value.</returns>
    private static EnumMemberDeclarationSyntax WithExplicitValue(EnumMemberDeclarationSyntax member, string value)
    {
        var equalsToken = SyntaxFactory.Token(SyntaxKind.EqualsToken)
            .WithLeadingTrivia(SyntaxFactory.Space)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var equalsValue = SyntaxFactory.EqualsValueClause(equalsToken, SyntaxFactory.ParseExpression(value));
        return member.WithIdentifier(member.Identifier.WithTrailingTrivia()).WithEqualsValue(equalsValue);
    }
}
