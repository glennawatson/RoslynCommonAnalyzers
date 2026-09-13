// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags code that removes the forwarded-headers trust boundary on a
/// <c>Microsoft.AspNetCore.Builder.ForwardedHeadersOptions</c> (SES1511). The middleware only rewrites a
/// request's remote IP, host, and scheme from <c>X-Forwarded-*</c> headers when the request arrives from a
/// trusted proxy (loopback by default); two local shapes strip that restriction and are reported. The
/// <c>Clear</c> shape is a <c>.Clear()</c> call on the options' <c>KnownProxies</c>, <c>KnownNetworks</c>, or
/// <c>KnownIPNetworks</c> member -- emptying the trusted-proxy/network list makes the middleware trust every
/// source. The hop-limit shape is <c>ForwardLimit</c> set to the literal <c>null</c>, written directly
/// (<c>options.ForwardLimit = null</c>) or in an object initializer
/// (<c>new ForwardedHeadersOptions { ForwardLimit = null }</c>), which removes the cap on how many forwarded
/// hops are honoured. In both shapes the accessed member is bound to its symbol and its containing type is
/// confirmed to be <c>ForwardedHeadersOptions</c>, so a same-named member on any other type is ignored. The
/// options type is resolved only after a matching call or assignment survives the syntactic prefilter.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1511ForwardedHeadersTrustBoundaryRemovalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the list-clearing method whose call removes the trusted-proxy restriction.</summary>
    private const string ClearMethodName = "Clear";

    /// <summary>The name of the hop-limit property whose <c>null</c> assignment removes the cap.</summary>
    private const string ForwardLimitPropertyName = "ForwardLimit";

    /// <summary>The trusted-proxy list member.</summary>
    private const string KnownProxiesPropertyName = "KnownProxies";

    /// <summary>The trusted-network list member (superseded by <c>KnownIPNetworks</c> on newer frameworks).</summary>
    private const string KnownNetworksPropertyName = "KnownNetworks";

    /// <summary>The trusted-network list member on newer frameworks.</summary>
    private const string KnownIPNetworksPropertyName = "KnownIPNetworks";

    /// <summary>The suffix appended to a trust-list member name to display the reported call.</summary>
    private const string ClearCallSuffix = ".Clear()";

    /// <summary>The message argument used when the hop-limit is removed.</summary>
    private const string ForwardLimitNullDisplay = "ForwardLimit = null";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.ForwardedHeadersTrustBoundaryRemoval);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var types = new ForwardedHeadersTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeClearInvocation(nodeContext, types), SyntaxKind.InvocationExpression);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeForwardLimitAssignment(nodeContext, types), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1511 for a <c>.Clear()</c> call on a gated trusted-proxy/network list member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped forwarded-headers type cache.</param>
    private static void AnalyzeClearInvocation(in SyntaxNodeAnalysisContext context, ForwardedHeadersTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.KnownProxies/KnownNetworks/KnownIPNetworks.Clear()' with no arguments.
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: ClearMethodName } clearAccess
            || clearAccess.Expression is not MemberAccessExpressionSyntax listAccess
            || !IsTrustListMemberName(listAccess.Name.Identifier.ValueText))
        {
            return;
        }

        if (types.Get() is not { } optionsType
            || context.SemanticModel.GetSymbolInfo(listAccess, context.CancellationToken).Symbol is not IPropertySymbol property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, optionsType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ForwardedHeadersTrustBoundaryRemoval,
            invocation.SyntaxTree,
            invocation.Span,
            property.Name + ClearCallSuffix));
    }

    /// <summary>Reports SES1511 for a <c>ForwardLimit = null</c> assignment on a gated options type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped forwarded-headers type cache.</param>
    private static void AnalyzeForwardLimitAssignment(in SyntaxNodeAnalysisContext context, ForwardedHeadersTypes types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.ForwardLimit = null' or the initializer form 'ForwardLimit = null'.
        if (!assignment.Right.IsKind(SyntaxKind.NullLiteralExpression)
            || GetForwardLimitTarget(assignment.Left) is not { } memberExpression)
        {
            return;
        }

        if (types.Get() is not { } optionsType
            || context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { Name: ForwardLimitPropertyName } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, optionsType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ForwardedHeadersTrustBoundaryRemoval,
            assignment.SyntaxTree,
            assignment.Span,
            ForwardLimitNullDisplay));
    }

    /// <summary>Returns whether a member name is one of the trusted-proxy/network list members.</summary>
    /// <param name="memberName">The accessed member's simple name.</param>
    /// <returns><see langword="true"/> for <c>KnownProxies</c>, <c>KnownNetworks</c>, or <c>KnownIPNetworks</c>.</returns>
    private static bool IsTrustListMemberName(string memberName) =>
        memberName is KnownProxiesPropertyName or KnownNetworksPropertyName or KnownIPNetworksPropertyName;

    /// <summary>Returns the assignment's left expression when it names <c>ForwardLimit</c>.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns>The left expression to bind, or <see langword="null"/> when it is not the guarded member.</returns>
    private static ExpressionSyntax? GetForwardLimitTarget(ExpressionSyntax left) =>
        left switch
        {
            // 'options.ForwardLimit = null'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: ForwardLimitPropertyName } or IdentifierNameSyntax { Identifier.ValueText: ForwardLimitPropertyName } => left,

            _ => null,
        };

    /// <summary>Resolves the forwarded-headers options type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose options type is resolved.</param>
    private sealed class ForwardedHeadersTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the forwarded-headers options type the rule gates on.</summary>
        private const string OptionsMetadataName = "Microsoft.AspNetCore.Builder.ForwardedHeadersOptions";

        /// <summary>The cached options type, with a null element when the type is absent.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Resolves the options type on first demand and caches its absence too.</summary>
        /// <returns>The options type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(OptionsMetadataName)])[0];
    }
}
