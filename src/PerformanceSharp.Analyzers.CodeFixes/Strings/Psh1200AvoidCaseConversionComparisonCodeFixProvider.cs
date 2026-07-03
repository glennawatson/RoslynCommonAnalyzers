// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a compare-after-case-conversion expression to
/// <c>string.Equals(a, b, System.StringComparison.X)</c> (PSH1200), dropping both
/// conversion calls. <c>ToLower</c>/<c>ToUpper</c> map to <c>OrdinalIgnoreCase</c>;
/// the invariant variants map to <c>InvariantCultureIgnoreCase</c>; <c>!=</c> becomes
/// <c>!string.Equals(...)</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1200AvoidCaseConversionComparisonCodeFixProvider))]
[Shared]
public sealed class Psh1200AvoidCaseConversionComparisonCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The fully-qualified ordinal-ignore-case comparison syntax reused across fixes.</summary>
    private static readonly ExpressionSyntax OrdinalIgnoreCaseSyntax = SyntaxFactory.ParseExpression("System.StringComparison.OrdinalIgnoreCase");

    /// <summary>The fully-qualified invariant-culture-ignore-case comparison syntax reused across fixes.</summary>
    private static readonly ExpressionSyntax InvariantCultureIgnoreCaseSyntax = SyntaxFactory.ParseExpression("System.StringComparison.InvariantCultureIgnoreCase");

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.AvoidCaseConversionComparison.Id);

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
            if (!TryGetTarget(root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true), out var target))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use string.Equals with a StringComparison",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, target!)),
                    equivalenceKey: nameof(Psh1200AvoidCaseConversionComparisonCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetTarget(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true), out var target)
            || !TryGetReplacement(target!, out var replacement))
        {
            return;
        }

        editor.ReplaceNode(target!, replacement!);
    }

    /// <summary>Replaces the reported comparison with its <c>string.Equals</c> form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The comparison expression to rewrite (binary or <c>Equals</c> invocation).</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, ExpressionSyntax comparison)
        => TryGetReplacement(comparison, out var replacement)
            ? document.WithSyntaxRoot(root.ReplaceNode(comparison, replacement!))
            : document;

    /// <summary>Resolves the diagnostic's node to the comparison expression that gets replaced.</summary>
    /// <param name="node">The node found at the diagnostic location.</param>
    /// <param name="target">The comparison expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a rewrite target was found.</returns>
    private static bool TryGetTarget(SyntaxNode node, out ExpressionSyntax? target)
    {
        if (node is BinaryExpressionSyntax binary)
        {
            target = binary;
            return true;
        }

        if (node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { } invocation)
        {
            target = invocation;
            return true;
        }

        target = null;
        return false;
    }

    /// <summary>Builds the <c>string.Equals</c> replacement for a reported comparison expression.</summary>
    /// <param name="comparison">The comparison expression to rewrite.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetReplacement(ExpressionSyntax comparison, out ExpressionSyntax? replacement)
    {
        if (comparison is BinaryExpressionSyntax binary)
        {
            return TryGetBinaryReplacement(binary, out replacement);
        }

        if (comparison is InvocationExpressionSyntax invocation)
        {
            return TryGetEqualsReplacement(invocation, out replacement);
        }

        replacement = null;
        return false;
    }

    /// <summary>Builds the replacement for the <c>==</c>/<c>!=</c> shape.</summary>
    /// <param name="binary">The reported binary expression.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetBinaryReplacement(BinaryExpressionSyntax binary, out ExpressionSyntax? replacement)
    {
        if (!Psh1200AvoidCaseConversionComparisonAnalyzer.TryGetCaseConversion(binary.Left, out var left, out var leftName)
            || !Psh1200AvoidCaseConversionComparisonAnalyzer.TryGetCaseConversion(binary.Right, out var right, out var rightName)
            || !string.Equals(leftName, rightName, StringComparison.Ordinal))
        {
            replacement = null;
            return false;
        }

        ExpressionSyntax equalsCall = BuildStringEquals(GetConversionReceiver(left!), GetConversionReceiver(right!), leftName!);
        if (binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            equalsCall = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, equalsCall);
        }

        replacement = equalsCall.WithTriviaFrom(binary);
        return true;
    }

    /// <summary>Builds the replacement for the instance and static <c>Equals</c> shapes.</summary>
    /// <param name="invocation">The reported <c>Equals</c> invocation.</param>
    /// <param name="replacement">The replacement expression when the shape is recognized.</param>
    /// <returns><see langword="true"/> when a replacement was built.</returns>
    private static bool TryGetEqualsReplacement(InvocationExpressionSyntax invocation, out ExpressionSyntax? replacement)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax access
            && Psh1200AvoidCaseConversionComparisonAnalyzer.TryGetEqualsOperands(invocation, access, out var left, out var right, out var methodName))
        {
            replacement = BuildStringEquals(GetConversionReceiver(left!), GetConversionReceiver(right!), methodName!)
                .WithTriviaFrom(invocation);
            return true;
        }

        replacement = null;
        return false;
    }

    /// <summary>Returns the unconverted receiver of a case-conversion invocation.</summary>
    /// <param name="conversion">The conversion invocation (for example <c>a.ToLower()</c>).</param>
    /// <returns>The receiver expression (for example <c>a</c>).</returns>
    private static ExpressionSyntax GetConversionReceiver(InvocationExpressionSyntax conversion)
        => ((MemberAccessExpressionSyntax)conversion.Expression).Expression;

    /// <summary>Builds <c>string.Equals(left, right, System.StringComparison.X)</c> for a conversion method.</summary>
    /// <param name="left">The left unconverted operand.</param>
    /// <param name="right">The right unconverted operand.</param>
    /// <param name="conversionName">The conversion method name that selects the comparison.</param>
    /// <returns>The built invocation.</returns>
    private static InvocationExpressionSyntax BuildStringEquals(ExpressionSyntax left, ExpressionSyntax right, string conversionName)
    {
        var comparison = conversionName.EndsWith("Invariant", StringComparison.Ordinal)
            ? InvariantCultureIgnoreCaseSyntax
            : OrdinalIgnoreCaseSyntax;

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

    /// <summary>Creates a comma token followed by a single space.</summary>
    /// <returns>The comma token.</returns>
    private static SyntaxToken CommaWithTrailingSpace()
        => SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));
}
