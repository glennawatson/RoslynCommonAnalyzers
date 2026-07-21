// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Folds an <c>as</c> assignment and its following null check into an <c>is</c> declaration pattern (SST2274):
/// the declaration is removed and the <c>if</c> condition becomes <c>o is T s</c> (guarded-use shape) or
/// <c>o is not T s</c> (early-exit shape), leaving the guarded body and any later uses of <c>s</c> unchanged.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2274AsAssignmentToIsPatternCodeFixProvider))]
[Shared]
public sealed class Sst2274AsAssignmentToIsPatternCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(ModernSyntaxRules.ConvertAsAssignmentToIsPattern.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Convert to an 'is' pattern", nameof(Sst2274AsAssignmentToIsPatternCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Re-derives the candidate shape and rebuilds the block without the declaration.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The block replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<LocalDeclarationStatementSyntax>() is not { } local
            || !Sst2274AsAssignmentToIsPatternAnalyzer.TryGetSyntacticCandidate(local, out var candidate))
        {
            return null;
        }

        var operand = PatternMatchingAnalyzer.Unwrap(candidate.AsExpression.Left);
        var condition = Sst2274AsAssignmentToIsPatternAnalyzer.BuildPattern(
            operand,
            candidate.Type,
            candidate.Declarator.Identifier.ValueText,
            candidate.IsNegative);
        var newIf = candidate.IfStatement.WithCondition(condition);

        // The declaration is one of the block's own statements, so its index is always found.
        var statements = candidate.Block.Statements;
        var declarationIndex = statements.IndexOf(candidate.Declaration);
        var rewritten = statements.Replace(candidate.IfStatement, newIf).RemoveAt(declarationIndex);
        var newBlock = candidate.Block.WithStatements(rewritten);
        return new NodeReplacement(candidate.Block, newBlock);
    }
}
