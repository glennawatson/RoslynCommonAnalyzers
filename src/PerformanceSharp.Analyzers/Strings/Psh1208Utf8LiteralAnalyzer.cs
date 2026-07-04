// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>Encoding.UTF8.GetBytes</c> and <c>Encoding.ASCII.GetBytes</c> calls on constant
/// strings (PSH1208), which re-encode and heap-allocate the same bytes on every call where a
/// u8 literal is encoded once at compile time. Reported only on C# 11+ trees, only when
/// <c>ReadOnlySpan&lt;byte&gt;</c> exists so the literal compiles, and only for constants a
/// u8 literal can represent — no unpaired surrogates, and ASCII-only characters when the
/// receiver is the ASCII encoding (whose replacement fallback would otherwise change bytes).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1208Utf8LiteralAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string GetBytesMethodName = "GetBytes";

    /// <summary>The UTF-8 encoding property name.</summary>
    internal const string Utf8PropertyName = "UTF8";

    /// <summary>The ASCII encoding property name.</summary>
    internal const string AsciiPropertyName = "ASCII";

    /// <summary>The metadata name of the encoding type.</summary>
    private const string EncodingMetadataName = "System.Text.Encoding";

    /// <summary>The metadata name of the span type a u8 literal produces.</summary>
    private const string ReadOnlySpanMetadataName = "System.ReadOnlySpan`1";

    /// <summary>The highest ASCII code point.</summary>
    private const char AsciiMax = (char)0x7F;

    /// <summary>The UTF-16 length of a surrogate pair.</summary>
    private const int SurrogatePairLength = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseUtf8Literal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var encodingType = start.Compilation.GetTypeByMetadataName(EncodingMetadataName);
            if (encodingType is null || start.Compilation.GetTypeByMetadataName(ReadOnlySpanMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, encodingType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns the encoding property name when an invocation has the <c>Encoding.UTF8.GetBytes(x)</c> shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The rightmost receiver name (UTF8 or ASCII), or <see langword="null"/> when the shape does not match.</returns>
    internal static SimpleNameSyntax? TryGetEncodingPropertyName(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != GetBytesMethodName)
        {
            return null;
        }

        var name = access.Expression switch
        {
            MemberAccessExpressionSyntax nested => nested.Name,
            IdentifierNameSyntax identifier => (SimpleNameSyntax)identifier,
            _ => null,
        };

        return name?.Identifier.ValueText is Utf8PropertyName or AsciiPropertyName ? name : null;
    }

    /// <summary>Returns whether a constant string can be written as an equivalent u8 literal.</summary>
    /// <param name="value">The constant string value.</param>
    /// <param name="asciiOnly">Whether the original encoding was ASCII, restricting the characters.</param>
    /// <returns><see langword="true"/> when a u8 literal produces the same bytes and compiles.</returns>
    internal static bool CanBecomeUtf8Literal(string value, bool asciiOnly)
    {
        for (var i = 0; i < value.Length; i += char.IsHighSurrogate(value[i]) ? SurrogatePairLength : 1)
        {
            var current = value[i];
            if ((asciiOnly && current > AsciiMax) || char.IsLowSurrogate(current))
            {
                return false;
            }

            if (char.IsHighSurrogate(current) && (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reports PSH1208 for a constant-string GetBytes call on the runtime's UTF-8 or ASCII encoding.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="encodingType">The encoding type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol encodingType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetEncodingPropertyName(invocation) is not { } encodingName
            || invocation.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp11 })
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        if (context.SemanticModel.GetConstantValue(argument, context.CancellationToken).Value is not string value
            || !CanBecomeUtf8Literal(value, encodingName.Identifier.ValueText == AsciiPropertyName)
            || context.SemanticModel.GetSymbolInfo(encodingName, context.CancellationToken).Symbol is not IPropertySymbol property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, encodingType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseUtf8Literal,
            invocation.SyntaxTree,
            invocation.Span));
    }
}
