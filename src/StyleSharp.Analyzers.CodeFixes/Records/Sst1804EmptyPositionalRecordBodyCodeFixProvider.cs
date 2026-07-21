// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a positional record's empty <c>{ }</c> body with a semicolon (SST1804).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1804EmptyPositionalRecordBodyCodeFixProvider))]
[Shared]
public sealed class Sst1804EmptyPositionalRecordBodyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(RecordRules.EmptyPositionalRecordBody.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Replace the empty body with a semicolon",
            nameof(Sst1804EmptyPositionalRecordBodyCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported record and drops its empty body for a semicolon.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<RecordDeclarationSyntax>() is not { } record
            || record.ParameterList is null
            || record.Members.Count != 0
            || record.OpenBraceToken.IsKind(SyntaxKind.None)
            || record.CloseBraceToken.IsKind(SyntaxKind.None))
        {
            return null;
        }

        return new NodeReplacement(record, ToSemicolonForm(record));
    }

    /// <summary>Rewrites a positional record with an empty body into its semicolon-terminated form.</summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The semicolon-terminated record.</returns>
    private static RecordDeclarationSyntax ToSemicolonForm(RecordDeclarationSyntax record)
    {
        var semicolon = SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(record.CloseBraceToken.TrailingTrivia);
        var previous = record.OpenBraceToken.GetPreviousToken();

        var updated = previous.IsKind(SyntaxKind.None)
            ? record
            : record.ReplaceToken(previous, previous.WithTrailingTrivia(SyntaxFactory.TriviaList()));

        return updated
            .WithOpenBraceToken(default)
            .WithCloseBraceToken(default)
            .WithSemicolonToken(semicolon);
    }
}
