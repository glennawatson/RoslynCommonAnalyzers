// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a positional <c>ItemN</c> tuple access with the element's name (SST1142).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TupleElementNameCodeFixProvider))]
[Shared]
public sealed class TupleElementNameCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.ReferToTupleElementByName.Id);

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
            if (!diagnostic.Properties.TryGetValue(TupleElementNameAnalyzer.NameKey, out var name) || string.IsNullOrEmpty(name)
                || root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IdentifierNameSyntax>() is not { } identifier)
            {
                continue;
            }

            var renamed = identifier.WithIdentifier(SyntaxFactory.Identifier(name!).WithTriviaFrom(identifier.Identifier));

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Refer to the element as '{name}'",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(identifier, renamed))),
                    equivalenceKey: nameof(TupleElementNameCodeFixProvider)),
                diagnostic);
        }
    }
}
