// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(
        ModernizationRules.UseThrowIfNull.Id,
        ModernizationRules.UseThrowIfNullOrEmpty.Id,
        ModernizationRules.UseThrowIfNullOrWhiteSpace.Id,
        ModernizationRules.UseObjectDisposedThrowIf.Id,
        ModernizationRules.UseArgumentOutOfRangeThrowIf.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            static (root, diagnostic) => FindGuard(root, diagnostic) is { } ifStatement && CanReplaceStatement(diagnostic.Id, ifStatement) ? "Use guard helper" : null,
            static _ => nameof(ArgumentGuardCodeFixProvider),
            TryRewrite);

    /// <summary>Resolves the reported guard statement and builds its throw-helper replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The guard statement and its replacement, or <see langword="null"/> when the statement no longer matches.</returns>
    internal static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        FindGuard(root, diagnostic) is { } ifStatement && BuildReplacement(diagnostic.Id, ifStatement) is { } replacement
            ? new NodeReplacement(ifStatement, replacement)
            : null;

    /// <summary>Resolves the diagnostic's span to the guard statement it was reported on.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The enclosing if statement, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IfStatementSyntax? FindGuard(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<IfStatementSyntax>();

    /// <summary>Builds the throw-helper statement for the matched guard, carrying the guard's trivia.</summary>
    /// <param name="diagnosticId">The reported diagnostic id, selecting the helper to emit.</param>
    /// <param name="ifStatement">The if statement to replace.</param>
    /// <returns>The replacement statement, or <see langword="null"/> when the guard no longer matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionStatementSyntax? BuildReplacement(string diagnosticId, IfStatementSyntax ifStatement) =>
        BuildReplacementStatement(diagnosticId, ifStatement)?.WithTriviaFrom(ifStatement);

    /// <summary>Builds the throw-helper statement syntax for the matched guard, or null when it no longer matches.</summary>
    /// <param name="diagnosticId">The reported diagnostic id, selecting the helper to emit.</param>
    /// <param name="ifStatement">The if statement to replace.</param>
    /// <returns>The replacement statement syntax, or <see langword="null"/>.</returns>
    private static ExpressionStatementSyntax? BuildReplacementStatement(string diagnosticId, IfStatementSyntax ifStatement)
    {
        if (diagnosticId == ModernizationRules.UseThrowIfNull.Id)
        {
            return ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var expression)
                ? CreateHelperStatement(nameof(ArgumentNullException), "ThrowIfNull", SyntaxFactory.Argument(expression!.WithoutTrivia()))
                : null;
        }

        if (diagnosticId == ModernizationRules.UseObjectDisposedThrowIf.Id)
        {
            return ThrowGuardPatterns.TryMatchObjectDisposed(ifStatement, out var condition)
                ? CreateHelperStatement(
                    nameof(ObjectDisposedException),
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
                    nameof(ArgumentOutOfRangeException),
                    match.Helper,
                    SyntaxFactory.Argument(match.Value.WithoutTrivia()))
                : CreateHelperStatement(
                    nameof(ArgumentOutOfRangeException),
                    match.Helper,
                    SyntaxFactory.Argument(match.Value.WithoutTrivia()),
                    SyntaxFactory.Argument(match.Bound.WithoutTrivia()));
        }

        if (!ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out _, out var stringExpression))
        {
            return null;
        }

        var method = diagnosticId == ModernizationRules.UseThrowIfNullOrEmpty.Id ? "ThrowIfNullOrEmpty" : "ThrowIfNullOrWhiteSpace";
        return CreateHelperStatement(nameof(ArgumentException), method, SyntaxFactory.Argument(stringExpression!.WithoutTrivia()));
    }

    /// <summary>Builds an expression statement that invokes the selected throw-helper.</summary>
    /// <param name="typeName">The helper type name.</param>
    /// <param name="methodName">The helper method name.</param>
    /// <param name="arguments">The helper-call arguments.</param>
    /// <returns>The helper-call statement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionStatementSyntax CreateHelperStatement(string typeName, string methodName, params ArgumentSyntax[] arguments) =>
        SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(typeName),
                    SyntaxFactory.IdentifierName(methodName)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments))));

    /// <summary>Checks the same guard patterns as the builder without constructing the helper call.</summary>
    /// <param name="diagnosticId">The diagnostic selecting the helper.</param>
    /// <param name="ifStatement">The guard statement.</param>
    /// <returns>Whether the statement has a replacement.</returns>
    private static bool CanReplaceStatement(string diagnosticId, IfStatementSyntax ifStatement)
    {
        if (diagnosticId == ModernizationRules.UseThrowIfNull.Id)
        {
            return ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out _);
        }

        if (diagnosticId == ModernizationRules.UseObjectDisposedThrowIf.Id)
        {
            return ThrowGuardPatterns.TryMatchObjectDisposed(ifStatement, out _);
        }

        return diagnosticId == ModernizationRules.UseArgumentOutOfRangeThrowIf.Id
            ? ThrowGuardPatterns.TryMatchRangeGuard(ifStatement, out _)
            : ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out _, out _);
    }
}
