// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a hand-written Unix epoch with the framework's field (PSH1413):
/// <c>new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)</c> becomes <c>DateTime.UnixEpoch</c> and
/// <c>new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)</c> becomes
/// <c>DateTimeOffset.UnixEpoch</c>. The type is written back exactly as the author wrote it, so
/// <c>new System.DateTime(...)</c> becomes <c>System.DateTime.UnixEpoch</c>.
/// </summary>
/// <remarks>
/// For the kindless <c>new DateTime(1970, 1, 1)</c> the fix does more than shorten the expression: the
/// replacement is <see cref="DateTimeKind.Utc"/> where the original was
/// <see cref="DateTimeKind.Unspecified"/>. That is the point of the rule — an Unspecified epoch shifts by
/// the machine's local offset as soon as anything converts it — but it does mean the fix corrects
/// behavior rather than preserving it, which the rule's page states plainly.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1413UseUnixEpochFieldCodeFixProvider))]
[Shared]
public sealed class Psh1413UseUnixEpochFieldCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseUnixEpochField.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the UnixEpoch field",
            nameof(Psh1413UseUnixEpochFieldCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported allocation with the epoch field.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="creation">The reported allocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ObjectCreationExpressionSyntax creation)
        => Psh1413UseUnixEpochFieldAnalyzer.IsEpochCreationShape(creation)
            ? document.WithSyntaxRoot(root.ReplaceNode(creation, Rewrite(creation)))
            : document;

    /// <summary>Resolves the reported allocation and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ObjectCreationExpressionSyntax creation
            && Psh1413UseUnixEpochFieldAnalyzer.IsEpochCreationShape(creation)
            ? new NodeReplacement(creation, Rewrite(creation))
            : null;

    /// <summary>Builds the <c>UnixEpoch</c> access, reusing the type name the author wrote.</summary>
    /// <param name="creation">The reported allocation.</param>
    /// <returns>The replacement expression.</returns>
    private static MemberAccessExpressionSyntax Rewrite(ObjectCreationExpressionSyntax creation)
        => SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            TypeNameExpression.From(((NameSyntax)creation.Type).WithoutTrivia()),
            SyntaxFactory.IdentifierName(Psh1413UseUnixEpochFieldAnalyzer.UnixEpochFieldName))
            .WithTriviaFrom(creation);
}
