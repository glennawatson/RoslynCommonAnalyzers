// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags regex call chains that materialize match objects just to answer a scalar question
/// (PSH1406): <c>regex.Match(input).Success</c> allocates a <c>Match</c> to produce a bool that
/// <c>IsMatch</c> answers directly, and <c>regex.Matches(input).Count</c> allocates a
/// <c>MatchCollection</c> to produce an int that the <c>Count</c> method (.NET 7+) answers
/// directly. Both the instance and static <c>Regex</c> forms are reported, but only direct
/// chains — a match stored in a local first is not. The <c>Count</c> shape is gated at
/// compilation start on <c>Regex</c> actually exposing a <c>Count</c> method.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1406UseDirectRegexQueriesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The direct boolean query replacing <c>Match(...).Success</c>.</summary>
    internal const string IsMatchMethodName = "IsMatch";

    /// <summary>The direct count query replacing <c>Matches(...).Count</c>, and the trailing property it replaces.</summary>
    internal const string CountMethodName = "Count";

    /// <summary>The materializing single-match method.</summary>
    private const string MatchMethodName = "Match";

    /// <summary>The materializing match-collection method.</summary>
    private const string MatchesMethodName = "Matches";

    /// <summary>The trailing property of the boolean chain.</summary>
    private const string SuccessPropertyName = "Success";

    /// <summary>The metadata name of the regex type.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.UseDirectRegexQueries);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var regexType = start.Compilation.GetTypeByMetadataName(RegexMetadataName);
            if (regexType is null)
            {
                return;
            }

            var hasCountMethod = HasCountMethod(regexType);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMemberAccess(nodeContext, regexType, hasCountMethod), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Matches a reported chain shape (syntax only): <c>Match(...).Success</c> or <c>Matches(...).Count</c>.</summary>
    /// <param name="access">The member access to inspect; a match covers the whole chain.</param>
    /// <param name="materializingInvocation">The inner <c>Match</c>/<c>Matches</c> invocation when the shape matches.</param>
    /// <param name="replacementName">The direct query method name when the shape matches.</param>
    /// <returns><see langword="true"/> when the member access is one of the two replaced chains.</returns>
    internal static bool TryGetQueryShape(
        MemberAccessExpressionSyntax access,
        [NotNullWhen(true)] out InvocationExpressionSyntax? materializingInvocation,
        out string replacementName)
    {
        if (access is { Name.Identifier.ValueText: SuccessPropertyName, Expression: InvocationExpressionSyntax matchInvocation }
            && GetInvokedName(matchInvocation) == MatchMethodName)
        {
            materializingInvocation = matchInvocation;
            replacementName = IsMatchMethodName;
            return true;
        }

        if (access is { Name.Identifier.ValueText: CountMethodName, Expression: InvocationExpressionSyntax matchesInvocation }
            && GetInvokedName(matchesInvocation) == MatchesMethodName)
        {
            materializingInvocation = matchesInvocation;
            replacementName = CountMethodName;
            return true;
        }

        materializingInvocation = null;
        replacementName = string.Empty;
        return false;
    }

    /// <summary>Reports PSH1406 for a chain whose materializing call binds to the regex type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexType">The regex type.</param>
    /// <param name="hasCountMethod">Whether the regex type exposes the direct <c>Count</c> method.</param>
    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, INamedTypeSymbol regexType, bool hasCountMethod)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (!TryGetQueryShape(access, out var materializingInvocation, out var replacementName)
            || (replacementName == CountMethodName && !hasCountMethod))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(materializingInvocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, regexType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseDirectRegexQueries,
            access.SyntaxTree,
            access.Name.Span,
            replacementName));
    }

    /// <summary>Returns the invoked member's simple name text for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static string? GetInvokedName(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
            _ => null
        };

    /// <summary>Returns whether the regex type exposes a <c>Count</c> method (instance or static).</summary>
    /// <param name="regexType">The regex type to probe.</param>
    /// <returns><see langword="true"/> when the direct count query exists (.NET 7+).</returns>
    private static bool HasCountMethod(INamedTypeSymbol regexType)
    {
        var members = regexType.GetMembers(CountMethodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }
}
