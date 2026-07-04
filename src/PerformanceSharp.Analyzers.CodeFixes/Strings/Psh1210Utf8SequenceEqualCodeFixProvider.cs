// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported decode-then-compare to a byte comparison (PSH1210):
/// <c>Encoding.UTF8.GetString(x) == "lit"</c> becomes <c>x.SequenceEqual("lit"u8)</c>, with an
/// <c>AsSpan()</c> inserted when the byte source is an array so the span extension binds on
/// every language version. An inequality gains a leading negation. When the System namespace
/// is not imported the fix calls <c>MemoryExtensions.SequenceEqual</c> fully qualified, which
/// accepts the array directly.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1210Utf8SequenceEqualCodeFixProvider))]
[Shared]
public sealed class Psh1210Utf8SequenceEqualCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The suffix that turns a string literal into a UTF-8 literal.</summary>
    private const string Utf8Suffix = "u8";

    /// <summary>The replacement method name.</summary>
    private const string SequenceEqualMethodName = "SequenceEqual";

    /// <summary>The array-to-span extension method name.</summary>
    private const string AsSpanMethodName = "AsSpan";

    /// <summary>The simple name of the span extensions type.</summary>
    private const string MemoryExtensionsTypeName = "MemoryExtensions";

    /// <summary>The fully qualified spelling used when the simple name does not resolve.</summary>
    private const string QualifiedMemoryExtensionsExpression = "global::System.MemoryExtensions";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseUtf8SequenceEqual.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Compare the bytes with SequenceEqual", nameof(Psh1210Utf8SequenceEqualCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported comparison and builds its byte-comparison replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not BinaryExpressionSyntax binary
            || Psh1210Utf8SequenceEqualAnalyzer.TryGetComparisonParts(binary) is not { } parts
            || model.GetConstantValue(parts.Constant).Value is not string value
            || !Psh1210Utf8SequenceEqualAnalyzer.CanCompareAsUtf8Literal(value))
        {
            return null;
        }

        var bytesSource = parts.Decode.ArgumentList.Arguments[0].Expression;
        var literal = SyntaxFactory.ParseExpression(BuildLiteralText(parts.Constant, value));
        var isArray = model.GetTypeInfo(bytesSource).Type is IArrayTypeSymbol;
        var comparison = BuildSequenceEqual(model, binary, bytesSource.WithoutTrivia(), literal, isArray);

        ExpressionSyntax replacement = binary.IsKind(SyntaxKind.NotEqualsExpression)
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, comparison)
            : comparison;

        return new NodeReplacement(binary, replacement.WithTriviaFrom(binary));
    }

    /// <summary>Builds the SequenceEqual call, extension-style when the System import makes it bind.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="binary">The reported comparison anchoring the lookup.</param>
    /// <param name="bytes">The byte-source expression.</param>
    /// <param name="literal">The u8 literal expression.</param>
    /// <param name="isArray">Whether the byte source is an array needing AsSpan for the extension form.</param>
    /// <returns>The comparison invocation.</returns>
    private static InvocationExpressionSyntax BuildSequenceEqual(
        SemanticModel model,
        BinaryExpressionSyntax binary,
        ExpressionSyntax bytes,
        ExpressionSyntax literal,
        bool isArray)
    {
        if (!ResolvesMemoryExtensions(model, binary.SpanStart))
        {
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression(QualifiedMemoryExtensionsExpression),
                    SyntaxFactory.IdentifierName(SequenceEqualMethodName)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                    ImmutableArrays.Of(SyntaxFactory.Argument(bytes), SyntaxFactory.Argument(literal)))));
        }

        var receiver = Parenthesize(bytes);
        if (isArray)
        {
            receiver = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    receiver,
                    SyntaxFactory.IdentifierName(AsSpanMethodName)));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName(SequenceEqualMethodName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(literal))));
    }

    /// <summary>Builds the u8 literal text, keeping a literal operand's original escapes.</summary>
    /// <param name="constant">The constant operand expression.</param>
    /// <param name="value">The constant string value.</param>
    /// <returns>The literal text including the u8 suffix.</returns>
    private static string BuildLiteralText(ExpressionSyntax constant, string value)
        => constant is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            ? literal.Token.Text + Utf8Suffix
            : SymbolDisplay.FormatLiteral(value, quote: true) + Utf8Suffix;

    /// <summary>Returns whether the span extensions type resolves by simple name at a position.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when extension-method syntax binds.</returns>
    private static bool ResolvesMemoryExtensions(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: MemoryExtensionsTypeName))
        {
            if (candidate is INamedTypeSymbol { ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Wraps an expression in parentheses when using it as a member-access receiver could reparse.</summary>
    /// <param name="expression">The expression to protect.</param>
    /// <returns>The original or a parenthesized copy.</returns>
    private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax
            ? expression
            : SyntaxFactory.ParenthesizedExpression(expression);
}
