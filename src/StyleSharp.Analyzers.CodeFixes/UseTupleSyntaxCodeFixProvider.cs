// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>Rewrites an explicit <c>ValueTuple&lt;...&gt;</c> type to tuple syntax (SST1141).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseTupleSyntaxCodeFixProvider))]
[Shared]
public sealed class UseTupleSyntaxCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.UseTupleSyntax.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<GenericNameSyntax>() is not { } generic)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use tuple syntax",
                    cancellationToken => Task.FromResult(Replace(context.Document, root, generic)),
                    equivalenceKey: nameof(UseTupleSyntaxCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Replaces the explicit <c>ValueTuple&lt;...&gt;</c> spelling with tuple syntax.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <returns>The updated document.</returns>
    internal static Document Replace(Document document, SyntaxNode root, GenericNameSyntax generic)
        => document.WithSyntaxRoot(root.ReplaceNode(ReplaceTarget(generic), BuildTuple(generic)));

    /// <summary>Returns the node to replace — the qualified name when the generic is its right side.</summary>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <returns>The outermost type node representing the value tuple.</returns>
    private static SyntaxNode ReplaceTarget(GenericNameSyntax generic)
        => generic.Parent is QualifiedNameSyntax qualified && qualified.Right == generic ? qualified : generic;

    /// <summary>Builds the <c>(T1, T2, ...)</c> tuple type from the value tuple's type arguments.</summary>
    /// <param name="generic">The <c>ValueTuple&lt;...&gt;</c> generic name.</param>
    /// <returns>The equivalent tuple type, carrying the replaced node's trivia.</returns>
    private static TupleTypeSyntax BuildTuple(GenericNameSyntax generic)
    {
        var arguments = generic.TypeArgumentList.Arguments;
        var builder = new StringBuilder("(");
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(arguments[i].WithoutTrivia().ToString());
        }

        builder.Append(')');
        return (TupleTypeSyntax)SyntaxFactory.ParseTypeName(builder.ToString()).WithTriviaFrom(ReplaceTarget(generic));
    }
}
