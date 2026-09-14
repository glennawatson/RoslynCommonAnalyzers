// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant initialization to a type's default value (SST1176).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberInitializedToDefaultCodeFixProvider))]
[Shared]
public sealed class MemberInitializedToDefaultCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoMemberInitializedToDefault.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the redundant initializer",
            nameof(MemberInitializedToDefaultCodeFixProvider),
            static (root, diagnostic) => ReportedNode.Ancestor<EqualsValueClauseSyntax>(root, diagnostic) is not null,
            TryRewrite);

    /// <summary>Resolves the reported initializer and rebuilds the declaration that owns it without it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the initializer has no property or variable owner.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        ReportedNode.Ancestor<EqualsValueClauseSyntax>(root, diagnostic) is { } initializer
        && TryTrim(initializer, out var declaration, out var trimmed)
            ? new NodeReplacement(declaration, trimmed)
            : null;

    /// <summary>Builds the declaration that owns the initializer, without it.</summary>
    /// <param name="initializer">The redundant initializer clause.</param>
    /// <param name="declaration">The property or variable declarator that owns the initializer.</param>
    /// <param name="trimmed">The owner rebuilt without the initializer.</param>
    /// <returns><see langword="true"/> when the initializer belongs to a property or a variable declarator.</returns>
    private static bool TryTrim(
        EqualsValueClauseSyntax initializer,
        [NotNullWhen(true)] out SyntaxNode? declaration,
        [NotNullWhen(true)] out SyntaxNode? trimmed)
    {
        // A property carries the initializer plus a trailing ';'; both must go. A field/event keeps
        // its own ';' on the declaration, so only the declarator's initializer is removed.
        if (initializer.Parent is PropertyDeclarationSyntax property)
        {
            declaration = property;
            trimmed = property.Update(
                    property.AttributeLists,
                    property.Modifiers,
                    property.Type,
                    property.ExplicitInterfaceSpecifier,
                    property.Identifier,
                    property.AccessorList,
                    property.ExpressionBody,
                    initializer: null,
                    semicolonToken: default)
                .WithTrailingTrivia(property.GetTrailingTrivia());
            return true;
        }

        if (initializer.Parent is VariableDeclaratorSyntax declarator)
        {
            declaration = declarator;
            trimmed = declarator.Update(
                declarator.ArgumentList is null
                    ? declarator.Identifier.WithTrailingTrivia(declarator.GetTrailingTrivia())
                    : declarator.Identifier,
                declarator.ArgumentList?.WithTrailingTrivia(declarator.GetTrailingTrivia()),
                initializer: null);
            return true;
        }

        declaration = null;
        trimmed = null;
        return false;
    }
}
