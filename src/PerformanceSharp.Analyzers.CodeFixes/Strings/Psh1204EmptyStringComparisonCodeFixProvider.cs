// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a comparison against the empty string to a null-safe length pattern
/// (PSH1204): <c>x == ""</c> becomes <c>x is { Length: 0 }</c> and <c>x != ""</c>
/// becomes <c>x is not { Length: 0 }</c>. The forms are exact equivalents — null
/// compares unequal to <c>""</c> and also fails the property pattern. The fix is
/// only offered on C# 9 or later, where the <c>not</c> pattern exists; older files
/// keep the diagnostic without a fix.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1204EmptyStringComparisonCodeFixProvider))]
[Shared]
public sealed class Psh1204EmptyStringComparisonCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The <c>{ Length: 0 }</c> pattern reused across fixes.</summary>
    private static readonly PatternSyntax LengthZeroPattern = BuildLengthZeroPattern();

    /// <summary>The <c>not { Length: 0 }</c> pattern reused across fixes.</summary>
    private static readonly PatternSyntax NotLengthZeroPattern = SyntaxFactory.UnaryPattern(
        SyntaxFactory.Token(default, SyntaxKind.NotKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
        BuildLengthZeroPattern());

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.EmptyStringComparison.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use a null-safe length pattern", nameof(Psh1204EmptyStringComparisonCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported comparison with its length-pattern form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison expression to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
        => TryGetReplacement(comparison, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement!))
            : document;

    /// <summary>Resolves the reported comparison and builds its length-pattern replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => SupportsPatternFix(root.SyntaxTree)
            && root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BinaryExpressionSyntax binary
            && TryGetReplacement(binary, out var replacement)
            ? new NodeReplacement(binary, replacement!)
            : null;

    /// <summary>Returns whether the file's language version supports the <c>not</c> pattern the fix emits.</summary>
    /// <param name="tree">The syntax tree being fixed.</param>
    /// <returns><see langword="true"/> for C# 9 or later.</returns>
    private static bool SupportsPatternFix(SyntaxTree tree)
        => ((CSharpParseOptions)tree.Options).LanguageVersion >= LanguageVersion.CSharp9;

    /// <summary>Builds the length-pattern replacement for a reported comparison.</summary>
    /// <param name="binary">The comparison expression to rewrite.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(BinaryExpressionSyntax binary, out ExpressionSyntax? replacement)
    {
        if (!Psh1204EmptyStringComparisonAnalyzer.TryGetOperands(binary, out _, out var value, out _))
        {
            replacement = null;
            return false;
        }

        var pattern = binary.IsKind(SyntaxKind.NotEqualsExpression) ? NotLengthZeroPattern : LengthZeroPattern;
        ExpressionSyntax result = SyntaxFactory.IsPatternExpression(
            value!.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.IsKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            pattern);

        if (NeedsParentheses(binary))
        {
            result = SyntaxFactory.ParenthesizedExpression(result);
        }

        // NormalizeWhitespace strips the elastic trivia the SyntaxFactory pattern carries; without it the
        // Roslyn 4.8 code-action formatter expands the property pattern onto multiple lines.
        replacement = result.NormalizeWhitespace().WithTriviaFrom(binary);
        return true;
    }

    /// <summary>Returns whether the pattern expression must be parenthesized in the comparison's context.</summary>
    /// <param name="binary">The comparison being replaced.</param>
    /// <returns><see langword="true"/> when the parent is an enclosing expression rather than a statement, argument, clause, or assignment-value position.</returns>
    private static bool NeedsParentheses(BinaryExpressionSyntax binary)
    {
        if (binary.Parent is not ExpressionSyntax parent || parent is ParenthesizedExpressionSyntax)
        {
            return false;
        }

        return parent is not AssignmentExpressionSyntax assignment || assignment.Right != binary;
    }

    /// <summary>Builds the <c>{ Length: 0 }</c> property pattern with conventional spacing.</summary>
    /// <returns>The built pattern.</returns>
    private static RecursivePatternSyntax BuildLengthZeroPattern()
        => SyntaxFactory.RecursivePattern(
            type: null,
            positionalPatternClause: null,
            propertyPatternClause: SyntaxFactory.PropertyPatternClause(
                SyntaxFactory.Token(default, SyntaxKind.OpenBraceToken, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Subpattern(
                        SyntaxFactory.NameColon(
                            SyntaxFactory.IdentifierName("Length"),
                            SyntaxFactory.Token(default, SyntaxKind.ColonToken, SyntaxFactory.TriviaList(SyntaxFactory.Space))),
                        SyntaxFactory.ConstantPattern(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(0))))),
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.CloseBraceToken, default)),
            designation: null);
}
