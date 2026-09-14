// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes an argument passed explicitly to a caller-info parameter (SST1448) so the compiler
/// supplies the call site again. The removal is only offered when it cannot shift the meaning of
/// other arguments: the argument must be named, or be the last argument in the list.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1448CallerInfoArgumentCodeFixProvider))]
[Shared]
public sealed class Sst1448CallerInfoArgumentCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.CallerInfoArgument.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync<ArgumentSyntax>(
            context,
            "Let the compiler supply the caller info",
            nameof(Sst1448CallerInfoArgumentCodeFixProvider),
            TryGetRemovableArgument,
            Apply);

    /// <summary>Removes the reported argument from its list.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="argument">The reported argument.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Document Apply(Document document, SyntaxNode root, ArgumentSyntax argument) =>
        document.WithSyntaxRoot(root.ReplaceNode((ArgumentListSyntax)argument.Parent!, RemoveArgument((ArgumentListSyntax)argument.Parent!, argument)));

    /// <summary>Resolves the diagnostic to its argument list rebuilt without the removable argument.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when no argument can be removed safely.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetRemovableArgument(root, diagnostic, out var argument)
            ? new NodeReplacement(argument.Parent!, RemoveArgument((ArgumentListSyntax)argument.Parent!, argument))
            : null;

    /// <summary>Resolves the diagnostic to an argument whose removal is order-safe.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="argument">The removable argument when found.</param>
    /// <returns><see langword="true"/> when the argument can be removed safely.</returns>
    private static bool TryGetRemovableArgument(SyntaxNode root, Diagnostic diagnostic, [NotNullWhen(true)] out ArgumentSyntax? argument)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArgumentListSyntax RemoveArgument(ArgumentListSyntax list, ArgumentSyntax argument) =>
        list.WithArguments(list.Arguments.Remove(argument));
}
