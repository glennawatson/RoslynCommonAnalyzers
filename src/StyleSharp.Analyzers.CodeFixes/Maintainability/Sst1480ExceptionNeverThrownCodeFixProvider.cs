// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Throws the exception the statement only constructed (SST1480), turning <c>new InvalidOperationException();</c>
/// into <c>throw new InvalidOperationException();</c>.
/// </summary>
/// <remarks>
/// The forgotten <c>throw</c> is the overwhelmingly likely intent, so the fix restores it rather than deleting
/// the statement; a reader who meant to delete it can still do so, but a reader who meant to throw would
/// otherwise lose the exception and its arguments.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1480ExceptionNeverThrownCodeFixProvider))]
[Shared]
public sealed class Sst1480ExceptionNeverThrownCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindStatement, static (current, _) => BuildThrow((ExpressionStatementSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.ExceptionNeverThrown.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(
            context,
            "Throw the exception",
            nameof(Sst1480ExceptionNeverThrownCodeFixProvider),
            FindStatement,
            BuildThrow);

    /// <summary>Builds the throw statement, keeping the statement's indentation and its trailing trivia.</summary>
    /// <param name="statement">The statement that only constructs the exception.</param>
    /// <returns>The throw statement.</returns>
    /// <remarks>
    /// The statement's leading trivia lives on the <c>new</c> keyword, so it moves to the <c>throw</c> keyword
    /// that now starts the line; the original semicolon carries the trailing trivia across untouched.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ThrowStatementSyntax BuildThrow(ExpressionStatementSyntax statement) =>
        SyntaxFactory.ThrowStatement(
            SyntaxFactory.Token(statement.GetLeadingTrivia(), SyntaxKind.ThrowKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            statement.Expression.WithLeadingTrivia(SyntaxFactory.TriviaList()),
            statement.SemicolonToken);

    /// <summary>Resolves the diagnostic's span back to the statement that discards the exception.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The reported statement, or <see langword="null"/> when the reported shape no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionStatementSyntax? FindStatement(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BaseObjectCreationExpressionSyntax creation
            ? creation.Parent as ExpressionStatementSyntax
            : null;
}
