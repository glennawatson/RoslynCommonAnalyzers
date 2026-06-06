// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Changes a dedicated <c>object</c> lock field to <c>System.Threading.Lock</c>
/// (SST1900), normalising its initializer to a target-typed <c>new()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferLockTypeCodeFixProvider))]
[Shared]
public sealed class PreferLockTypeCodeFixProvider : CodeFixProvider
{
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
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(field, Rewrite(field)))),
                    equivalenceKey: nameof(PreferLockTypeCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Rewrites the field's type to System.Threading.Lock and its initializer to <c>new()</c>.</summary>
    /// <param name="field">The field declaration to rewrite.</param>
    /// <returns>The rewritten field declaration.</returns>
    private static FieldDeclarationSyntax Rewrite(FieldDeclarationSyntax field)
    {
        var declaration = field.Declaration;
        var newType = SyntaxFactory.ParseTypeName("System.Threading.Lock").WithTriviaFrom(declaration.Type);

        var variable = declaration.Variables[0];
        if (variable.Initializer is { } initializer)
        {
            var newValue = SyntaxFactory.ImplicitObjectCreationExpression().WithTriviaFrom(initializer.Value);
            variable = variable.WithInitializer(initializer.WithValue(newValue));
        }

        return field.WithDeclaration(declaration.WithType(newType).WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));
    }
}
