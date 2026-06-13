// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Removes a redundant parameterless <c>: base()</c> constructor initializer (SST1178).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantBaseConstructorCallCodeFixProvider))]
[Shared]
public sealed class RedundantBaseConstructorCallCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ReadabilityRules.NoRedundantBaseConstructorCall.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ConstructorInitializerSyntax initializer
                || initializer.Parent is not ConstructorDeclarationSyntax constructor)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the redundant ': base()' call",
                    _ => Task.FromResult(Apply(context.Document, root, constructor)),
                    equivalenceKey: nameof(RedundantBaseConstructorCallCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Drops the constructor initializer, leaving the parameter list to flow into the body.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="constructor">The constructor whose initializer is redundant.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ConstructorDeclarationSyntax constructor)
        => document.WithSyntaxRoot(root.ReplaceNode(constructor, constructor.WithInitializer(null)));
}
