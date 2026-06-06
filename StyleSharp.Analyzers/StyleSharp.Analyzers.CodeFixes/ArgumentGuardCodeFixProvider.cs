// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a hand-written argument guard with the corresponding modern runtime
/// throw-helper call (SST2000/SST2001/SST2002), preserving the original statement's
/// leading and trailing trivia.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ArgumentGuardCodeFixProvider))]
[Shared]
public sealed class ArgumentGuardCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernizationRules.UseThrowIfNull.Id,
        ModernizationRules.UseThrowIfNullOrEmpty.Id,
        ModernizationRules.UseThrowIfNullOrWhiteSpace.Id);

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
                || BuildReplacementCall(diagnostic.Id, ifStatement) is not { } call)
            {
                continue;
            }

            var replacement = SyntaxFactory.ParseStatement(call).WithTriviaFrom(ifStatement);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use guard helper",
                    cancellationToken => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(ifStatement, replacement))),
                    equivalenceKey: nameof(ArgumentGuardCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Builds the throw-helper statement text for the matched guard, or null when it no longer matches.</summary>
    /// <param name="diagnosticId">The reported diagnostic id, selecting the helper to emit.</param>
    /// <param name="ifStatement">The if statement to replace.</param>
    /// <returns>The replacement statement text, or <see langword="null"/>.</returns>
    private static string? BuildReplacementCall(string diagnosticId, IfStatementSyntax ifStatement)
    {
        if (diagnosticId == ModernizationRules.UseThrowIfNull.Id)
        {
            return ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var expression)
                ? $"ArgumentNullException.ThrowIfNull({expression});"
                : null;
        }

        if (!ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out _, out var stringExpression))
        {
            return null;
        }

        var method = diagnosticId == ModernizationRules.UseThrowIfNullOrEmpty.Id ? "ThrowIfNullOrEmpty" : "ThrowIfNullOrWhiteSpace";
        return $"ArgumentException.{method}({stringExpression});";
    }
}
