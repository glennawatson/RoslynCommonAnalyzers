// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Moves an exception passed as a message value into the exception argument (SST2439), dropping the value and
/// the placeholder it filled so the exception's full detail reaches the sink.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2439ExceptionAsTemplateArgumentCodeFixProvider))]
[Shared]
public sealed class Sst2439ExceptionAsTemplateArgumentCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.ExceptionAsTemplateArgument.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Pass the exception as the exception argument",
            nameof(Sst2439ExceptionAsTemplateArgumentCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported exception value and hoists it into the exception argument.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node replacement, or <see langword="null"/> when the reported shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ArgumentSyntax>() is not { } argument
            || argument.Parent is not ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } list
            || !LoggerFixProperties.TryGetIndex(diagnostic, LoggerCallAnalyzer.InsertIndexKey, out var insertIndex)
            || !LoggerFixProperties.TryGetIndex(diagnostic, LoggerCallAnalyzer.TailStartKey, out var tailStart))
        {
            return null;
        }

        var removeIndex = list.Arguments.IndexOf(argument);
        if (Apply(invocation, insertIndex, removeIndex, tailStart) is not { } rewritten)
        {
            return null;
        }

        return new NodeReplacement(
            invocation,
            rewritten,
            current => current is InvocationExpressionSyntax updated ? Apply(updated, insertIndex, removeIndex, tailStart) ?? current : current);
    }

    /// <summary>Hoists the value at a position into the exception argument.</summary>
    /// <param name="invocation">The logging call.</param>
    /// <param name="insertIndex">The position the exception argument is inserted at.</param>
    /// <param name="removeIndex">The exception value's position.</param>
    /// <param name="tailStart">The first value argument's position.</param>
    /// <returns>The rewritten call, or <see langword="null"/> when the shape no longer matches.</returns>
    private static InvocationExpressionSyntax? Apply(InvocationExpressionSyntax invocation, int insertIndex, int removeIndex, int tailStart)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (removeIndex < 0 || removeIndex >= arguments.Count)
        {
            return null;
        }

        return LoggerExceptionHoist.Rewrite(invocation, arguments[removeIndex].Expression, insertIndex, removeIndex, tailStart);
    }
}
