// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
public sealed class Psh1413UseUnixEpochFieldCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ApiSelectionRules.UseUnixEpochField.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use the UnixEpoch field",
            nameof(Psh1413UseUnixEpochFieldCodeFixProvider),
            CanRewrite,
            TryRewrite);

    /// <summary>Resolves the reported allocation and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is ObjectCreationExpressionSyntax creation
            && Psh1413UseUnixEpochFieldAnalyzer.IsEpochCreationShape(creation)
            ? new NodeReplacement(creation, Rewrite(creation))
            : null;

    /// <summary>Checks applicability without constructing replacement syntax.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported shape can be rewritten.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)is ObjectCreationExpressionSyntax creation
            && Psh1413UseUnixEpochFieldAnalyzer.IsEpochCreationShape(creation);

    /// <summary>Builds the <c>UnixEpoch</c> access, reusing the type name the author wrote.</summary>
    /// <param name="creation">The reported allocation.</param>
    /// <returns>The replacement expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MemberAccessExpressionSyntax Rewrite(ObjectCreationExpressionSyntax creation) =>
        SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            TypeNameExpression.From(((NameSyntax)creation.Type).WithoutTrivia()).WithLeadingTrivia(creation.GetLeadingTrivia()),
            SyntaxFactory.Token(SyntaxKind.DotToken),
            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker),
                Psh1413UseUnixEpochFieldAnalyzer.UnixEpochFieldName,
                creation.GetTrailingTrivia())));
}
