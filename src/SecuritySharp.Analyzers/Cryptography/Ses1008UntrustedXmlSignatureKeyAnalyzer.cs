// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an XML digital signature that is verified without a caller-supplied key (SES1008). The rule
/// reports a <c>SignedXml.CheckSignature()</c> or <c>CheckSignature(bool)</c> call -- the overloads that take
/// no key and therefore trust whatever public key or certificate is embedded in the document's own
/// <c>KeyInfo</c> element, letting an attacker re-sign tampered XML with their own key and still pass. The
/// key-bearing overloads (<c>CheckSignature(AsymmetricAlgorithm)</c>, <c>CheckSignature(KeyedHashAlgorithm)</c>,
/// and <c>CheckSignature(X509Certificate2, bool)</c>) verify against a known key and are never reported; the
/// <c>CheckSignatureReturning*</c> methods hand the signing key or certificate back for the caller to
/// validate and carry a different member name, so the syntactic prefilter (member name <c>CheckSignature</c>)
/// already excludes them. Detection is a cheap name prefilter followed by a symbol bind: the invocation must
/// resolve to a method named <c>CheckSignature</c> declared on <c>System.Security.Cryptography.Xml.SignedXml</c>
/// whose parameters are empty or a single <c>bool</c>. <c>SignedXml</c> is probed once per compilation (it
/// ships in the separate <c>System.Security.Cryptography.Xml</c> package on modern .NET), so a project without
/// it registers no syntax action and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1008UntrustedXmlSignatureKeyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the XML-signing type that gates the rule and declares the reported method.</summary>
    private const string SignedXmlMetadataName = "System.Security.Cryptography.Xml.SignedXml";

    /// <summary>The name of the signature-verification method the rule inspects.</summary>
    private const string CheckSignatureMethodName = "CheckSignature";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArrays.Of(SecurityRules.UntrustedXmlSignatureKey);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Gate the whole rule on SignedXml: without it there is no CheckSignature to report, so nothing
            // is registered and the clean path costs nothing. On modern .NET the type lives in the separate
            // System.Security.Cryptography.Xml package, so its absence is common.
            var signedXmlType = start.Compilation.GetTypeByMetadataName(SignedXmlMetadataName);
            if (signedXmlType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, signedXmlType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1008 for a no-key <c>SignedXml.CheckSignature</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="signedXmlType">The gated <c>SignedXml</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol signedXmlType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: an invocation of a member named 'CheckSignature'. Only the bare name is checked
        // here, so the semantic model is never touched on the overwhelmingly common non-matching path.
        if (GetInvokedName(invocation.Expression) != CheckSignatureMethodName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.Name != CheckSignatureMethodName
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, signedXmlType)
            || !IsNoKeyOverload(method))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UntrustedXmlSignatureKey,
            invocation.SyntaxTree,
            invocation.Span,
            GetReceiverDisplayName(invocation.Expression)));
    }

    /// <summary>Returns whether a <c>CheckSignature</c> overload takes no caller-supplied key.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> for the parameterless or single-<c>bool</c> overloads that trust the embedded key.</returns>
    private static bool IsNoKeyOverload(IMethodSymbol method)
    {
        var parameters = method.Parameters;

        // The parameterless overload trusts the document's KeyInfo outright; the single-bool overload only
        // toggles reference resolution and still supplies no key. Every other overload takes a key or
        // certificate the caller controls, so it is safe and left alone.
        return parameters.Length switch
        {
            0 => true,
            1 => parameters[0].Type.SpecialType == SpecialType.System_Boolean,
            _ => false,
        };
    }

    /// <summary>Returns the simple name an invocation targets, or <see langword="null"/> when it is not a simple call.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The invoked member's simple-name text, or <see langword="null"/>.</returns>
    private static string? GetInvokedName(ExpressionSyntax invoked)
        => invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns a readable name for the verified <c>SignedXml</c> receiver to fill the diagnostic message.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The receiver's identifier, or <c>SignedXml</c> when the call has no named receiver.</returns>
    private static string GetReceiverDisplayName(ExpressionSyntax invoked)
    {
        var receiver = invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            _ => null,
        };

        return receiver switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => "SignedXml",
        };
    }
}
