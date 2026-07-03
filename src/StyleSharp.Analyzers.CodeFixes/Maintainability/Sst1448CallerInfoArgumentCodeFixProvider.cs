// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an argument passed explicitly to a caller-info parameter (SST1448) so the compiler
/// supplies the call site again. The removal is only offered when it cannot shift the meaning of
/// other arguments: the argument must be named, or be the last argument in the list.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1448CallerInfoArgumentCodeFixProvider))]
[Shared]
public sealed class Sst1448CallerInfoArgumentCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.CallerInfoArgument.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (!TryGetRemovableArgument(root, diagnostic, out var argument))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Let the compiler supply the caller info",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, argument!)),
                    equivalenceKey: nameof(Sst1448CallerInfoArgumentCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetRemovableArgument(editor.OriginalRoot, diagnostic, out var argument))
        {
            return;
        }

        editor.ReplaceNode(argument!.Parent!, RemoveArgument((ArgumentListSyntax)argument.Parent!, argument));
    }

    /// <summary>Removes the reported argument from its list.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="argument">The reported argument.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ArgumentSyntax argument)
        => document.WithSyntaxRoot(root.ReplaceNode((ArgumentListSyntax)argument.Parent!, RemoveArgument((ArgumentListSyntax)argument.Parent!, argument)));

    /// <summary>Resolves the diagnostic to an argument whose removal is order-safe.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="argument">The removable argument when found.</param>
    /// <returns><see langword="true"/> when the argument can be removed safely.</returns>
    private static bool TryGetRemovableArgument(SyntaxNode root, Diagnostic diagnostic, out ArgumentSyntax? argument)
    {
        argument = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<ArgumentSyntax>();
        if (argument?.Parent is ArgumentListSyntax list
            && (argument.NameColon is not null || list.Arguments[list.Arguments.Count - 1] == argument))
        {
            return true;
        }

        argument = null;
        return false;
    }

    /// <summary>Builds the argument list without the removed argument.</summary>
    /// <param name="list">The original argument list.</param>
    /// <param name="argument">The argument to remove.</param>
    /// <returns>The rewritten argument list.</returns>
    private static ArgumentListSyntax RemoveArgument(ArgumentListSyntax list, ArgumentSyntax argument)
        => list.WithArguments(list.Arguments.Remove(argument));
}
