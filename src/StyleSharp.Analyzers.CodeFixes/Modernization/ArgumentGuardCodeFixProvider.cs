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
public sealed class ArgumentGuardCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernizationRules.UseThrowIfNull.Id,
        ModernizationRules.UseThrowIfNullOrEmpty.Id,
        ModernizationRules.UseThrowIfNullOrWhiteSpace.Id,
        ModernizationRules.UseObjectDisposedThrowIf.Id,
        ModernizationRules.UseArgumentOutOfRangeThrowIf.Id);

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
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement
                || BuildReplacementStatement(diagnostic.Id, ifStatement) is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use guard helper",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, ifStatement, diagnostic.Id)),
                    equivalenceKey: nameof(ArgumentGuardCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>() is not { } ifStatement)
        {
            return;
        }

        if (BuildReplacementStatement(diagnostic.Id, ifStatement) is not { } replacement)
        {
            return;
        }

        editor.ReplaceNode(ifStatement, replacement.WithTriviaFrom(ifStatement));
    }

    /// <summary>Replaces the matched guard statement with the corresponding throw-helper call.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="ifStatement">The guard statement.</param>
    /// <param name="diagnosticId">The diagnostic id selecting the replacement helper.</param>
    /// <returns>The updated document, or the original document when the statement no longer matches.</returns>
    internal static Document Apply(Document document, SyntaxNode root, IfStatementSyntax ifStatement, string diagnosticId)
    {
        var replacement = BuildReplacementStatement(diagnosticId, ifStatement);
        if (replacement is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(ifStatement, replacement.WithTriviaFrom(ifStatement)));
    }

    /// <summary>Builds the throw-helper statement syntax for the matched guard, or null when it no longer matches.</summary>
    /// <param name="diagnosticId">The reported diagnostic id, selecting the helper to emit.</param>
    /// <param name="ifStatement">The if statement to replace.</param>
    /// <returns>The replacement statement syntax, or <see langword="null"/>.</returns>
    private static ExpressionStatementSyntax? BuildReplacementStatement(string diagnosticId, IfStatementSyntax ifStatement)
    {
        if (diagnosticId == ModernizationRules.UseThrowIfNull.Id)
        {
            return ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var expression)
                ? CreateHelperStatement("ArgumentNullException", "ThrowIfNull", SyntaxFactory.Argument(expression!.WithoutTrivia()))
                : null;
        }

        if (diagnosticId == ModernizationRules.UseObjectDisposedThrowIf.Id)
        {
            return ThrowGuardPatterns.TryMatchObjectDisposed(ifStatement, out var condition)
                ? CreateHelperStatement(
                    "ObjectDisposedException",
                    "ThrowIf",
                    SyntaxFactory.Argument(condition!.WithoutTrivia()),
                    SyntaxFactory.Argument(SyntaxFactory.ThisExpression()))
                : null;
        }

        if (diagnosticId == ModernizationRules.UseArgumentOutOfRangeThrowIf.Id)
        {
            if (!ThrowGuardPatterns.TryMatchRangeGuard(ifStatement, out var match))
            {
                return null;
            }

            return match.Bound is null
                ? CreateHelperStatement(
                    "ArgumentOutOfRangeException",
                    match.Helper,
                    SyntaxFactory.Argument(match.Value.WithoutTrivia()))
                : CreateHelperStatement(
                    "ArgumentOutOfRangeException",
                    match.Helper,
                    SyntaxFactory.Argument(match.Value.WithoutTrivia()),
                    SyntaxFactory.Argument(match.Bound.WithoutTrivia()));
        }

        if (!ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out _, out var stringExpression))
        {
            return null;
        }

        var method = diagnosticId == ModernizationRules.UseThrowIfNullOrEmpty.Id ? "ThrowIfNullOrEmpty" : "ThrowIfNullOrWhiteSpace";
        return CreateHelperStatement("ArgumentException", method, SyntaxFactory.Argument(stringExpression!.WithoutTrivia()));
    }

    /// <summary>Builds an expression statement that invokes the selected throw-helper.</summary>
    /// <param name="typeName">The helper type name.</param>
    /// <param name="methodName">The helper method name.</param>
    /// <param name="arguments">The helper-call arguments.</param>
    /// <returns>The helper-call statement.</returns>
    private static ExpressionStatementSyntax CreateHelperStatement(string typeName, string methodName, params ArgumentSyntax[] arguments)
        => SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(typeName),
                    SyntaxFactory.IdentifierName(methodName)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments))));
}
