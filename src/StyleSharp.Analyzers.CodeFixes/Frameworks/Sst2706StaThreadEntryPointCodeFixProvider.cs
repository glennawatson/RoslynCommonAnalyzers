// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading;

namespace StyleSharp.Analyzers;

/// <summary>
/// Adds <c>[System.STAThread]</c> to the Windows Forms entry point reported by SST2706, on its own line above
/// the method and indented to match, so the COM-backed UI features that need a single-threaded apartment work
/// at runtime. The fully-qualified attribute name is emitted so no <c>using</c> is required.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2706StaThreadEntryPointCodeFixProvider))]
[Shared]
public sealed class Sst2706StaThreadEntryPointCodeFixProvider : CodeFixProvider
{
    /// <summary>The fully-qualified attribute name, emitted so the fix needs no <c>using System</c>.</summary>
    private const string StaThreadAttributeName = "System.STAThread";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(FrameworksRules.StaThreadEntryPoint.Id);

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
            if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add [STAThread]",
                    cancellationToken => AddStaThreadAsync(context.Document, method, cancellationToken),
                    equivalenceKey: nameof(Sst2706StaThreadEntryPointCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Prepends a <c>[System.STAThread]</c> attribute list to the entry-point method.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="method">The entry-point method declaration.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> AddStaThreadAsync(Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var leading = method.GetLeadingTrivia();
        var indent = IndentTrivia(leading);
        var newLine = LineEndingHelper.GetLineBreak(method);

        var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.ParseName(StaThreadAttributeName))))
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(newLine);

        var relocated = method.WithLeadingTrivia(indent);
        var updated = relocated.WithAttributeLists(relocated.AttributeLists.Insert(0, attributeList));

        return document.WithSyntaxRoot(root.ReplaceNode(method, updated));
    }

    /// <summary>Returns the indentation trivia (the whitespace immediately before the method) of its leading trivia.</summary>
    /// <param name="leading">The method's leading trivia.</param>
    /// <returns>The indentation trivia list, or an empty list when the method starts at column zero.</returns>
    private static SyntaxTriviaList IndentTrivia(SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxTriviaList.Empty;
}
