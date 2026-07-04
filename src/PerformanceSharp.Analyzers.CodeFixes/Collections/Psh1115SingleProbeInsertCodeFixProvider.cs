// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported <c>ContainsKey</c>-guarded indexer write to a single
/// <c>TryAdd(key, value)</c> call (PSH1115). Only the TryAdd shape is fixed automatically —
/// the value expression moves out of the guarded branch, so it is evaluated even when the key
/// exists, which matches the framework guidance for TryAdd. The value-slot shape is reported
/// without a fix because rewriting it needs a ref local the surrounding code must adopt.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1115SingleProbeInsertCodeFixProvider))]
[Shared]
public sealed class Psh1115SingleProbeInsertCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.SingleProbeInsert.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use TryAdd", nameof(Psh1115SingleProbeInsertCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported guard and builds the TryAdd statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> for the value-slot shape.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not IfStatementSyntax ifStatement
            || Psh1115SingleProbeInsertAnalyzer.TryGetNegatedGuard(
                ifStatement,
                Psh1115SingleProbeInsertAnalyzer.ContainsKeyMethodName,
                argumentCount: 1) is not { } guard
            || Psh1115SingleProbeInsertAnalyzer.TryGetGuardedIndexerStore(ifStatement, guard.Receiver, guard.Key) is not { } value)
        {
            return null;
        }

        var tryAdd = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                guard.Receiver.WithoutTrivia(),
                SyntaxFactory.IdentifierName(Psh1115SingleProbeInsertAnalyzer.TryAddMethodName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(ImmutableArrays.Of(
                SyntaxFactory.Argument(guard.Key.WithoutTrivia()),
                SyntaxFactory.Argument(value.WithoutTrivia()).WithLeadingTrivia(SyntaxFactory.Space)))));

        return new NodeReplacement(ifStatement, SyntaxFactory.ExpressionStatement(tryAdd).WithTriviaFrom(ifStatement));
    }
}
