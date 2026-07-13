// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a comparison against the empty string (PSH1204) into the replacement the analyzer
/// settled on and recorded in the diagnostic: the null-safe pattern <c>x is { Length: 0 }</c> by
/// default, the direct <c>x.Length == 0</c>, or <c>string.IsNullOrEmpty(x)</c>. The <c>!=</c> forms
/// negate — <c>x is not { Length: 0 }</c>, <c>x.Length != 0</c>, <c>!string.IsNullOrEmpty(x)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fix never decides the shape itself.</b> Two of the three replacements disagree with
/// <c>== ""</c> when the string is null — the length test throws, <c>IsNullOrEmpty</c> answers
/// <see langword="true"/> — and nothing in a code fix's syntax-only view can tell whether the operand
/// might be null. The analyzer, which has the flow state, has already answered that and written the
/// permitted style into <see cref="EmptyStringStyleOptions.StyleKey"/>; a diagnostic that carries no
/// style at all falls back to the pattern, the one form that is an exact equivalent for every input.
/// </para>
/// <para>
/// Only the pattern needs C# 9. The length test and <c>string.IsNullOrEmpty</c> compile on every
/// supported language version, so a file below C# 9 that configures either of them still gets a fix
/// where the operand is known not to be null — and no fix at all otherwise.
/// </para>
/// </remarks>
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
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryRewrite(root, diagnostic) is not { } edit)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    GetTitle(EmptyStringStyleOptions.ReadStyle(diagnostic.Properties)),
                    _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(edit.Original, edit.Replacement))),
                    equivalenceKey: nameof(Psh1204EmptyStringComparisonCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported comparison with its default length-pattern form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison expression to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
        => TryGetReplacement(comparison, EmptyStringStyle.Pattern, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement!))
            : document;

    /// <summary>Resolves the reported comparison and builds the replacement the analyzer permitted.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        var style = EmptyStringStyleOptions.ReadStyle(diagnostic.Properties);
        return SupportsStyle(root.SyntaxTree, style)
            && root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BinaryExpressionSyntax binary
            && TryGetReplacement(binary, style, out var replacement)
            ? new NodeReplacement(binary, replacement!)
            : null;
    }

    /// <summary>Returns whether the file's language version supports the syntax one style emits.</summary>
    /// <param name="tree">The syntax tree being fixed.</param>
    /// <param name="style">The style the fix would emit.</param>
    /// <returns><see langword="true"/> when the replacement compiles in this file.</returns>
    /// <remarks>Only the pattern form is gated: it needs the <c>not</c> pattern, which is C# 9.</remarks>
    private static bool SupportsStyle(SyntaxTree tree, EmptyStringStyle style)
        => style != EmptyStringStyle.Pattern
            || ((CSharpParseOptions)tree.Options).LanguageVersion >= LanguageVersion.CSharp9;

    /// <summary>Gets the code action title naming the replacement.</summary>
    /// <param name="style">The style the fix will emit.</param>
    /// <returns>The title.</returns>
    private static string GetTitle(EmptyStringStyle style) => style switch
    {
        EmptyStringStyle.Length => "Use a length check",
        EmptyStringStyle.IsNullOrEmpty => "Use string.IsNullOrEmpty",
        _ => "Use a null-safe length pattern",
    };

    /// <summary>Builds the replacement for a reported comparison in one style.</summary>
    /// <param name="binary">The comparison expression to rewrite.</param>
    /// <param name="style">The style to emit.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(BinaryExpressionSyntax binary, EmptyStringStyle style, out ExpressionSyntax? replacement)
    {
        if (!Psh1204EmptyStringComparisonAnalyzer.TryGetOperands(binary, out _, out var value, out _))
        {
            replacement = null;
            return false;
        }

        var negated = binary.IsKind(SyntaxKind.NotEqualsExpression);
        var result = style switch
        {
            EmptyStringStyle.Length => BuildLengthTest(value!, negated),
            EmptyStringStyle.IsNullOrEmpty => BuildIsNullOrEmptyCall(value!, negated),
            _ => BuildPattern(binary, value!, negated),
        };

        // NormalizeWhitespace strips the elastic trivia the SyntaxFactory nodes carry; without it the
        // Roslyn 4.8 code-action formatter expands the property pattern onto multiple lines.
        replacement = result.NormalizeWhitespace().WithTriviaFrom(binary);
        return true;
    }

    /// <summary>Builds the <c>x is { Length: 0 }</c> form, parenthesized where its precedence needs it.</summary>
    /// <param name="binary">The comparison being replaced.</param>
    /// <param name="value">The string operand.</param>
    /// <param name="negated">Whether the comparison was <c>!=</c>.</param>
    /// <returns>The replacement expression.</returns>
    private static ExpressionSyntax BuildPattern(BinaryExpressionSyntax binary, ExpressionSyntax value, bool negated)
    {
        ExpressionSyntax result = SyntaxFactory.IsPatternExpression(
            value.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.IsKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space)),
            negated ? NotLengthZeroPattern : LengthZeroPattern);

        return NeedsParentheses(binary) ? SyntaxFactory.ParenthesizedExpression(result) : result;
    }

    /// <summary>Builds the <c>x.Length == 0</c> form.</summary>
    /// <param name="value">The string operand.</param>
    /// <param name="negated">Whether the comparison was <c>!=</c>.</param>
    /// <returns>The replacement expression.</returns>
    /// <remarks>
    /// The result is an equality expression replacing an equality expression, so it never needs
    /// parentheses of its own — but the operand does, whenever it binds looser than member access:
    /// <c>a + b == ""</c> must become <c>(a + b).Length == 0</c>, not <c>a + b.Length == 0</c>.
    /// </remarks>
    private static BinaryExpressionSyntax BuildLengthTest(ExpressionSyntax value, bool negated)
        => SyntaxFactory.BinaryExpression(
            negated ? SyntaxKind.NotEqualsExpression : SyntaxKind.EqualsExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                AsReceiver(value),
                SyntaxFactory.IdentifierName(nameof(string.Length))),
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));

    /// <summary>Builds the <c>string.IsNullOrEmpty(x)</c> form.</summary>
    /// <param name="value">The string operand.</param>
    /// <param name="negated">Whether the comparison was <c>!=</c>.</param>
    /// <returns>The replacement expression.</returns>
    /// <remarks>An argument takes any expression, and <c>!</c> binds tighter than everything around it, so neither side needs parentheses.</remarks>
    private static ExpressionSyntax BuildIsNullOrEmptyCall(ExpressionSyntax value, bool negated)
    {
        ExpressionSyntax call = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName(nameof(string.IsNullOrEmpty))),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(value.WithoutTrivia()))));

        return negated ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, call) : call;
    }

    /// <summary>Prepares an operand to sit to the left of <c>.Length</c>.</summary>
    /// <param name="value">The string operand.</param>
    /// <returns>The operand, parenthesized when member access would otherwise bind to only part of it.</returns>
    private static ExpressionSyntax AsReceiver(ExpressionSyntax value)
    {
        var stripped = value.WithoutTrivia();
        return IsPrimaryExpression(stripped) ? stripped : SyntaxFactory.ParenthesizedExpression(stripped);
    }

    /// <summary>Returns whether member access binds to the whole of an expression without parentheses.</summary>
    /// <param name="value">The string operand.</param>
    /// <returns><see langword="true"/> for the primary expressions <c>.Length</c> can follow directly.</returns>
    private static bool IsPrimaryExpression(ExpressionSyntax value) => value
        is IdentifierNameSyntax
        or MemberAccessExpressionSyntax
        or InvocationExpressionSyntax
        or ElementAccessExpressionSyntax
        or ParenthesizedExpressionSyntax
        or LiteralExpressionSyntax
        or ThisExpressionSyntax;

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
                            SyntaxFactory.IdentifierName(nameof(string.Length)),
                            SyntaxFactory.Token(default, SyntaxKind.ColonToken, SyntaxFactory.TriviaList(SyntaxFactory.Space))),
                        SyntaxFactory.ConstantPattern(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(0))))),
                SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.Space), SyntaxKind.CloseBraceToken, default)),
            designation: null);
}
