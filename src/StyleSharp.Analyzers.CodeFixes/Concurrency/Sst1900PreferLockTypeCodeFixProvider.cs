// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Changes a dedicated <c>object</c> lock field to <c>System.Threading.Lock</c>
/// (SST1900), normalising its initializer to a target-typed <c>new()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1900PreferLockTypeCodeFixProvider))]
[Shared]
public sealed class Sst1900PreferLockTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>The fully-qualified <c>System.Threading.Lock</c> type syntax reused across fixes.</summary>
    private static readonly TypeSyntax LockTypeSyntax = SyntaxFactory.ParseTypeName("System.Threading.Lock");

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.PreferLockType.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<FieldDeclarationSyntax>() is not { } field)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use System.Threading.Lock",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, field)),
                    equivalenceKey: nameof(Sst1900PreferLockTypeCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the reported lock field with its <c>System.Threading.Lock</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="field">The field declaration to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, FieldDeclarationSyntax field)
        => document.WithSyntaxRoot(root.ReplaceNode(field, Rewrite(field)));

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
