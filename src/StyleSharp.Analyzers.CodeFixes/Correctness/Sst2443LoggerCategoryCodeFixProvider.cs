// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a typed logger's category to the type that logs through it (SST2443), so its configured level
/// filters and sink routes apply. The enclosing type's name replaces the mismatched one, carrying the type
/// parameters when the enclosing type is generic.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2443LoggerCategoryCodeFixProvider))]
[Shared]
public sealed class Sst2443LoggerCategoryCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.WrongLoggerCategory.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the enclosing type as the logger category",
            nameof(Sst2443LoggerCategoryCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported category syntax and replaces it with the enclosing type's name.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node replacement, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not TypeSyntax category
            || category.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } enclosing)
        {
            return null;
        }

        var replacement = BuildSelfType(enclosing).WithTriviaFrom(category);
        return new NodeReplacement(category, replacement, current => current is TypeSyntax type ? BuildSelfType(enclosing).WithTriviaFrom(type) : current);
    }

    /// <summary>Builds a type reference naming a declaration's own type, carrying its type parameters.</summary>
    /// <param name="declaration">The enclosing type declaration.</param>
    /// <returns>The self type reference.</returns>
    private static TypeSyntax BuildSelfType(TypeDeclarationSyntax declaration)
    {
        var identifier = SyntaxFactory.Identifier(declaration.Identifier.ValueText);
        if (declaration.TypeParameterList is not { Parameters.Count: > 0 } typeParameters)
        {
            return SyntaxFactory.IdentifierName(identifier);
        }

        var arguments = new List<TypeSyntax>(typeParameters.Parameters.Count);
        for (var i = 0; i < typeParameters.Parameters.Count; i++)
        {
            arguments.Add(SyntaxFactory.IdentifierName(typeParameters.Parameters[i].Identifier.ValueText));
        }

        return SyntaxFactory.GenericName(identifier, SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(arguments)));
    }
}
