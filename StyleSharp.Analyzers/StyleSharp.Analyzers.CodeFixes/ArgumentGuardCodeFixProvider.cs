// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a hand-written null guard with <c>ArgumentNullException.ThrowIfNull(...)</c>
/// (SST2000), preserving the original statement's leading and trailing trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArgumentGuardCodeFixProvider))]
[Shared]
public sealed class ArgumentGuardCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernizationRules.UseThrowIfNull.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
                || !ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var expression))
            {
                continue;
            }

            var replacement = SyntaxFactory.ParseStatement($"ArgumentNullException.ThrowIfNull({expression});").WithTriviaFrom(ifStatement);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use ArgumentNullException.ThrowIfNull",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(ifStatement, replacement))),
                    equivalenceKey: nameof(ArgumentGuardCodeFixProvider)),
                diagnostic);
        }
    }
}
