// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports culture-sensitive string searches and comparisons that omit a
/// <see cref="StringComparison"/> argument (PSH1207). The instance shapes
/// <c>StartsWith(string)</c>, <c>EndsWith(string)</c>, <c>IndexOf(string)</c>, and
/// <c>LastIndexOf(string)</c>, and the static <c>string.Compare(string, string)</c>,
/// bind to their culture-sensitive overloads, which consult the current culture on
/// every call. Requiring an explicit <c>StringComparison</c> lets the caller pick the
/// far cheaper <c>StringComparison.Ordinal</c>. Only the all-string shapes are matched:
/// the char overloads, <c>Contains(string)</c>, and <c>Equals</c>/<c>==</c> are already
/// ordinal, and any call that already passes a <c>StringComparison</c> has a different
/// argument count and is left untouched. The rule is gated once per compilation on
/// <c>System.StringComparison</c> existing, so it costs nothing on targets without it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1207SpecifyStringComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The argument count of the reported instance search shapes (<c>StartsWith(string)</c>).</summary>
    private const int InstanceSearchArgumentCount = 1;

    /// <summary>The argument count of the reported static <c>string.Compare(string, string)</c> shape.</summary>
    private const int StaticCompareArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.SpecifyStringComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName("System.StringComparison") is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1207 for a culture-sensitive all-string search or comparison call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax name
            || !TryClassify(name.Identifier.ValueText, out var argumentCount, out var isStatic)
            || invocation.ArgumentList.Arguments.Count != argumentCount
            || !BindsToCultureSensitiveStringMethod(context.SemanticModel, invocation, argumentCount, isStatic, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.SpecifyStringComparison,
            invocation.SyntaxTree,
            name.Span,
            name.Identifier.ValueText));
    }

    /// <summary>Maps a member name to the all-string shape the rule reports.</summary>
    /// <param name="methodName">The invoked member name.</param>
    /// <param name="argumentCount">The argument count of the reported shape (1 instance, 2 static).</param>
    /// <param name="isStatic">Whether the reported shape is the static <c>string.Compare</c>.</param>
    /// <returns><see langword="true"/> when the member is a culture-sensitive search or comparison.</returns>
    private static bool TryClassify(string methodName, out int argumentCount, out bool isStatic)
    {
        switch (methodName)
        {
            case "StartsWith":
            case "EndsWith":
            case "IndexOf":
            case "LastIndexOf":
            {
                argumentCount = InstanceSearchArgumentCount;
                isStatic = false;
                return true;
            }

            case "Compare":
            {
                argumentCount = StaticCompareArgumentCount;
                isStatic = true;
                return true;
            }

            default:
            {
                argumentCount = 0;
                isStatic = false;
                return false;
            }
        }
    }

    /// <summary>Returns whether an invocation binds to a <see cref="string"/> method taking only string parameters.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="argumentCount">The expected parameter count.</param>
    /// <param name="isStatic">Whether the expected overload is static.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to the culture-sensitive all-string overload.</returns>
    private static bool BindsToCultureSensitiveStringMethod(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        int argumentCount,
        bool isStatic,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.IsStatic != isStatic
            || method.ContainingType.SpecialType != SpecialType.System_String
            || method.Parameters.Length != argumentCount)
        {
            return false;
        }

        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Type.SpecialType != SpecialType.System_String)
            {
                return false;
            }
        }

        return true;
    }
}
