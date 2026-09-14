// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a constant or reused nonce passed to an AEAD encrypt call (SES1001). The rule reports the
/// nonce argument of <c>AesGcm.Encrypt</c>, <c>AesCcm.Encrypt</c>, and <c>ChaCha20Poly1305.Encrypt</c>
/// when that argument is a fixed value: an inline <c>new byte[N]</c> (an all-zero buffer that no
/// statement can write to before the call), an inline array of constant bytes, or a reference to a
/// <c>static readonly</c> field (allocated once and shared across every call). The three AEAD types
/// are resolved on demand for a syntactic candidate and cached per compilation, including when no
/// types resolve. A target framework without them never receives a diagnostic it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1001ConstantAeadNonceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the AEAD encryption method whose nonce argument is inspected.</summary>
    private const string EncryptMethodName = "Encrypt";

    /// <summary>The name of the nonce parameter on every AEAD <c>Encrypt</c> overload.</summary>
    private const string NonceParameterName = "nonce";

    /// <summary>The metadata names of the AEAD types whose <c>Encrypt</c> nonce is guarded.</summary>
    private static readonly string[] AeadMetadataNames =
    [
        "System.Security.Cryptography.AesGcm",
        "System.Security.Cryptography.AesCcm",
        "System.Security.Cryptography.ChaCha20Poly1305"
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.ConstantAeadNonce);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypeSet(compilation, AeadMetadataNames),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports SES1001 for an AEAD <c>Encrypt</c> call whose nonce argument is a fixed value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="aeadTypes">The AEAD types resolved on demand for the compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSet aeadTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetCandidateNonce(invocation) is not { } nonceArgument)
        {
            return;
        }

        var resolvedTypes = aeadTypes.Get();
        if (resolvedTypes.Length == 0
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: EncryptMethodName } method
            || !TypeRelations.IsOneOf(method.ContainingType, resolvedTypes)
            || !IsFixedNonce(context.SemanticModel, nonceArgument, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ConstantAeadNonce,
            nonceArgument.SyntaxTree,
            nonceArgument.Span,
            method.ContainingType.Name));
    }

    /// <summary>Finds a possible fixed nonce before resolving encryption types.</summary>
    /// <param name="invocation">The candidate Encrypt call.</param>
    /// <returns>The nonce expression when its syntax can represent a fixed value, or null.</returns>
    private static ExpressionSyntax? GetCandidateNonce(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: EncryptMethodName }
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        // The nonce is the first parameter of every AEAD Encrypt overload, so a leading positional argument is the nonce.
        var nonce = ArgumentLookup.Find(invocation.ArgumentList.Arguments, NonceParameterName, 0)?.Expression;
        return nonce is ArrayCreationExpressionSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax ? nonce : null;
    }

    /// <summary>Returns whether a nonce expression is a compile-time-fixed or shared-field value.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The nonce argument expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the nonce is a fixed value.</returns>
    private static bool IsFixedNonce(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken) =>
        expression switch
        {
            // An inline 'new byte[N]' has no writes before the call: no initializer means an all-zero
            // buffer, and an initializer is fixed only when every element is a compile-time constant.
            ArrayCreationExpressionSyntax arrayCreation =>
                arrayCreation.Initializer is null || InitializerConstants.AreAllConstant(model, arrayCreation.Initializer, cancellationToken),

            // A reference to a field that is allocated once and reused across every call.
            IdentifierNameSyntax or MemberAccessExpressionSyntax =>
                IsStaticReadonlyField(model, expression, cancellationToken),

            _ => false,
        };

    /// <summary>Returns whether an expression binds to a <c>static readonly</c> field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The nonce reference expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a static readonly field reference.</returns>
    private static bool IsStaticReadonlyField(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol { IsStatic: true, IsReadOnly: true };
}
