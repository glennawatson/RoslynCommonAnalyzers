// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a recorded local-clock read to the UTC clock (SST2011): <c>DateTime.Now</c> becomes
/// <c>DateTime.UtcNow</c>, <c>DateTimeOffset.Now</c> becomes <c>DateTimeOffset.UtcNow</c>.
/// </summary>
/// <remarks>
/// The fix changes the recorded value — that is what the rule is asking for — but not the type of the
/// expression, so nothing downstream stops compiling. The rewritten member access is bound speculatively
/// before the fix is offered, so a <c>Now</c> on some other type is never rewritten into a <c>UtcNow</c> that
/// does not exist.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2011RecordInstantsInUtcCodeFixProvider))]
[Shared]
public sealed class Sst2011RecordInstantsInUtcCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The UTC clock property name.</summary>
    private const string UtcNowName = "UtcNow";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.RecordInstantsInUtc.Id);

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
            if (!TryBuildReplacement(root, model, diagnostic, out var access, out var replacement))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Read the UTC clock",
                    _ => Task.FromResult(Apply(context.Document, root, access!, replacement!)),
                    equivalenceKey: nameof(Sst2011RecordInstantsInUtcCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryBuildReplacement(editor.OriginalRoot, editor.SemanticModel, diagnostic, out var access, out var replacement))
        {
            return;
        }

        editor.ReplaceNode(access!, replacement!);
    }

    /// <summary>Rewrites one reported clock read to the UTC clock.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="access">The reported member access.</param>
    /// <param name="replacement">The UTC member access built for the reported read.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(
        Document document,
        SyntaxNode root,
        MemberAccessExpressionSyntax access,
        MemberAccessExpressionSyntax replacement)
        => document.WithSyntaxRoot(root.ReplaceNode(access, replacement));

    /// <summary>Resolves the reported clock read and builds its UTC replacement, if the rewrite binds.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="access">The reported member access, when the shape still matches.</param>
    /// <param name="replacement">The UTC member access, when it binds.</param>
    /// <returns><see langword="true"/> when the fix can be offered.</returns>
    internal static bool TryBuildReplacement(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        out MemberAccessExpressionSyntax? access,
        out MemberAccessExpressionSyntax? replacement)
    {
        replacement = null;
        access = root.FindNode(diagnostic.Location.SourceSpan) as MemberAccessExpressionSyntax;
        if (access is null)
        {
            return false;
        }

        var candidate = access.WithName(SyntaxFactory.IdentifierName(UtcNowName).WithTriviaFrom(access.Name));
        var speculative = model.GetSpeculativeSymbolInfo(access.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression);
        if (speculative.Symbol is not IPropertySymbol { IsStatic: true })
        {
            return false;
        }

        replacement = candidate;
        return true;
    }
}
