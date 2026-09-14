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

    /// <summary>The highest ASCII code point.</summary>
    private const char AsciiMax = (char)0x7F;

    /// <summary>The UTF-16 length of a surrogate pair.</summary>
    private const int SurrogatePairLength = 2;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(StringRules.UseUtf8Literal);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveEncodingType),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
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
    /// <param name="encodingTypes">The lazily resolved encoding and span types.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> encodingTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetEncodingPropertyName(invocation) is not { } encodingName
            || invocation.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp11 }
            || encodingTypes.Get() is not { } encodingType)
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

    /// <summary>Resolves UTF-8 literal support after a call passes the syntax and language filters.</summary>
    /// <param name="compilation">The compilation whose encoding and span types are probed.</param>
    /// <returns>The encoding type when the span type a u8 literal produces also exists; otherwise <see langword="null"/>.</returns>
    private static INamedTypeSymbol? ResolveEncodingType(Compilation compilation) =>
        compilation.GetTypeByMetadataName("System.ReadOnlySpan`1") is null
            ? null
            : compilation.GetTypeByMetadataName("System.Text.Encoding");
}
