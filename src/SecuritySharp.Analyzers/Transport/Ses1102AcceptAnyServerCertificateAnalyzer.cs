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
/// gated on <c>System.Net.Http.HttpClientHandler</c> exposing the member. The type and member are resolved
/// only after a member access passes the name check.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1102AcceptAnyServerCertificateAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the accept-any server-certificate validator member.</summary>
    private const string ValidatorMemberName = "DangerousAcceptAnyServerCertificateValidator";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.AcceptAnyServerCertificate);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var frameworkType = new FrameworkType(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeMemberAccess(nodeContext, frameworkType), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Reports SES1102 for a member access that reads the accept-any server-certificate validator.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="frameworkType">The deferred type lookup shared by this compilation's callbacks.</param>
    private static void AnalyzeMemberAccess(in SyntaxNodeAnalysisContext context, FrameworkType frameworkType)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Syntactic prefilter: only a '....DangerousAcceptAnyServerCertificateValidator' access can match.
        if (memberAccess.Name.Identifier.ValueText != ValidatorMemberName)
        {
            return;
        }

        // Bind the member: report only when it truly resolves to the member on HttpClientHandler, so a
        // same-named member on an unrelated type is never flagged.
        if (frameworkType.Get() is not { } handlerType
            || handlerType.GetMembers(ValidatorMemberName).IsEmpty
            || context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol is not { Name: ValidatorMemberName } member
            || !SymbolEqualityComparer.Default.Equals(member.ContainingType, handlerType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AcceptAnyServerCertificate,
            memberAccess.SyntaxTree,
            memberAccess.Span));
    }

    /// <summary>Resolves the HTTP handler type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose references supply the type.</param>
    private sealed class FrameworkType(Compilation compilation)
    {
        /// <summary>The metadata name of the handler type that owns the accept-any validator.</summary>
        private const string HttpClientHandlerMetadataName = "System.Net.Http.HttpClientHandler";

        /// <summary>The cached type, or an empty array when unavailable; null until first demand.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the HTTP handler type, caching absent types as well as successful lookups.</summary>
        /// <returns>The resolved type, or null when unavailable.</returns>
        public INamedTypeSymbol? Get()
        {
            var resolved = _resolved ??= compilation.GetTypeByMetadataName(HttpClientHandlerMetadataName) is { } type ? [type] : [];
            return resolved.Length == 0 ? null : resolved[0];
        }
    }
}
