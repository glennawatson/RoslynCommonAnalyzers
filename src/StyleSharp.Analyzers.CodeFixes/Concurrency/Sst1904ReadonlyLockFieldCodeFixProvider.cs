// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Formatting;

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes a non-readonly lock field <c>readonly</c> (SST1904) so no later assignment can swap the lock
/// object out from under a caller already holding it. The fix bails when the field is assigned outside a
/// constructor: there the code is genuinely changing the lock object, and the author has to decide what
/// that means.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1904ReadonlyLockFieldCodeFixProvider))]
[Shared]
public sealed class Sst1904ReadonlyLockFieldCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.DoNotLockOnNonReadonlyField.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Make the lock field readonly", nameof(Sst1904ReadonlyLockFieldCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported lock field and builds its readonly replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the field must not be made readonly here.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not { } target
            || model.GetSymbolInfo(target).Symbol is not IFieldSymbol field
            || field.DeclaringSyntaxReferences is not [var reference]
            || reference.GetSyntax() is not VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax declaration }
            || declaration.Declaration.Variables.Count != 1
            || declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            || AssignedOutsideConstructor(declaration.Parent, field, model))
        {
            return null;
        }

        var replacement = AddReadonly(declaration);
        return new NodeReplacement(declaration, replacement, current => AddReadonly((FieldDeclarationSyntax)current));
    }

    /// <summary>Returns whether the field is assigned anywhere outside a constructor.</summary>
    /// <param name="typeDeclaration">The declaring type node.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <returns><see langword="true"/> when a write outside a constructor makes the fix unsafe.</returns>
    private static bool AssignedOutsideConstructor(SyntaxNode? typeDeclaration, IFieldSymbol field, SemanticModel model)
    {
        if (typeDeclaration is null)
        {
            return false;
        }

        foreach (var node in typeDeclaration.DescendantNodes())
        {
            if (node is AssignmentExpressionSyntax assignment
                && node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is null
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, field))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Inserts <c>readonly</c> into a field declaration's modifiers.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <returns>The updated declaration.</returns>
    private static FieldDeclarationSyntax AddReadonly(FieldDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
        {
            var lone = SyntaxFactory.Token(declaration.GetLeadingTrivia(), SyntaxKind.ReadOnlyKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
            return declaration
                .WithDeclaration(declaration.Declaration.WithLeadingTrivia(SyntaxFactory.TriviaList()))
                .WithModifiers(SyntaxFactory.TokenList(lone));
        }

        var appended = SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        return declaration.WithModifiers(modifiers.Add(appended)).WithAdditionalAnnotations(Formatter.Annotation);
    }
}
