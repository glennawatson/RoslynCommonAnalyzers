// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// options type is probed once per compilation; a project without ASP.NET Core registers nothing and pays no
/// analysis cost.
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

    /// <summary>The metadata name of the forwarded-headers options type the rule gates on.</summary>
    private const string OptionsMetadataName = "Microsoft.AspNetCore.Builder.ForwardedHeadersOptions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ForwardedHeadersTrustBoundaryRemoval);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(OptionsMetadataName) is not { } optionsType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeClearInvocation(nodeContext, optionsType), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeForwardLimitAssignment(nodeContext, optionsType), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1511 for a <c>.Clear()</c> call on a gated trusted-proxy/network list member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsType">The gated <c>ForwardedHeadersOptions</c> type.</param>
    private static void AnalyzeClearInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol optionsType)
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

        if (context.SemanticModel.GetSymbolInfo(listAccess, context.CancellationToken).Symbol is not IPropertySymbol property
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
    /// <param name="optionsType">The gated <c>ForwardedHeadersOptions</c> type.</param>
    private static void AnalyzeForwardLimitAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol optionsType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.ForwardLimit = null' or the initializer form 'ForwardLimit = null'.
        if (!assignment.Right.IsKind(SyntaxKind.NullLiteralExpression)
            || GetForwardLimitTarget(assignment.Left) is not { } memberExpression)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { Name: ForwardLimitPropertyName } property
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
    private static bool IsTrustListMemberName(string memberName)
        => memberName is KnownProxiesPropertyName or KnownNetworksPropertyName or KnownIPNetworksPropertyName;

    /// <summary>Returns the assignment's left expression when it names <c>ForwardLimit</c>.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns>The left expression to bind, or <see langword="null"/> when it is not the guarded member.</returns>
    private static ExpressionSyntax? GetForwardLimitTarget(ExpressionSyntax left)
        => left switch
        {
            // 'options.ForwardLimit = null'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: ForwardLimitPropertyName } => left,

            // 'new ForwardedHeadersOptions { ForwardLimit = null }' (object-initializer member).
            IdentifierNameSyntax { Identifier.ValueText: ForwardLimitPropertyName } => left,

            _ => null,
        };
}
