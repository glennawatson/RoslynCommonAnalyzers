// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a redundant <c>GetType()</c> call from a value that is already a <see cref="System.Type"/> (SST2432).
/// </summary>
/// <remarks>
/// The receiver replaces the whole invocation and keeps the invocation's outer trivia, so the surrounding
/// expression is unchanged apart from the dropped call.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2432RedundantGetTypeCodeFixProvider))]
[Shared]
public sealed class Sst2432RedundantGetTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.RedundantGetType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the redundant GetType() call",
            nameof(Sst2432RedundantGetTypeCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported invocation and replaces it with its receiver.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var replacement = memberAccess.Expression.WithTriviaFrom(invocation);
        return new NodeReplacement(invocation, replacement);
    }
}
