// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Rewrites a reported constant-string <c>GetBytes</c> call to a u8 literal (PSH1208). When
/// the call's converted type is <c>ReadOnlySpan&lt;byte&gt;</c> the literal stands alone;
/// otherwise the call produced a <c>byte[]</c> and the literal gets a <c>ToArray()</c> so the
/// result type is unchanged (still one allocation cheaper than re-encoding). A string literal
/// argument keeps its original escapes; other constants are re-escaped from the value.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1208Utf8LiteralCodeFixProvider))]
[Shared]
public sealed class Psh1208Utf8LiteralCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The suffix that turns a string literal into a UTF-8 literal.</summary>
    private const string Utf8Suffix = "u8";

    /// <summary>The method that copies a span into a fresh array.</summary>
    private const string ToArrayMethodName = "ToArray";

    /// <summary>The simple name of the span type u8 literals produce.</summary>
    private const string ReadOnlySpanTypeName = "ReadOnlySpan";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(StringRules.UseUtf8Literal.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Use a u8 literal", nameof(Psh1208Utf8LiteralCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported invocation and builds its u8 replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, SemanticModel model, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || Psh1208Utf8LiteralAnalyzer.TryGetEncodingPropertyName(invocation) is not { } encodingName)
        {
            return null;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        var asciiOnly = encodingName.Identifier.ValueText == Psh1208Utf8LiteralAnalyzer.AsciiPropertyName;
        if (model.GetConstantValue(argument).Value is not string value
            || !Psh1208Utf8LiteralAnalyzer.CanBecomeUtf8Literal(value, asciiOnly))
        {
            return null;
        }

        var literal = SyntaxFactory.ParseExpression(BuildLiteralText(argument, value));
        var replacement = IsConvertedToReadOnlySpan(model, invocation)
            ? literal
            : SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    literal,
                    SyntaxFactory.IdentifierName(ToArrayMethodName)));

        return new NodeReplacement(invocation, replacement.WithTriviaFrom(invocation));
    }

    /// <summary>Builds the u8 literal text, keeping a literal argument's original escapes.</summary>
    /// <param name="argument">The constant argument expression.</param>
    /// <param name="value">The constant string value.</param>
    /// <returns>The literal text including the u8 suffix.</returns>
    private static string BuildLiteralText(ExpressionSyntax argument, string value)
        => argument is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            ? literal.Token.Text + Utf8Suffix
            : SymbolDisplay.FormatLiteral(value, quote: true) + Utf8Suffix;

    /// <summary>Returns whether the invocation's converted type is <c>ReadOnlySpan&lt;byte&gt;</c>.</summary>
    /// <param name="model">The semantic model for the document.</param>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns><see langword="true"/> when the u8 literal can stand alone without ToArray.</returns>
    private static bool IsConvertedToReadOnlySpan(SemanticModel model, InvocationExpressionSyntax invocation)
        => model.GetTypeInfo(invocation).ConvertedType is INamedTypeSymbol
        {
            Name: ReadOnlySpanTypeName,
            IsGenericType: true,
            TypeArguments: [{ SpecialType: SpecialType.System_Byte }],
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };
}
