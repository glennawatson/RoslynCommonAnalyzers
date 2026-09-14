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
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Add [STAThread]",
            nameof(Sst2706StaThreadEntryPointCodeFixProvider),
            static (root, diagnostic) => root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>(),
            AddStaThreadAsync);

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
        var indent = CodeFixTriviaHelper.IndentTrivia(leading);
        var newLine = LineEndingHelper.GetLineBreak(method);

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.Token(leading, SyntaxKind.OpenBracketToken, default),
            target: null,
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName(StaThreadAttributeName))),
            SyntaxFactory.Token(default, SyntaxKind.CloseBracketToken, SyntaxFactory.TriviaList(newLine)));

        var relocated = method.WithLeadingTrivia(indent);
        var updated = relocated.WithAttributeLists(relocated.AttributeLists.Insert(0, attributeList));

        return document.WithSyntaxRoot(root.ReplaceNode(method, updated));
    }
}
