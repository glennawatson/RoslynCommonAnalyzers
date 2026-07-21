// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a flags-enum member's numeric literal into the OR of the members it combines (SST2330) — <c>7</c>
/// becomes <c>Read | Write | Execute</c> — so the value states its own meaning and survives a renumbering of
/// the members it names.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2330FlagsCombinationLiteralCodeFixProvider))]
[Shared]
public sealed class Sst2330FlagsCombinationLiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(DesignRules.FlagsCombinationLiteralShouldNameMembers.Id);

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
            if (Resolve(root, diagnostic) is not var (literal, replacement))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Name the combined flags",
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(literal, replacement))),
                    equivalenceKey: nameof(Sst2330FlagsCombinationLiteralCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (Resolve(editor.OriginalRoot, diagnostic) is not var (literal, replacement))
        {
            return;
        }

        editor.ReplaceNode(literal, replacement);
    }

    /// <summary>Resolves the diagnostic to the literal to replace and its OR-of-members replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The literal and its replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static (LiteralExpressionSyntax Literal, ExpressionSyntax Replacement)? Resolve(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LiteralExpressionSyntax>() is not { } literal
            || !diagnostic.Properties.TryGetValue(Sst2330FlagsCombinationLiteralAnalyzer.MembersKey, out var members)
            || string.IsNullOrEmpty(members))
        {
            return null;
        }

        var replacement = SyntaxFactory.ParseExpression(members!.Replace(",", " | ")).WithTriviaFrom(literal);
        return (literal, replacement);
    }
}
