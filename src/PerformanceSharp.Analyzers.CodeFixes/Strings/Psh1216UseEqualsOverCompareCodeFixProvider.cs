// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites an equality test of a string ordering call against zero to
/// <c>string.Equals</c> with the comparison the call already used (PSH1216). The
/// two-argument <c>string.Compare</c> and instance <c>CompareTo</c> map to
/// <c>StringComparison.CurrentCulture</c> — their actual default, not ordinal — an
/// explicit <c>StringComparison</c> argument is carried over unchanged, a literal
/// <c>ignoreCase</c> flag maps to <c>CurrentCultureIgnoreCase</c>/<c>CurrentCulture</c>,
/// and <c>string.CompareOrdinal</c> maps to <c>StringComparison.Ordinal</c>;
/// <c>!=</c> becomes <c>!string.Equals(...)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1216UseEqualsOverCompareCodeFixProvider))]
[Shared]
public sealed class Psh1216UseEqualsOverCompareCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The argument count of the static <c>Compare</c> shape that defaults to the current culture.</summary>
    private const int CultureDefaultCompareArgumentCount = 2;

    /// <summary>The index of the option argument in the three-argument <c>Compare</c> shape.</summary>
    private const int OptionArgumentIndex = 2;

    /// <summary>The fully-qualified current-culture comparison syntax reused across fixes.</summary>
    private static readonly ExpressionSyntax CurrentCultureSyntax = SyntaxFactory.ParseExpression("System.StringComparison.CurrentCulture");

    /// <summary>The fully-qualified current-culture-ignore-case comparison syntax reused across fixes.</summary>
    private static readonly ExpressionSyntax CurrentCultureIgnoreCaseSyntax = SyntaxFactory.ParseExpression("System.StringComparison.CurrentCultureIgnoreCase");

    /// <summary>The fully-qualified ordinal comparison syntax reused across fixes.</summary>
    private static readonly ExpressionSyntax OrdinalSyntax = SyntaxFactory.ParseExpression("System.StringComparison.Ordinal");

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseEqualsOverCompare.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use string.Equals with the matching StringComparison", nameof(Psh1216UseEqualsOverCompareCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Replaces the reported comparison with its <c>string.Equals</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison expression to rewrite.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
        => TryGetReplacement(comparison, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement!))
            : document;

    /// <summary>Resolves the reported comparison and builds its <c>string.Equals</c> replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BinaryExpressionSyntax binary
            && TryGetReplacement(binary, out var replacement)
            ? new NodeReplacement(binary, replacement!)
            : null;

    /// <summary>Builds the <c>string.Equals</c> replacement for a reported comparison.</summary>
    /// <param name="binary">The comparison expression to rewrite.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(BinaryExpressionSyntax binary, out ExpressionSyntax? replacement)
    {
        if (!Psh1216UseEqualsOverCompareAnalyzer.TryGetOrderingCall(binary, out var invocation, out var methodName))
        {
            replacement = null;
            return false;
        }

        ExpressionSyntax equalsCall = BuildStringEquals(invocation!, methodName!);
        if (binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            equalsCall = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, equalsCall);
        }

        replacement = equalsCall.WithTriviaFrom(binary);
        return true;
    }

    /// <summary>Builds <c>string.Equals(left, right, comparison)</c> for a reported ordering call.</summary>
    /// <param name="invocation">The ordering invocation being replaced.</param>
    /// <param name="methodName">The invoked member name selecting the operand and comparison mapping.</param>
    /// <returns>The built invocation.</returns>
    private static InvocationExpressionSyntax BuildStringEquals(InvocationExpressionSyntax invocation, string methodName)
    {
        var arguments = invocation.ArgumentList.Arguments;
        ExpressionSyntax left;
        ExpressionSyntax right;
        ExpressionSyntax comparison;
        switch (methodName)
        {
            case Psh1216UseEqualsOverCompareAnalyzer.CompareToName:
            {
                left = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
                right = arguments[0].Expression;
                comparison = CurrentCultureSyntax;
                break;
            }

            case Psh1216UseEqualsOverCompareAnalyzer.CompareOrdinalName:
            {
                left = arguments[0].Expression;
                right = arguments[1].Expression;
                comparison = OrdinalSyntax;
                break;
            }

            default:
            {
                left = arguments[0].Expression;
                right = arguments[1].Expression;
                comparison = arguments.Count == CultureDefaultCompareArgumentCount
                    ? CurrentCultureSyntax
                    : GetOptionComparison(arguments[OptionArgumentIndex].Expression);
                break;
            }
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName(nameof(string.Equals))),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(new SyntaxNodeOrToken[]
            {
                SyntaxFactory.Argument(left.WithoutTrivia()),
                CommaWithTrailingSpace(),
                SyntaxFactory.Argument(right.WithoutTrivia()),
                CommaWithTrailingSpace(),
                SyntaxFactory.Argument(comparison)
            })));
    }

    /// <summary>Maps a three-argument <c>Compare</c> option to the comparison argument the fix emits.</summary>
    /// <param name="option">The third argument: a literal <c>ignoreCase</c> flag or a <c>StringComparison</c> expression.</param>
    /// <returns>The comparison expression preserving the original call's semantics.</returns>
    private static ExpressionSyntax GetOptionComparison(ExpressionSyntax option)
    {
        if (option.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return CurrentCultureIgnoreCaseSyntax;
        }

        return option.IsKind(SyntaxKind.FalseLiteralExpression) ? CurrentCultureSyntax : option.WithoutTrivia();
    }

    /// <summary>Creates a comma token followed by a single space.</summary>
    /// <returns>The comma token.</returns>
    private static SyntaxToken CommaWithTrailingSpace()
        => SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
}
