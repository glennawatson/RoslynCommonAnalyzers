// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Passes the caught exception to a catch's error log (SST2438): the exception becomes the exception argument,
/// and a value that was standing in for it — a projection such as its message — is dropped along with the
/// placeholder it filled.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2438ExceptionDiscardedInCatchCodeFixProvider))]
[Shared]
public sealed class Sst2438ExceptionDiscardedInCatchCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.ExceptionDiscardedInCatch.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Pass the caught exception to the logger",
            nameof(Sst2438ExceptionDiscardedInCatchCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported call and rewrites it to pass the caught exception.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node replacement, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || !TryReadProperties(diagnostic, out var name, out var insertIndex, out var removeIndex, out var tailStart))
        {
            return null;
        }

        if (Apply(invocation, name, insertIndex, removeIndex, tailStart) is not { } rewritten)
        {
            return null;
        }

        return new NodeReplacement(
            invocation,
            rewritten,
            current => current is InvocationExpressionSyntax updated ? Apply(updated, name, insertIndex, removeIndex, tailStart) ?? current : current);
    }

    /// <summary>Reads the caught exception's name and the positions the fix needs.</summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <param name="name">The caught exception's name.</param>
    /// <param name="insertIndex">The position the exception argument is inserted at.</param>
    /// <param name="removeIndex">The stand-in value's position, or -1.</param>
    /// <param name="tailStart">The first value argument's position.</param>
    /// <returns><see langword="true"/> when every property is present.</returns>
    private static bool TryReadProperties(Diagnostic diagnostic, out string name, out int insertIndex, out int removeIndex, out int tailStart)
    {
        insertIndex = -1;
        removeIndex = -1;
        tailStart = -1;
        name = string.Empty;
        if (!diagnostic.Properties.TryGetValue(LoggerCallAnalyzer.ExceptionNameKey, out var stored) || string.IsNullOrEmpty(stored))
        {
            return false;
        }

        name = stored!;
        return LoggerFixProperties.TryGetIndex(diagnostic, LoggerCallAnalyzer.InsertIndexKey, out insertIndex)
            && LoggerFixProperties.TryGetIndex(diagnostic, LoggerCallAnalyzer.TailStartKey, out tailStart)
            && LoggerFixProperties.TryGetIndex(diagnostic, LoggerCallAnalyzer.DegradedArgumentKey, out removeIndex);
    }

    /// <summary>Inserts the caught exception, dropping a stand-in value when there is one.</summary>
    /// <param name="invocation">The logging call.</param>
    /// <param name="name">The caught exception's name.</param>
    /// <param name="insertIndex">The position the exception argument is inserted at.</param>
    /// <param name="removeIndex">The stand-in value's position, or -1.</param>
    /// <param name="tailStart">The first value argument's position.</param>
    /// <returns>The rewritten call, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? Apply(InvocationExpressionSyntax invocation, string name, int insertIndex, int removeIndex, int tailStart)
        => LoggerExceptionHoist.Rewrite(invocation, SyntaxFactory.IdentifierName(name), insertIndex, removeIndex, tailStart);
}
