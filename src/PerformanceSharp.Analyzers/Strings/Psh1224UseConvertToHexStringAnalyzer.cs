// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports hex built by hand out of <c>BitConverter.ToString</c> (PSH1224) —
/// <c>BitConverter.ToString(bytes).Replace("-", "")</c>. That builds the hyphen-separated form first,
/// then allocates a second string to take the hyphens back out, so a 32-byte hash costs two strings
/// and two full passes to produce one. <c>Convert.ToHexString</c> writes the answer once.
/// </summary>
/// <remarks>
/// <para>
/// The two produce the same text: both emit upper-case hex, two characters per byte, and both throw
/// <see cref="ArgumentNullException"/> on a null array. The offset-and-count form lines up as well —
/// <c>BitConverter.ToString(bytes, offset, length)</c> against
/// <c>Convert.ToHexString(bytes, offset, length)</c>. A case conversion chained after the call is left
/// exactly where it is, so <c>...Replace("-", "").ToLowerInvariant()</c> keeps its lower-casing and
/// simply gets a cheaper string to lower-case.
/// </para>
/// <para>
/// <c>Convert.ToHexString</c> arrived in .NET 5, so it is resolved in the analyzed compilation and the
/// rule registers nothing when it is absent — on netstandard2.0 and .NET Framework the diagnostic
/// would name an API the author cannot call.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1224UseConvertToHexStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The member name that strips the separators.</summary>
    internal const string ReplaceMethodName = "Replace";

    /// <summary>The member name that produces the separated hex.</summary>
    internal const string ToStringMethodName = "ToString";

    /// <summary>The replacement member name.</summary>
    internal const string ToHexStringMethodName = "ToHexString";

    /// <summary>The simple name of the type providing the one-call conversion.</summary>
    internal const string ConvertTypeName = "Convert";

    /// <summary>The spelling of the <c>Convert</c> type that always resolves, used to prove the rewrite binds.</summary>
    internal const string QualifiedConvert = "global::System.Convert";

    /// <summary>The display used for the suggested call in the message.</summary>
    private const string ToHexStringDisplay = "Convert.ToHexString";

    /// <summary>The metadata name of the type providing the one-call conversion.</summary>
    private const string ConvertMetadataName = "System.Convert";

    /// <summary>The metadata name of the type providing the separated hex.</summary>
    private const string BitConverterTypeName = "BitConverter";

    /// <summary>The separator the hand-rolled form strips back out.</summary>
    private const string HyphenSeparator = "-";

    /// <summary>The argument count of the two-string <c>string.Replace</c>.</summary>
    private const int ReplaceArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseConvertToHexString);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (!HasToHexString(start.Compilation))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeReplace, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Splits a <c>BitConverter.ToString(...).Replace("-", "")</c> chain, syntactically.</summary>
    /// <param name="invocation">The candidate <c>Replace</c> invocation.</param>
    /// <returns>The inner <c>ToString</c> call, or <see langword="null"/> when the shape does not match.</returns>
    internal static InvocationExpressionSyntax? TryGetSeparatedHexCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != ReplaceArgumentCount
            || invocation.Expression is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            || access.Name.Identifier.ValueText != ReplaceMethodName)
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (!IsHyphenLiteral(arguments[0].Expression) || !IsEmptyStringExpression(arguments[1].Expression))
        {
            return null;
        }

        return IsSeparatedHexShape(access.Expression) ? (InvocationExpressionSyntax)access.Expression : null;
    }

    /// <summary>Builds the <c>Convert.ToHexString(...)</c> rewrite, reusing the original arguments.</summary>
    /// <param name="separatedHex">The inner <c>BitConverter.ToString</c> call.</param>
    /// <param name="convertSpelling">The spelling of the <c>Convert</c> type to emit.</param>
    /// <returns>The rewritten call.</returns>
    internal static InvocationExpressionSyntax BuildHexCall(InvocationExpressionSyntax separatedHex, string convertSpelling)
        => SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression(convertSpelling),
                SyntaxFactory.IdentifierName(ToHexStringMethodName)),
            separatedHex.ArgumentList.WithoutTrivia());

    /// <summary>Confirms the rewrite binds to <c>Convert.ToHexString</c> and still returns a string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The reported expression's position.</param>
    /// <param name="rewritten">The rewritten call.</param>
    /// <returns><see langword="true"/> when the fix compiles.</returns>
    internal static bool RewriteBindsToHexString(SemanticModel model, int position, InvocationExpressionSyntax rewritten)
        => model.GetSpeculativeSymbolInfo(position, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: ToHexStringMethodName,
            ReturnType.SpecialType: SpecialType.System_String,
            ContainingType: { Name: ConvertTypeName, ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } },
        };

    /// <summary>Returns whether an expression is a plain <c>X.ToString(...)</c> call.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    private static bool IsSeparatedHexShape(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } inner
            && inner.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } innerAccess
            && innerAccess.Name.Identifier.ValueText == ToStringMethodName;

    /// <summary>Reports PSH1224 for hex assembled from the separated form and then stripped.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeReplace(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetSeparatedHexCall(invocation) is not { } separatedHex)
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (!IsStringReplace(model, invocation, cancellationToken)
            || !IsBitConverterToString(model, separatedHex, cancellationToken)
            || SpanRewriteGuard.IsInsideExpressionTree(invocation, model, cancellationToken)
            || !RewriteBindsToHexString(model, invocation.SpanStart, BuildHexCall(separatedHex, QualifiedConvert)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseConvertToHexString,
            invocation.SyntaxTree,
            invocation.Span,
            ToHexStringDisplay));
    }

    /// <summary>Returns whether <see cref="Convert"/> declares the one-call hex conversion.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when <c>Convert.ToHexString(byte[])</c> exists.</returns>
    private static bool HasToHexString(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(ConvertMetadataName) is not { } convert)
        {
            return false;
        }

        foreach (var member in convert.GetMembers(ToHexStringMethodName))
        {
            if (member is IMethodSymbol
                {
                    IsStatic: true,
                    ReturnType.SpecialType: SpecialType.System_String,
                    Parameters: [{ Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } }, ..],
                })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an expression is the string literal <c>"-"</c>.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for the hyphen literal.</returns>
    private static bool IsHyphenLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
            && (string?)literal.Token.Value == HyphenSeparator;

    /// <summary>Returns whether an expression is the empty string, however it is spelled.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for <c>""</c> or <c>string.Empty</c>.</returns>
    private static bool IsEmptyStringExpression(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal)
        {
            return literal.Token.Value is string value && value.Length == 0;
        }

        return expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == nameof(string.Empty);
    }

    /// <summary>Returns whether the outer call is the framework's two-string <c>string.Replace</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The <c>Replace</c> invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is the expected replace.</returns>
    private static bool IsStringReplace(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: false,
            Name: ReplaceMethodName,
            ContainingType.SpecialType: SpecialType.System_String,
            Parameters: [{ Type.SpecialType: SpecialType.System_String }, { Type.SpecialType: SpecialType.System_String }],
        };

    /// <summary>Returns whether the inner call is <c>BitConverter.ToString</c> over a byte array.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="separatedHex">The inner invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is the expected separated-hex builder.</returns>
    private static bool IsBitConverterToString(SemanticModel model, InvocationExpressionSyntax separatedHex, CancellationToken cancellationToken)
        => model.GetSymbolInfo(separatedHex, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: ToStringMethodName,
            ReturnType.SpecialType: SpecialType.System_String,
            Parameters: [{ Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } }, ..],
            ContainingType:
            {
                Name: BitConverterTypeName,
                ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
            },
        };
}
