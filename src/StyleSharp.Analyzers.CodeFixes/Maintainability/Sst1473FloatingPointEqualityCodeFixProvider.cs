// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites the NaN comparisons SST1473 reports as a call to <c>float.IsNaN</c> / <c>double.IsNaN</c>. No
/// fix is offered for the general case: an exact comparison is replaced by a comparison against a
/// tolerance, and the tolerance is a property of the caller's problem that no analyzer can supply.
/// </summary>
/// <remarks>
/// <para>
/// The boolean sense differs between the two shapes, and the analyzer decides it rather than the fix:
/// <c>x == double.NaN</c> is always false and the test it stands for is <c>double.IsNaN(x)</c>, while
/// <c>x == x</c> is false only for NaN and therefore already means <c>!double.IsNaN(x)</c>. The analyzer
/// stores which of the two rewrites applies, so the inversion is decided once, where the operands are
/// known.
/// </para>
/// <para>
/// The <c>x == NaN</c> rewrite deliberately changes behaviour — from a comparison that is always false to
/// one that answers the question the author was asking. The <c>x == x</c> rewrite does not: it preserves
/// the value exactly and only says it out loud. Both are offered, because a comparison against NaN is never
/// what was wanted.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1473FloatingPointEqualityCodeFixProvider))]
[Shared]
public sealed class Sst1473FloatingPointEqualityCodeFixProvider : CodeFixProvider
{
    /// <summary>The name of the method that answers whether a value is NaN.</summary>
    private const string IsNaNMethodName = "IsNaN";

    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(RegisterBatchEdits);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.FloatingPointEquality.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(
            context,
            TryCreateTitle,
            static _ => nameof(Sst1473FloatingPointEqualityCodeFixProvider),
            TryRewrite);

    /// <summary>Registers the edits that fix one diagnostic against the editor's original root.</summary>
    /// <param name="editor">The shared document editor.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    internal static void RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetRewrite(editor.OriginalRoot, diagnostic, out var binary, out var keyword, out var negated))
        {
            return;
        }

        editor.ReplaceNode(binary!, (current, _) => Rewrite((BinaryExpressionSyntax)current, keyword, negated));
    }

    /// <summary>Resolves a diagnostic to the comparison it reported and the rewrite the analyzer chose.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="binary">The reported comparison, when the shape still matches.</param>
    /// <param name="keyword">The <c>float</c> or <c>double</c> keyword.</param>
    /// <param name="negated">Whether the rewrite the analyzer chose negates the <c>IsNaN</c> call.</param>
    /// <returns><see langword="true"/> when the diagnostic carries a rewrite this fix can apply.</returns>
    /// <remarks>
    /// A diagnostic with no rewrite properties is the general tolerance case, which has no fix — that is how
    /// one descriptor serves both, without the fix having to re-derive what the analyzer already knew.
    /// </remarks>
    private static bool TryGetRewrite(
        SyntaxNode root,
        Diagnostic diagnostic,
        out BinaryExpressionSyntax? binary,
        out string keyword,
        out bool negated)
    {
        binary = null;
        keyword = string.Empty;
        negated = false;

        if (!diagnostic.Properties.TryGetValue(Sst1473FloatingPointEqualityAnalyzer.TypeKeywordKey, out var storedKeyword)
            || !diagnostic.Properties.TryGetValue(Sst1473FloatingPointEqualityAnalyzer.FixKindKey, out var storedFixKind)
            || storedKeyword is null
            || storedFixKind is null
            || GetTypeKeywordToken(storedKeyword) == SyntaxKind.None)
        {
            return false;
        }

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax found)
        {
            return false;
        }

        binary = found;
        keyword = storedKeyword;
        negated = storedFixKind == Sst1473FloatingPointEqualityAnalyzer.NotIsNaNFixKind;
        return true;
    }

    /// <summary>Words the action for the reported comparison, when it still rewrites to an <c>IsNaN</c> call.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The code action title, or <see langword="null"/> when the comparison no longer matches.</returns>
    private static string? TryCreateTitle(SyntaxNode root, Diagnostic diagnostic) =>
        !TryGetRewrite(root, diagnostic, out _, out var keyword, out var negated)
            ? null
            : $"Use '{(negated ? "!" : string.Empty)}{keyword}.{IsNaNMethodName}(...)'";

    /// <summary>Resolves the reported comparison and builds its <c>IsNaN</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the comparison no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic) =>
        TryGetRewrite(root, diagnostic, out var binary, out var keyword, out var negated)
            ? new NodeReplacement(binary!, Rewrite(binary!, keyword, negated))
            : null;

    /// <summary>Builds the <c>IsNaN</c> call that replaces the comparison.</summary>
    /// <param name="binary">The reported comparison.</param>
    /// <param name="keyword">The <c>float</c> or <c>double</c> keyword.</param>
    /// <param name="negated">Whether the call is negated.</param>
    /// <returns>The replacement expression, carrying the comparison's outer trivia.</returns>
    /// <remarks>
    /// No parentheses are ever needed. An invocation is a primary expression, and <c>!</c> binds tighter
    /// than every operator that can hold an unparenthesized equality — so wherever the comparison stood, the
    /// replacement stands too.
    /// </remarks>
    private static ExpressionSyntax Rewrite(BinaryExpressionSyntax binary, string keyword, bool negated)
    {
        var call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(GetTypeKeywordToken(keyword))),
                SyntaxFactory.IdentifierName(IsNaNMethodName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Argument(GetTestedOperand(binary).WithoutTrivia()))));

        ExpressionSyntax replacement = negated
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, call)
            : call;

        return replacement.WithTriviaFrom(binary);
    }

    /// <summary>Gets the operand whose NaN-ness the comparison was really asking about.</summary>
    /// <param name="binary">The reported comparison.</param>
    /// <returns>The operand that is not the <c>NaN</c> literal.</returns>
    /// <remarks>
    /// For a self-comparison neither operand is <c>NaN</c> and the two are equivalent, so the left one is
    /// the answer either way.
    /// </remarks>
    private static ExpressionSyntax GetTestedOperand(BinaryExpressionSyntax binary) =>
        Sst1473FloatingPointEqualityAnalyzer.IsNaNShaped(binary.Left) ? binary.Right : binary.Left;

    /// <summary>Maps a stored type keyword to its predefined-type token.</summary>
    /// <param name="keyword">The <c>float</c> or <c>double</c> keyword.</param>
    /// <returns>The token kind, or <see cref="SyntaxKind.None"/> when the keyword is not one this fix writes.</returns>
    private static SyntaxKind GetTypeKeywordToken(string keyword) => keyword switch
    {
        FloatingPointTypes.DoubleKeyword => SyntaxKind.DoubleKeyword,
        FloatingPointTypes.SingleKeyword => SyntaxKind.FloatKeyword,
        _ => SyntaxKind.None,
    };
}
