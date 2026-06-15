// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace StyleSharp.Analyzers;

/// <summary>Prefixes a bare instance-member reference with <c>this.</c> (SST1101).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1101PrefixLocalCallsWithThisCodeFixProvider))]
[Shared]
public sealed class Sst1101PrefixLocalCallsWithThisCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.PrefixLocalCallsWithThis.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add 'this.' prefix",
                    _ => Task.FromResult(Apply(context.Document, root, identifier)),
                    equivalenceKey: nameof(Sst1101PrefixLocalCallsWithThisCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan) is not IdentifierNameSyntax identifier)
        {
            return;
        }

        editor.ReplaceNode(identifier, Qualify(identifier));
    }

    /// <summary>Replaces the identifier with a <c>this.</c>-qualified member access.</summary>
    /// <param name="document">The document to fix.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="identifier">The bare identifier.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, IdentifierNameSyntax identifier) =>
        document.WithSyntaxRoot(root.ReplaceNode(identifier, Qualify(identifier)));

    /// <summary>Builds the <c>this.</c>-qualified member access for an identifier.</summary>
    /// <param name="identifier">The bare identifier.</param>
    /// <returns>The qualified member access carrying the identifier's trivia.</returns>
    private static MemberAccessExpressionSyntax Qualify(IdentifierNameSyntax identifier) =>
        SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ThisExpression(),
                identifier.WithoutTrivia())
            .WithTriviaFrom(identifier);
}
