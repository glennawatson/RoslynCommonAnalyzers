// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an assignment that deliberately weakens X509 certificate-chain validation (SES1104). The rule
/// reports a set of an <c>X509ChainPolicy</c> member — a plain <c>policy.Member = value</c> or an
/// object-initializer <c>Member = value</c> — when it disables genuine chain checks:
/// <c>RevocationMode</c> assigned <c>X509RevocationMode.NoCheck</c> (revocation checking off), or
/// <c>VerificationFlags</c> assigned an <c>X509VerificationFlags</c> value that names
/// <c>AllowUnknownCertificateAuthority</c> or <c>AllFlags</c> (untrusted-authority errors ignored),
/// including inside an OR-combination where any operand names one of those. The rule resolves
/// <c>X509ChainPolicy</c> once per compilation and registers nothing when it is absent, so a target
/// framework without the type pays nothing and never receives a diagnostic it cannot act on. Detection
/// is local to the assignment: only the value written at the site is inspected, never a value that flows
/// in from elsewhere.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1104WeakenedCertificateChainValidationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>X509ChainPolicy</c> member controlling revocation checking.</summary>
    private const string RevocationModeMemberName = "RevocationMode";

    /// <summary>The <c>X509ChainPolicy</c> member controlling which chain errors are ignored.</summary>
    private const string VerificationFlagsMemberName = "VerificationFlags";

    /// <summary>The <c>X509RevocationMode</c> value that turns revocation checking off.</summary>
    private const string NoCheckFieldName = "NoCheck";

    /// <summary>The <c>X509VerificationFlags</c> value that ignores untrusted-authority errors.</summary>
    private const string AllowUnknownCertificateAuthorityFieldName = "AllowUnknownCertificateAuthority";

    /// <summary>The <c>X509VerificationFlags</c> value that ignores every chain error.</summary>
    private const string AllFlagsFieldName = "AllFlags";

    /// <summary>The metadata name of the chain-policy type whose weakening members are guarded.</summary>
    private const string ChainPolicyMetadataName = "System.Security.Cryptography.X509Certificates.X509ChainPolicy";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.WeakenedCertificateChainValidation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var chainPolicyType = start.Compilation.GetTypeByMetadataName(ChainPolicyMetadataName);
            if (chainPolicyType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, chainPolicyType), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1104 when an assignment weakens an <c>X509ChainPolicy</c> chain check.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="chainPolicyType">The resolved <c>X509ChainPolicy</c> type gating the rule.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol chainPolicyType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: a set of a member spelled 'RevocationMode' or 'VerificationFlags',
        // reached either as 'target.Member =' or as an object-initializer 'Member ='.
        if (GetAssignedMemberName(assignment.Left) is not { } memberName
            || (memberName != RevocationModeMemberName && memberName != VerificationFlagsMemberName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, chainPolicyType))
        {
            return;
        }

        // The confirmed X509ChainPolicy member's own type is the enum the value must belong to.
        var isWeakened = memberName == RevocationModeMemberName
            ? IsRevocationDisabled(context.SemanticModel, assignment.Right, property.Type, context.CancellationToken)
            : SuppressesChainErrors(context.SemanticModel, assignment.Right, property.Type, context.CancellationToken);

        if (!isWeakened)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.WeakenedCertificateChainValidation,
            assignment.Right.SyntaxTree,
            assignment.Right.Span,
            memberName));
    }

    /// <summary>Returns the member name being assigned, for a member-access or initializer target.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns>The member name, or <see langword="null"/> when the target is not a simple member set.</returns>
    private static string? GetAssignedMemberName(ExpressionSyntax left)
        => left switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns whether the assigned revocation mode is <c>NoCheck</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="value">The assigned value expression.</param>
    /// <param name="revocationModeType">The <c>RevocationMode</c> property's enum type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the value is <c>X509RevocationMode.NoCheck</c>.</returns>
    private static bool IsRevocationDisabled(SemanticModel model, ExpressionSyntax value, ITypeSymbol revocationModeType, CancellationToken cancellationToken)
        => model.GetSymbolInfo(value, cancellationToken).Symbol is IFieldSymbol field
            && field.Name == NoCheckFieldName
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, revocationModeType);

    /// <summary>Returns whether an assigned verification-flags value ignores genuine chain errors.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="value">The assigned value expression, possibly an OR-combination.</param>
    /// <param name="verificationFlagsType">The <c>VerificationFlags</c> property's enum type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when any operand names a suppressing flag.</returns>
    private static bool SuppressesChainErrors(SemanticModel model, ExpressionSyntax value, ITypeSymbol verificationFlagsType, CancellationToken cancellationToken)
    {
        var expression = value;
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        // An OR-combination suppresses errors when any operand names a suppressing flag.
        if (expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.BitwiseOrExpression))
        {
            return SuppressesChainErrors(model, binary.Left, verificationFlagsType, cancellationToken)
                || SuppressesChainErrors(model, binary.Right, verificationFlagsType, cancellationToken);
        }

        return model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol field
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, verificationFlagsType)
            && (field.Name == AllowUnknownCertificateAuthorityFieldName || field.Name == AllFlagsFieldName);
    }
}
