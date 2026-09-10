// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Reports the AES key-wrap integrity check value appearing in source, whether written as one 64-bit
/// constant or as the eight bytes it is made of, where the platform primitive exists (SES1010).
/// </summary>
/// <remarks>
/// The constant is the detection: it has exactly one meaning, so code carrying it is implementing key
/// wrapping rather than calling it. That keeps the rule precise without having to recognize any
/// particular shape of hand-written implementation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1010HandRolledKeyWrapAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the symmetric algorithm that offers key wrapping.</summary>
    private const string AesMetadataName = "System.Security.Cryptography.Aes";

    /// <summary>The platform key-wrap entry point this rule points at.</summary>
    private const string EncryptKeyWrapName = "EncryptKeyWrap";

    /// <summary>The single byte the integrity check value repeats.</summary>
    private const int KeyWrapIntegrityCheckByte = 0xA6;

    /// <summary>How many bytes the integrity check value occupies.</summary>
    private const int KeyWrapIntegrityCheckLength = 8;

    /// <summary>How far to shift when packing one more byte into the value.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The default initial value AES key wrapping prepends to the key.</summary>
    /// <remarks>Built from its parts rather than written as one literal, which is what it is: one byte, repeated eight times.</remarks>
    private static readonly ulong KeyWrapIntegrityCheckValue = BuildIntegrityCheckValue();

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(SecurityRules.HandRolledKeyWrap);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var aes = start.Compilation.GetTypeByMetadataName(AesMetadataName);
            if (aes is null || aes.GetMembers(EncryptKeyWrapName).Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.NumericLiteralExpression);
            start.RegisterSyntaxNodeAction(AnalyzeInitializer, SyntaxKind.ArrayInitializerExpression);
        });
    }

    /// <summary>Reports the integrity check value written as a single constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (!IsIntegrityCheckValue(context.SemanticModel.GetConstantValue(literal, context.CancellationToken)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.HandRolledKeyWrap, literal.GetLocation()));
    }

    /// <summary>Reports the integrity check value written as its eight bytes.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInitializer(SyntaxNodeAnalysisContext context)
    {
        var initializer = (InitializerExpressionSyntax)context.Node;
        if (initializer.Expressions.Count != KeyWrapIntegrityCheckLength)
        {
            return;
        }

        foreach (var expression in initializer.Expressions)
        {
            var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
            if (!IsIntegrityCheckByte(constant))
            {
                return;
            }
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.HandRolledKeyWrap, initializer.GetLocation()));
    }

    /// <summary>Builds the integrity check value by repeating its single byte.</summary>
    /// <returns>The 64-bit initial value.</returns>
    private static ulong BuildIntegrityCheckValue()
    {
        var value = 0UL;
        for (var i = 0; i < KeyWrapIntegrityCheckLength; i++)
        {
            value = (value << BitsPerByte) | KeyWrapIntegrityCheckByte;
        }

        return value;
    }

    /// <summary>Gets whether a constant is the 64-bit integrity check value.</summary>
    /// <param name="constant">The bound constant.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    private static bool IsIntegrityCheckValue(Optional<object?> constant)
        => constant is { HasValue: true, Value: { } value }
            && value switch
            {
                ulong unsigned => unsigned == KeyWrapIntegrityCheckValue,
                long signed => unchecked((ulong)signed) == KeyWrapIntegrityCheckValue,
                _ => false,
            };

    /// <summary>Gets whether a constant is the repeated integrity check byte.</summary>
    /// <param name="constant">The bound constant.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    private static bool IsIntegrityCheckByte(Optional<object?> constant)
        => constant is { HasValue: true, Value: { } value }
            && value switch
            {
                byte singleByte => singleByte == KeyWrapIntegrityCheckByte,
                int number => number == KeyWrapIntegrityCheckByte,
                _ => false,
            };
}
