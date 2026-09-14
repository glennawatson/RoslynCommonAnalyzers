// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Replaces a positional record's empty <c>{ }</c> body with a semicolon (SST1804).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1804EmptyPositionalRecordBodyCodeFixProvider))]
[Shared]
public sealed class Sst1804EmptyPositionalRecordBodyCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindEmptyBodiedRecord, static (current, _) => ToSemicolonForm((RecordDeclarationSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(RecordRules.EmptyPositionalRecordBody.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Replace the empty body with a semicolon",
            nameof(Sst1804EmptyPositionalRecordBodyCodeFixProvider),
            FindEmptyBodiedRecord,
            ToSemicolonForm);

    /// <summary>Resolves the reported record when it is positional and its braces hold nothing.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The record, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static RecordDeclarationSyntax? FindEmptyBodiedRecord(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<RecordDeclarationSyntax>() is not { } record
            || record.ParameterList is null
            || record.Members.Count != 0
            || record.OpenBraceToken.IsKind(SyntaxKind.None)
            || record.CloseBraceToken.IsKind(SyntaxKind.None)
            ? null
            : record;

    /// <summary>Rewrites a positional record with an empty body into its semicolon-terminated form.</summary>
    /// <param name="record">The record declaration.</param>
    /// <returns>The semicolon-terminated record.</returns>
    private static RecordDeclarationSyntax ToSemicolonForm(RecordDeclarationSyntax record)
    {
        var semicolon = SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), SyntaxKind.SemicolonToken, record.CloseBraceToken.TrailingTrivia);
        var previous = record.OpenBraceToken.GetPreviousToken();

        var updated = previous.IsKind(SyntaxKind.None)
            ? record
            : record.ReplaceToken(previous, previous.WithTrailingTrivia(SyntaxFactory.TriviaList()));

        return updated.Update(
            updated.AttributeLists,
            updated.Modifiers,
            updated.Keyword,
            updated.ClassOrStructKeyword,
            updated.Identifier,
            updated.TypeParameterList,
            updated.ParameterList,
            updated.BaseList,
            updated.ConstraintClauses,
            default,
            updated.Members,
            default,
            semicolon);
    }
}
