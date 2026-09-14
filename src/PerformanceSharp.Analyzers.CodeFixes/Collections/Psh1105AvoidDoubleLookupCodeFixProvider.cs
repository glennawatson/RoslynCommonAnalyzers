// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Removes a redundant membership guard around a mutating call (PSH1105): the if
/// statement collapses to its body call for the <c>Remove</c> and bool-returning
/// <c>Add</c> pairings, and the guarded two-argument <c>Add</c> pairing is rewritten
/// to a single <c>TryAdd</c> call. The if statement's leading trivia is preserved.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1105AvoidDoubleLookupCodeFixProvider))]
[Shared]
public sealed class Psh1105AvoidDoubleLookupCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.AvoidDoubleLookup.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Psh1105AvoidDoubleLookupCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported guard and builds the single lookup that replaces it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the guard no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        FindIfStatement(root, diagnostic) is { } ifStatement
            && Psh1105AvoidDoubleLookupAnalyzer.TryGetShape(ifStatement, out var shape)
            ? new NodeReplacement(ifStatement, CreateReplacement(ifStatement, shape))
            : null;

    /// <summary>Resolves the if statement enclosing the reported node.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported if statement, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IfStatementSyntax? FindIfStatement(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>();

    /// <summary>Words the action for the reported guard: a TryAdd when it adds, otherwise dropping the redundant lookup.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the guard no longer matches.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        FindIfStatement(root, diagnostic) is { } ifStatement && Psh1105AvoidDoubleLookupAnalyzer.TryGetShape(ifStatement, out var shape)
            ? TitleFor(shape)
            : null;

    /// <summary>Words the action for a resolved guard shape.</summary>
    /// <param name="shape">The resolved double-lookup shape.</param>
    /// <returns>The code action title.</returns>
    private static string TitleFor(in Psh1105AvoidDoubleLookupAnalyzer.DoubleLookupShape shape) =>
        shape.RequiresTryAdd ? "Use TryAdd" : "Remove the redundant lookup guard";

    /// <summary>Builds the statement that replaces the guard, rewriting Add to TryAdd where required.</summary>
    /// <param name="ifStatement">The reported if statement.</param>
    /// <param name="shape">The validated shape.</param>
    /// <returns>The body call statement carrying the if statement's outer trivia.</returns>
    private static ExpressionStatementSyntax CreateReplacement(IfStatementSyntax ifStatement, in Psh1105AvoidDoubleLookupAnalyzer.DoubleLookupShape shape)
    {
        var statement = shape.Body;
        if (shape.RequiresTryAdd)
        {
            statement = statement.ReplaceNode(
                shape.MutationName,
                SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                    shape.MutationName.GetLeadingTrivia(),
                    Psh1105AvoidDoubleLookupAnalyzer.TryAddMethodName,
                    shape.MutationName.GetTrailingTrivia())));
        }

        var attributeLists = statement.AttributeLists;
        var expression = statement.Expression;
        if (attributeLists.Count == 0)
        {
            expression = expression.WithLeadingTrivia(ifStatement.GetLeadingTrivia());
        }
        else
        {
            attributeLists = attributeLists.Replace(attributeLists[0], attributeLists[0].WithLeadingTrivia(ifStatement.GetLeadingTrivia()));
        }

        return statement.Update(
            attributeLists,
            expression,
            statement.SemicolonToken.WithTrailingTrivia(ifStatement.GetTrailingTrivia()));
    }
}
