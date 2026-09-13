// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.JwtSignatureValidationDisabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var parameterTypes = new TokenValidationTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, parameterTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1503 for a signature flag set to <c>false</c> on the gated options type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="parameterTypes">The token-validation type cache for this compilation.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, TokenValidationTypes parameterTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.RequireSignedTokens = false' / '...ValidateIssuerSigningKey = false', or the
        // object-initializer member forms. Both bind the left member to the options property below.
        if (!assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression)
            || !IsSignatureFlagTarget(assignment.Left))
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

    /// <summary>Returns whether an assignment target syntactically names one of the two signature flags.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns><see langword="true"/> for <c>x.RequireSignedTokens</c>/<c>x.ValidateIssuerSigningKey</c> or their bare initializer forms.</returns>
    private static bool IsSignatureFlagTarget(ExpressionSyntax left) =>
        left switch
        {
            // 'parameters.RequireSignedTokens = false' / '...ValidateIssuerSigningKey = false'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name } => IsSignatureFlag(name),

            // 'new TokenValidationParameters { RequireSignedTokens = false }' (object-initializer member).
            IdentifierNameSyntax { Identifier.ValueText: var name } => IsSignatureFlag(name),

            _ => false,
        };

    /// <summary>Returns whether a member name is one of the two guarded signature-verification flags.</summary>
    /// <param name="name">The member name to test.</param>
    /// <returns><see langword="true"/> for <c>RequireSignedTokens</c> or <c>ValidateIssuerSigningKey</c>.</returns>
    private static bool IsSignatureFlag(string name) =>
        name is RequireSignedTokensPropertyName or ValidateIssuerSigningKeyPropertyName;

    /// <summary>Resolves the token-validation options type on first demand within one compilation.</summary>
    /// <param name="compilation">The compilation whose references are searched.</param>
    private sealed class TokenValidationTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the token-validation options type whose signature flags are guarded.</summary>
        private const string TokenValidationParametersMetadataName = "Microsoft.IdentityModel.Tokens.TokenValidationParameters";

        /// <summary>Stores the resolved symbol, including a missing result, in an atomically assigned array.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the token-validation options type, resolving it on first demand.</summary>
        /// <returns>The options type, or null when it is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(TokenValidationParametersMetadataName)])[0];
    }
}
