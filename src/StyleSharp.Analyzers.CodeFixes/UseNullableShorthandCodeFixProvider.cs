// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites a long-form <c>Nullable&lt;T&gt;</c> type as the <c>T?</c> shorthand (SST1125).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseNullableShorthandCodeFixProvider))]
[Shared]
public sealed class UseNullableShorthandCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseNullableShorthand.Id);

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
            var outer = root.FindNode(diagnostic.Location.SourceSpan);
            if (FindGeneric(outer) is not { } generic)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use 'T?' shorthand",
                    _ => Task.FromResult(Replace(context.Document, root, outer, generic)),
                    equivalenceKey: nameof(UseNullableShorthandCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the nullable type node with its <c>T?</c> shorthand.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="outer">The full type node to replace.</param>
    /// <param name="generic">The <c>Nullable&lt;T&gt;</c> generic name.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, SyntaxNode outer, GenericNameSyntax generic)
    {
        var elementType = generic.TypeArgumentList.Arguments[0].WithoutTrivia();
        var shorthand = SyntaxFactory.NullableType(elementType).WithTriviaFrom(outer);
        return document.WithSyntaxRoot(root.ReplaceNode(outer, shorthand));
    }

    /// <summary>Finds the <c>Nullable&lt;T&gt;</c> generic name inside the reported type node.</summary>
    /// <param name="outer">The reported type node.</param>
    /// <returns>The generic name, or <see langword="null"/> when none is present.</returns>
    private static GenericNameSyntax? FindGeneric(SyntaxNode outer) => outer switch
    {
        GenericNameSyntax generic => generic,
        QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic,
        _ => null
    };
}
