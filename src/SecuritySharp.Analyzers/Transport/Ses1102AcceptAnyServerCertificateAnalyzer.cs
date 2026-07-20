// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a read of <c>HttpClientHandler.DangerousAcceptAnyServerCertificateValidator</c> (SES1102). That
/// static member is a callback that returns <see langword="true"/> for every server certificate; assigning it
/// to <c>ServerCertificateCustomValidationCallback</c> (or any equivalent validation callback) turns off TLS
/// server authentication and opens the connection to man-in-the-middle attacks. Reading the member has no
/// other purpose, so every member-access reference to it is reported — the rule does not try to follow where
/// the value is later assigned. The member is bound (never matched on identifier text alone), and the rule is
/// resolved once per compilation by probing <c>System.Net.Http.HttpClientHandler</c> and confirming the member
/// exists on it; on a target framework without either (netstandard2.0, .NET Framework) nothing is registered,
/// so a project that cannot reference the member pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1102AcceptAnyServerCertificateAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the handler type that owns the accept-any validator.</summary>
    private const string HttpClientHandlerMetadataName = "System.Net.Http.HttpClientHandler";

    /// <summary>The name of the accept-any server-certificate validator member.</summary>
    private const string ValidatorMemberName = "DangerousAcceptAnyServerCertificateValidator";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.AcceptAnyServerCertificate);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var handlerType = start.Compilation.GetTypeByMetadataName(HttpClientHandlerMetadataName);
            if (handlerType is null || handlerType.GetMembers(ValidatorMemberName).Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMemberAccess(nodeContext, handlerType), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Reports SES1102 for a member access that reads the accept-any server-certificate validator.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="handlerType">The gated <c>HttpClientHandler</c> type resolved for the compilation.</param>
    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, INamedTypeSymbol handlerType)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Syntactic prefilter: only a '....DangerousAcceptAnyServerCertificateValidator' access can match.
        if (memberAccess.Name.Identifier.ValueText != ValidatorMemberName)
        {
            return;
        }

        // Bind the member: report only when it truly resolves to the member on HttpClientHandler, so a
        // same-named member on an unrelated type is never flagged.
        if (context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol is not { Name: ValidatorMemberName } member
            || !SymbolEqualityComparer.Default.Equals(member.ContainingType, handlerType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AcceptAnyServerCertificate,
            memberAccess.SyntaxTree,
            memberAccess.Span));
    }
}
