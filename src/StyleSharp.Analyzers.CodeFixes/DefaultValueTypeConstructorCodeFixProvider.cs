// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Replaces a parameterless value-type construction with <c>default(T)</c> (SST1129).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DefaultValueTypeConstructorCodeFixProvider))]
[Shared]
public sealed class DefaultValueTypeConstructorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.DefaultValueTypeConstructor.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ObjectCreationExpressionSyntax creation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use 'default'",
                    _ => Task.FromResult(Replace(context.Document, root, creation)),
                    equivalenceKey: nameof(DefaultValueTypeConstructorCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the construction with a <c>default(T)</c> expression.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="creation">The object-creation expression.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, ObjectCreationExpressionSyntax creation)
    {
        var replacement = SyntaxFactory.DefaultExpression(creation.Type.WithoutTrivia()).WithTriviaFrom(creation);
        return document.WithSyntaxRoot(root.ReplaceNode(creation, replacement));
    }
}
