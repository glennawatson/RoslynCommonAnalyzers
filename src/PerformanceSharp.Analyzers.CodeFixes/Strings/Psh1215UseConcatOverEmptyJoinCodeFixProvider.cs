// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites an empty-separator <c>string.Join</c> call to <c>string.Concat</c> (PSH1215):
/// the invoked member is renamed to <c>Concat</c> and the separator argument is removed,
/// so <c>string.Join("", parts)</c> becomes <c>string.Concat(parts)</c> and
/// <c>string.Join(string.Empty, a, b, c)</c> becomes <c>string.Concat(a, b, c)</c>.
/// Trivia around the member name and the argument list is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1215UseConcatOverEmptyJoinCodeFixProvider))]
[Shared]
public sealed class Psh1215UseConcatOverEmptyJoinCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseConcatOverEmptyJoin.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use string.Concat", nameof(Psh1215UseConcatOverEmptyJoinCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported Join invocation with its Concat form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported Join invocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
        => Psh1215UseConcatOverEmptyJoinAnalyzer.IsCandidate(invocation, out _, out _)
            ? document.WithSyntaxRoot(root.ReplaceNode(invocation, Rewrite(invocation)))
            : document;

    /// <summary>Resolves the reported Join invocation and builds its Concat replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is InvocationExpressionSyntax invocation
            && Psh1215UseConcatOverEmptyJoinAnalyzer.IsCandidate(invocation, out _, out _)
            ? new NodeReplacement(invocation, Rewrite(invocation))
            : null;

    /// <summary>Builds the Concat invocation: renamed member, separator argument removed.</summary>
    /// <param name="invocation">The reported Join invocation.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax Rewrite(InvocationExpressionSyntax invocation)
    {
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var arguments = invocation.ArgumentList.Arguments;
        var separator = arguments[0];
        var remaining = arguments.RemoveAt(0);
        var first = remaining[0];
        remaining = remaining.Replace(first, first.WithLeadingTrivia(separator.GetLeadingTrivia()));

        return invocation
            .WithExpression(access.WithName(RenameToConcat(access.Name)))
            .WithArgumentList(invocation.ArgumentList.WithArguments(remaining))
            .WithAdditionalAnnotations(Microsoft.CodeAnalysis.Formatting.Formatter.Annotation);
    }

    /// <summary>Renames the invoked member to <c>Concat</c>, preserving trivia and any explicit type arguments.</summary>
    /// <param name="name">The original <c>Join</c> member name.</param>
    /// <returns>The <c>Concat</c> member name.</returns>
    private static SimpleNameSyntax RenameToConcat(SimpleNameSyntax name)
    {
        var identifier = SyntaxFactory.Identifier(name.Identifier.LeadingTrivia, "Concat", name.Identifier.TrailingTrivia);
        return name is GenericNameSyntax generic
            ? generic.WithIdentifier(identifier)
            : SyntaxFactory.IdentifierName(identifier);
    }
}
