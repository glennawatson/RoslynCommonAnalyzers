// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces the null argument of an event raise with the value subscribers expect (SST2436): a null sender
/// becomes <c>this</c>, and a null args becomes <c>EventArgs.Empty</c>.
/// </summary>
/// <remarks>
/// The args fix is only offered when the delegate's event-args parameter is exactly <see cref="EventArgs"/>:
/// a custom args type may have no parameterless construction, and a fix that produced code which did not
/// compile would be worse than none. The reported null keeps its trivia when it is swapped.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2436NullEventRaiseCodeFixProvider))]
[Shared]
public sealed class Sst2436NullEventRaiseCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The parameter count of the <c>(object sender, EventArgs args)</c> event-handler shape.</summary>
    private const int EventHandlerParameterCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.NullEventRaise.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryRewrite(root, model, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    TitleFor(root, diagnostic),
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))),
                    equivalenceKey: nameof(Sst2436NullEventRaiseCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported null argument and replaces it with <c>this</c> or <c>EventArgs.Empty</c>.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the fix is unsafe or the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        var (argument, index) = FindArgument(root, diagnostic);
        if (argument is null)
        {
            return null;
        }

        ExpressionSyntax replacement;
        if (index == 0)
        {
            replacement = SyntaxFactory.ThisExpression();
        }
        else if (index == 1 && ArgsParameterIsExactlyEventArgs(argument, model))
        {
            replacement = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("EventArgs"),
                SyntaxFactory.IdentifierName("Empty"));
        }
        else
        {
            return null;
        }

        var original = argument.Expression;
        return new NodeReplacement(original, replacement.WithTriviaFrom(original));
    }

    /// <summary>Builds the code-action title for the reported argument.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The title.</returns>
    private static string TitleFor(SyntaxNode root, Diagnostic diagnostic)
        => FindArgument(root, diagnostic).Index == 0 ? "Pass 'this' as the sender" : "Pass 'EventArgs.Empty' as the event args";

    /// <summary>Resolves the reported null to its argument and position in the call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The argument and its index, or a null argument when the shape no longer matches.</returns>
    private static (ArgumentSyntax? Argument, int Index) FindArgument(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ArgumentSyntax>() is not { } argument
            || argument.Parent is not ArgumentListSyntax list)
        {
            return (null, -1);
        }

        return (argument, list.Arguments.IndexOf(argument));
    }

    /// <summary>Returns whether the delegate's event-args parameter is exactly <see cref="EventArgs"/>.</summary>
    /// <param name="argument">The reported args argument.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <returns><see langword="true"/> when <c>EventArgs.Empty</c> is a compiling replacement.</returns>
    private static bool ArgsParameterIsExactlyEventArgs(ArgumentSyntax argument, SemanticModel model)
        => argument.Parent?.Parent is InvocationExpressionSyntax invocation
            && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Parameters.Length: EventHandlerParameterCount } invoke
            && SymbolEqualityComparer.Default.Equals(invoke.Parameters[1].Type, model.Compilation.GetTypeByMetadataName("System.EventArgs"));
}
