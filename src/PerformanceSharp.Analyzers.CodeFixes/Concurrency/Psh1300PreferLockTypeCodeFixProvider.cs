// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Changes a dedicated <c>object</c> lock field to <c>System.Threading.Lock</c>
/// (PSH1300), normalising its initializer to a target-typed <c>new()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1300PreferLockTypeCodeFixProvider))]
[Shared]
public sealed class Psh1300PreferLockTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The fully-qualified <c>System.Threading.Lock</c> type syntax reused across fixes.</summary>
    private static readonly TypeSyntax LockTypeSyntax = SyntaxFactory.ParseTypeName("System.Threading.Lock");

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.PreferLockType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use System.Threading.Lock", nameof(Psh1300PreferLockTypeCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported lock field with its <c>System.Threading.Lock</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="field">The field declaration to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, FieldDeclarationSyntax field)
        => document.WithSyntaxRoot(root.ReplaceNode(field, Rewrite(field)));

    /// <summary>Resolves the reported lock field and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<FieldDeclarationSyntax>() is { } field
            ? new NodeReplacement(field, Rewrite(field))
            : null;

    /// <summary>Rewrites the field's type to System.Threading.Lock and its initializer to <c>new()</c>.</summary>
    /// <param name="field">The field declaration to rewrite.</param>
    /// <returns>The rewritten field declaration.</returns>
    private static FieldDeclarationSyntax Rewrite(FieldDeclarationSyntax field)
    {
        var declaration = field.Declaration;
        var newType = LockTypeSyntax.WithTriviaFrom(declaration.Type);

        var variable = declaration.Variables[0];
        if (variable.Initializer is { } initializer)
        {
            var newValue = SyntaxFactory.ImplicitObjectCreationExpression().WithTriviaFrom(initializer.Value);
            variable = variable.WithInitializer(initializer.WithValue(newValue));
        }

        return field.WithDeclaration(declaration.WithType(newType).WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));
    }
}
