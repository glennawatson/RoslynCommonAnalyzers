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
public sealed class Psh1300PreferLockTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>The fully-qualified <c>System.Threading.Lock</c> type syntax reused across fixes.</summary>
    private static readonly TypeSyntax LockTypeSyntax = SyntaxFactory.ParseTypeName("System.Threading.Lock");

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(ReportedNode.Ancestor<FieldDeclarationSyntax>, static (current, _) => Rewrite((FieldDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.PreferLockType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(context, "Use System.Threading.Lock", nameof(Psh1300PreferLockTypeCodeFixProvider), ReportedNode.Ancestor<FieldDeclarationSyntax>, Rewrite);

    /// <summary>Rewrites the field's type to System.Threading.Lock and its initializer to <c>new()</c>.</summary>
    /// <param name="field">The field declaration to rewrite.</param>
    /// <returns>The rewritten field declaration.</returns>
    internal static FieldDeclarationSyntax Rewrite(FieldDeclarationSyntax field)
    {
        var declaration = field.Declaration;
        var newType = LockTypeSyntax.WithTriviaFrom(declaration.Type);

        var variable = declaration.Variables[0];
        if (variable.Initializer is { } initializer)
        {
            var newValue = SyntaxFactory.ImplicitObjectCreationExpression(
                SyntaxFactory.Token(initializer.Value.GetLeadingTrivia(), SyntaxKind.NewKeyword, SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker)),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                    default,
                    SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.CloseParenToken, initializer.Value.GetTrailingTrivia())),
                initializer: null);
            variable = variable.WithInitializer(initializer.WithValue(newValue));
        }

        return field.WithDeclaration(declaration.Update(newType, SyntaxFactory.SingletonSeparatedList(variable)));
    }
}
