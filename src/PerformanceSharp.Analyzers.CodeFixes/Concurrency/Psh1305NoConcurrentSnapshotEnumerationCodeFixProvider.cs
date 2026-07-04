// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a foreach over a <c>ConcurrentDictionary</c> snapshot (PSH1305) into a pair
/// deconstruction over the dictionary itself: <c>foreach (var k in d.Keys)</c> becomes
/// <c>foreach (var (k, _) in d)</c> and <c>foreach (var v in d.Values)</c> becomes
/// <c>foreach (var (_, v) in d)</c>, so the loop variable keeps its name and the body is
/// untouched. The fix is offered only when <c>KeyValuePair</c> exposes <c>Deconstruct</c>
/// (.NET Core 2.0+ / netstandard2.1+) and the loop declares a <c>var</c>-typed variable.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1305NoConcurrentSnapshotEnumerationCodeFixProvider))]
[Shared]
public sealed class Psh1305NoConcurrentSnapshotEnumerationCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The metadata name of the key/value pair type probed for Deconstruct.</summary>
    private const string KeyValuePairMetadataName = "System.Collections.Generic.KeyValuePair`2";

    /// <summary>The deconstruct member the rewrite relies on.</summary>
    private const string DeconstructMethodName = "Deconstruct";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ConcurrencyRules.NoConcurrentSnapshotEnumeration.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Enumerate the dictionary's key/value pairs", nameof(Psh1305NoConcurrentSnapshotEnumerationCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported foreach and builds its deconstructing replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
        => PairSupportsDeconstruct(model.Compilation)
            && TryGetFixableForEach(root, diagnostic) is { } statement
            ? new NodeReplacement(statement, Rewrite(statement))
            : null;

    /// <summary>Returns whether the compilation's key/value pair type exposes a Deconstruct method.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns><see langword="true"/> when the deconstruction rewrite compiles.</returns>
    private static bool PairSupportsDeconstruct(Compilation compilation)
        => compilation.GetTypeByMetadataName(KeyValuePairMetadataName) is { } pairType
            && !pairType.GetMembers(DeconstructMethodName).IsEmpty;

    /// <summary>Returns the reported foreach when it declares a var-typed variable over a snapshot access.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The foreach statement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ForEachStatementSyntax? TryGetFixableForEach(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not MemberAccessExpressionSyntax access
            || access.Parent is not ForEachStatementSyntax { Type: IdentifierNameSyntax { IsVar: true } } statement
            || Psh1305NoConcurrentSnapshotEnumerationAnalyzer.TryGetSnapshotAccess(statement) is null)
        {
            return null;
        }

        return statement;
    }

    /// <summary>Builds the deconstructing foreach over the dictionary itself.</summary>
    /// <param name="statement">The foreach statement to rewrite; callers must have validated the shape.</param>
    /// <returns>The rewritten foreach-variable statement.</returns>
    private static ForEachVariableStatementSyntax Rewrite(ForEachStatementSyntax statement)
    {
        var access = (MemberAccessExpressionSyntax)statement.Expression;
        var isKeys = access.Name.Identifier.ValueText == Psh1305NoConcurrentSnapshotEnumerationAnalyzer.KeysPropertyName;

        var variable = SyntaxFactory.SingleVariableDesignation(statement.Identifier.WithoutTrivia());
        var discard = SyntaxFactory.DiscardDesignation();
        var designation = SyntaxFactory.ParenthesizedVariableDesignation(
            SyntaxFactory.SeparatedList<VariableDesignationSyntax>(
                isKeys ? new VariableDesignationSyntax[] { variable, discard } : [discard, variable]));

        var declaration = SyntaxFactory.DeclarationExpression(
                SyntaxFactory.IdentifierName(SyntaxFactory.Identifier("var")),
                designation.WithLeadingTrivia(SyntaxFactory.Space))
            .WithTrailingTrivia(SyntaxFactory.Space);

        return SyntaxFactory.ForEachVariableStatement(
            statement.AttributeLists,
            statement.AwaitKeyword,
            statement.ForEachKeyword,
            statement.OpenParenToken,
            declaration,
            statement.InKeyword,
            access.Expression.WithTriviaFrom(statement.Expression),
            statement.CloseParenToken,
            statement.Statement);
    }
}
