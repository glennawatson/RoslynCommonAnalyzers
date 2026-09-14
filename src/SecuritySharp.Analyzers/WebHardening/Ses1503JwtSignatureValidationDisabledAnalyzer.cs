// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags JWT signature verification being disabled on
/// <c>Microsoft.IdentityModel.Tokens.TokenValidationParameters</c> (SES1503). The rule reports a
/// <c>RequireSignedTokens = false</c> or <c>ValidateIssuerSigningKey = false</c> assignment -- written directly
/// (<c>parameters.RequireSignedTokens = false</c>) or as an object-initializer member
/// (<c>new TokenValidationParameters { ValidateIssuerSigningKey = false }</c>) -- when the assigned member's containing
/// type is <c>TokenValidationParameters</c>. Either flag, once false, lets a forged or unsigned token pass validation,
/// the most dangerous JWT misconfiguration. The issuer, audience, and lifetime flags are deliberately out of scope. The
/// options type is resolved only after an assignment passes the syntax checks; a project without
/// <c>Microsoft.IdentityModel</c> never receives a diagnostic it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1503JwtSignatureValidationDisabledAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The property that, when true, rejects a token carrying no signature.</summary>
    private const string RequireSignedTokensPropertyName = "RequireSignedTokens";

    /// <summary>The property that, when true, verifies the token's signing key against the accepted keys.</summary>
    private const string ValidateIssuerSigningKeyPropertyName = "ValidateIssuerSigningKey";

    /// <summary>The metadata name of the token-validation options type whose signature flags are guarded.</summary>
    private const string TokenValidationParametersMetadataName = "Microsoft.IdentityModel.Tokens.TokenValidationParameters";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.JwtSignatureValidationDisabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, TokenValidationParametersMetadataName),
            AnalyzeAssignment,
            SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports SES1503 for a signature flag set to <c>false</c> on the gated options type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterTypes">The token-validation type cache for this compilation.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, LazyMetadataType parameterTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.RequireSignedTokens = false' / '...ValidateIssuerSigningKey = false', or the
        // object-initializer member forms. Both bind the left member to the options property below.
        if (!assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression)
            || SyntaxNames.GetMemberName(assignment.Left) is not { } name
            || !IsSignatureFlag(name))
        {
            return;
        }

        if (parameterTypes.Get() is not { } parametersType
            || context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property
            || !IsSignatureFlag(property.Name)
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, parametersType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.JwtSignatureValidationDisabled,
            assignment.SyntaxTree,
            assignment.Span,
            property.Name));
    }

    /// <summary>Returns whether a member name is one of the two guarded signature-verification flags.</summary>
    /// <param name="name">The member name to test.</param>
    /// <returns><see langword="true"/> for <c>RequireSignedTokens</c> or <c>ValidateIssuerSigningKey</c>.</returns>
    private static bool IsSignatureFlag(string name) =>
        name is RequireSignedTokensPropertyName or ValidateIssuerSigningKeyPropertyName;
}
