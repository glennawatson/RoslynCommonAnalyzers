// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags equality comparisons between <c>Encoding.UTF8.GetString(bytes)</c> and a constant
/// string (PSH1210), which allocate a decoded string only to throw it away, where
/// <c>SequenceEqual</c> against a u8 literal compares the raw bytes with no allocation.
/// Reported only on C# 11+ trees, only when <c>MemoryExtensions.SequenceEqual</c> exists, and
/// only for constants a u8 literal can represent whose decode comparison is byte-exact — a
/// constant containing U+FFFD could equal a decode of different (invalid) bytes, so those are
/// skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1210Utf8SequenceEqualAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string GetStringMethodName = "GetString";

    /// <summary>The UTF-8 encoding property name the syntax gate requires.</summary>
    internal const string Utf8PropertyName = "UTF8";

    /// <summary>The metadata name of the encoding type.</summary>
    private const string EncodingMetadataName = "System.Text.Encoding";

    /// <summary>The metadata name of the span extensions type providing SequenceEqual.</summary>
    private const string MemoryExtensionsMetadataName = "System.MemoryExtensions";

    /// <summary>The replacement method name.</summary>
    private const string SequenceEqualMethodName = "SequenceEqual";

    /// <summary>The replacement character invalid UTF-8 decodes to.</summary>
    private const char ReplacementCharacter = '�';

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseUtf8SequenceEqual);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var encodingType = start.Compilation.GetTypeByMetadataName(EncodingMetadataName);
            if (encodingType is null
                || start.Compilation.GetTypeByMetadataName(MemoryExtensionsMetadataName) is not { } extensionsType
                || extensionsType.GetMembers(SequenceEqualMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeComparison(nodeContext, encodingType),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        });
    }

    /// <summary>Returns the <c>Encoding.UTF8.GetString(x)</c> invocation of a comparison, before any binding.</summary>
    /// <param name="binary">The comparison to inspect.</param>
    /// <returns>The decode invocation and the other operand, or <see langword="null"/> when neither side matches the shape.</returns>
    internal static (InvocationExpressionSyntax Decode, ExpressionSyntax Constant)? TryGetComparisonParts(BinaryExpressionSyntax binary)
    {
        if (TryGetGetStringInvocation(binary.Left) is { } leftDecode)
        {
            return (leftDecode, binary.Right);
        }

        return TryGetGetStringInvocation(binary.Right) is { } rightDecode
            ? (rightDecode, binary.Left)
            : null;
    }

    /// <summary>Returns whether a constant string compares byte-exactly as a u8 literal.</summary>
    /// <param name="value">The constant string value.</param>
    /// <returns><see langword="true"/> when the literal compiles and cannot alias an invalid decode.</returns>
    internal static bool CanCompareAsUtf8Literal(string value)
        => value.IndexOf(ReplacementCharacter) < 0
            && Psh1208Utf8LiteralAnalyzer.CanBecomeUtf8Literal(value, asciiOnly: false);

    /// <summary>Returns an expression's <c>Encoding.UTF8.GetString(x)</c> invocation shape match.</summary>
    /// <param name="expression">The comparison operand to inspect.</param>
    /// <returns>The invocation, or <see langword="null"/> when the shape does not match.</returns>
    private static InvocationExpressionSyntax? TryGetGetStringInvocation(ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != GetStringMethodName)
        {
            return null;
        }

        var name = access.Expression switch
        {
            MemberAccessExpressionSyntax nested => nested.Name,
            IdentifierNameSyntax identifier => (SimpleNameSyntax)identifier,
            _ => null,
        };

        return name?.Identifier.ValueText == Utf8PropertyName ? invocation : null;
    }

    /// <summary>Reports PSH1210 for a decode-then-compare against a representable constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="encodingType">The encoding type.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context, INamedTypeSymbol encodingType)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (TryGetComparisonParts(binary) is not { } parts
            || binary.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp11 })
        {
            return;
        }

        var encodingName = ((MemberAccessExpressionSyntax)parts.Decode.Expression).Expression;
        if (context.SemanticModel.GetConstantValue(parts.Constant, context.CancellationToken).Value is not string value
            || !CanCompareAsUtf8Literal(value)
            || context.SemanticModel.GetSymbolInfo(encodingName, context.CancellationToken).Symbol is not IPropertySymbol property
            || property.Name != Utf8PropertyName
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, encodingType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseUtf8SequenceEqual,
            binary.SyntaxTree,
            binary.Span,
            GetStringMethodName));
    }
}
