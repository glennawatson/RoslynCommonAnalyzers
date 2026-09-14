// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an OpenID Connect authorization-code-flow protection being disabled (SES1514). The rule reports a
/// <c>= false</c> assignment -- written directly (<c>options.UsePkce = false</c>) or as an object-initializer member
/// (<c>new OpenIdConnectProtocolValidator { RequireNonce = false }</c>) -- to one of four flags, matched by symbol and
/// containing type: <c>UsePkce</c> on <c>Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions</c>
/// (disables PKCE), and <c>RequireState</c>, <c>RequireStateValidation</c>, or <c>RequireNonce</c> on
/// <c>Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectProtocolValidator</c> (reached through
/// <c>OpenIdConnectOptions.ProtocolValidator</c>; disables the state or nonce check). Each protection defends the login
/// against cross-site request forgery or token replay, so turning one off is a downgrade. The options type is probed
/// only for a candidate assignment and gates the whole rule; the validator flags additionally require the validator
/// type to resolve, so a project without OpenID Connect authentication never receives a diagnostic it
/// cannot act on. The issuer/audience/lifetime and signature flags on <c>TokenValidationParameters</c> are a separate
/// concern and are not reported here.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1514OidcProtocolProtectionDisabledAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The flag that, when true, binds the code exchange to the client via PKCE.</summary>
    private const string UsePkcePropertyName = "UsePkce";

    /// <summary>The flag that, when true, requires the OpenID Connect state parameter to be present.</summary>
    private const string RequireStatePropertyName = "RequireState";

    /// <summary>The flag that, when true, validates the returned state against the request that began the flow.</summary>
    private const string RequireStateValidationPropertyName = "RequireStateValidation";

    /// <summary>The flag that, when true, requires and validates the id-token nonce that defeats replay.</summary>
    private const string RequireNoncePropertyName = "RequireNonce";

    /// <summary>The metadata name of the options type that carries <c>UsePkce</c> and the validator.</summary>
    private const string OpenIdConnectOptionsMetadataName = "Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions";

    /// <summary>The metadata name of the validator type that carries the state and nonce flags.</summary>
    private const string OpenIdConnectProtocolValidatorMetadataName = "Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectProtocolValidator";

    /// <summary>The options and validator metadata names, in slot order.</summary>
    private static readonly string[] OidcMetadataNames = [OpenIdConnectOptionsMetadataName, OpenIdConnectProtocolValidatorMetadataName];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.OidcProtocolProtectionDisabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypes(compilation, OidcMetadataNames),
            AnalyzeAssignment,
            SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports SES1514 for a protocol-protection flag set to <c>false</c> on a gated type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped OpenID Connect type cache.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, LazyMetadataTypes types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.UsePkce = false' / '...RequireState = false' etc., or the object-initializer
        // member forms. Both bind the left member to the guarded property below.
        if (!assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression)
            || SyntaxNames.GetMemberName(assignment.Left) is not { } name
            || !IsProtectionFlag(name))
        {
            return;
        }

        if (types.Get() is not [{ } optionsType, var validatorType])
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property)
        {
            return;
        }

        // UsePkce lives on the options type; the three Require* flags live on the validator type.
        var expectedType = property.Name == UsePkcePropertyName
            ? optionsType
            : validatorType;
        if (!SymbolEqualityComparer.Default.Equals(property.ContainingType, expectedType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.OidcProtocolProtectionDisabled,
            assignment.SyntaxTree,
            assignment.Span,
            property.ContainingType.Name,
            property.Name));
    }

    /// <summary>Returns whether a member name is one of the four guarded protocol-protection flags.</summary>
    /// <param name="name">The member name to test.</param>
    /// <returns><see langword="true"/> for <c>UsePkce</c>, <c>RequireState</c>, <c>RequireStateValidation</c>, or <c>RequireNonce</c>.</returns>
    private static bool IsProtectionFlag(string name) =>
        name is UsePkcePropertyName or RequireStatePropertyName or RequireStateValidationPropertyName or RequireNoncePropertyName;
}
