// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports <c>==</c>/<c>!=</c> comparisons between the literal <c>0</c> and a string
/// ordering call (PSH1216), with the operands in either order. The reported shapes are
/// the static <c>string.Compare(a, b)</c>, <c>string.Compare(a, b, StringComparison)</c>,
/// and <c>string.Compare(a, b, ignoreCase)</c> with a literal <see langword="true"/> or
/// <see langword="false"/> flag, the static <c>string.CompareOrdinal(a, b)</c>, and the
/// instance <c>a.CompareTo(b)</c> where the receiver and argument are strings. An
/// equality test throws away the ordering information Compare computes, and Compare
/// cannot bail out early on length mismatches the way <c>string.Equals</c> can. The
/// <c>ignoreCase</c> shape is only reported for literal flags so the fix can map the
/// flag to a <see cref="StringComparison"/> value, and the rule is gated once per
/// compilation on <c>System.StringComparison</c> existing because the fix rewrites
/// into its members.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1216UseEqualsOverCompareAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name of the static culture-sensitive comparison.</summary>
    internal const string CompareName = "Compare";

    /// <summary>The invoked member name of the static ordinal comparison.</summary>
    internal const string CompareOrdinalName = "CompareOrdinal";

    /// <summary>The invoked member name of the instance comparison.</summary>
    internal const string CompareToName = "CompareTo";

    /// <summary>The message display of the static culture-sensitive comparison.</summary>
    private const string CompareDisplay = "string.Compare";

    /// <summary>The message display of the static ordinal comparison.</summary>
    private const string CompareOrdinalDisplay = "string.CompareOrdinal";

    /// <summary>The argument count of the instance <c>CompareTo(string)</c> shape.</summary>
    private const int CompareToArgumentCount = 1;

    /// <summary>The argument count of the two-string static comparison shapes.</summary>
    private const int TwoStringArgumentCount = 2;

    /// <summary>The argument count of the static <c>Compare</c> shapes carrying a comparison option.</summary>
    private const int CompareWithOptionArgumentCount = 3;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseEqualsOverCompare);

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

            start.RegisterSyntaxNodeAction(AnalyzeComparison, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        });
    }

    /// <summary>Splits a comparison into its ordering call and its literal-zero operand, syntactically.</summary>
    /// <param name="binary">The <c>==</c>/<c>!=</c> expression to probe.</param>
    /// <param name="invocation">The ordering invocation when one operand is the literal <c>0</c> and the other calls a comparison member.</param>
    /// <param name="methodName">The invoked member name (<c>Compare</c>, <c>CompareOrdinal</c>, or <c>CompareTo</c>).</param>
    /// <returns><see langword="true"/> when the comparison matches the reported shape syntactically.</returns>
    internal static bool TryGetOrderingCall(BinaryExpressionSyntax binary, out InvocationExpressionSyntax? invocation, out string? methodName)
    {
        if (IsZeroLiteral(binary.Right))
        {
            return TryGetCallShape(binary.Left, out invocation, out methodName);
        }

        if (IsZeroLiteral(binary.Left))
        {
            return TryGetCallShape(binary.Right, out invocation, out methodName);
        }

        invocation = null;
        methodName = null;
        return false;
    }

    /// <summary>Reports PSH1216 for an equality test of a string ordering call against zero.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetOrderingCall(binary, out var invocation, out var methodName)
            || !BindsToStringOrderingMethod(context.SemanticModel, invocation!, methodName!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseEqualsOverCompare,
            binary.SyntaxTree,
            binary.Span,
            GetDisplay(methodName!)));
    }

    /// <summary>Returns whether an expression is the numeric literal <c>0</c>.</summary>
    /// <param name="expression">The candidate operand expression.</param>
    /// <returns><see langword="true"/> for a numeric literal whose value is the <see cref="int"/> zero.</returns>
    private static bool IsZeroLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            && literal.Token.Value is 0;

    /// <summary>Returns whether an expression invokes a comparison member with a reportable argument count, syntactically.</summary>
    /// <param name="expression">The candidate operand expression.</param>
    /// <param name="invocation">The invocation when the shape matches.</param>
    /// <param name="methodName">The invoked member name when the shape matches.</param>
    /// <returns><see langword="true"/> for a simple member invocation of <c>Compare</c>, <c>CompareOrdinal</c>, or <c>CompareTo</c>.</returns>
    private static bool TryGetCallShape(ExpressionSyntax expression, out InvocationExpressionSyntax? invocation, out string? methodName)
    {
        if (expression is InvocationExpressionSyntax candidate
            && candidate.Expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name is IdentifierNameSyntax name
            && HasReportableShape(name.Identifier.ValueText, candidate.ArgumentList.Arguments.Count))
        {
            invocation = candidate;
            methodName = name.Identifier.ValueText;
            return true;
        }

        invocation = null;
        methodName = null;
        return false;
    }

    /// <summary>Returns whether a member name and argument count match one of the reported call shapes.</summary>
    /// <param name="methodName">The invoked member name.</param>
    /// <param name="argumentCount">The invocation's argument count.</param>
    /// <returns><see langword="true"/> when the name and count can be a reportable ordering call.</returns>
    private static bool HasReportableShape(string methodName, int argumentCount)
    {
        switch (methodName)
        {
            case CompareName:
            {
                return argumentCount is TwoStringArgumentCount or CompareWithOptionArgumentCount;
            }

            case CompareOrdinalName:
            {
                return argumentCount == TwoStringArgumentCount;
            }

            case CompareToName:
            {
                return argumentCount == CompareToArgumentCount;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Returns whether an invocation binds to a <see cref="string"/> ordering method the fix can rewrite.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="methodName">The invoked member name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to a reportable <see cref="string"/> comparison overload.</returns>
    private static bool BindsToStringOrderingMethod(SemanticModel model, InvocationExpressionSyntax invocation, string methodName, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        switch (methodName)
        {
            case CompareName:
            {
                return method.IsStatic && IsReportableCompareOverload(method, invocation);
            }

            case CompareOrdinalName:
            {
                return method.IsStatic && HasTwoStringParameters(method);
            }

            default:
            {
                return !method.IsStatic
                    && method.Parameters.Length == CompareToArgumentCount
                    && method.Parameters[0].Type.SpecialType == SpecialType.System_String;
            }
        }
    }

    /// <summary>Returns whether a static <c>Compare</c> binding is one of the rewritable overloads.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <param name="invocation">The invocation supplying the arguments.</param>
    /// <returns><see langword="true"/> for the two-string overload, the <see cref="StringComparison"/> overload,
    /// or the <c>ignoreCase</c> overload called with a literal flag.</returns>
    private static bool IsReportableCompareOverload(IMethodSymbol method, InvocationExpressionSyntax invocation)
    {
        if (!HasTwoStringParameters(method))
        {
            return false;
        }

        var parameters = method.Parameters;
        if (parameters.Length == TwoStringArgumentCount)
        {
            return true;
        }

        if (parameters.Length != CompareWithOptionArgumentCount)
        {
            return false;
        }

        var optionType = parameters[2].Type;
        if (optionType.SpecialType == SpecialType.System_Boolean)
        {
            var ignoreCase = invocation.ArgumentList.Arguments[2].Expression;
            return ignoreCase.IsKind(SyntaxKind.TrueLiteralExpression) || ignoreCase.IsKind(SyntaxKind.FalseLiteralExpression);
        }

        return optionType.TypeKind == TypeKind.Enum;
    }

    /// <summary>Returns whether a method's first two parameters are both strings.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> when the method compares two string arguments.</returns>
    private static bool HasTwoStringParameters(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        return parameters.Length >= TwoStringArgumentCount
            && parameters[0].Type.SpecialType == SpecialType.System_String
            && parameters[1].Type.SpecialType == SpecialType.System_String;
    }

    /// <summary>Returns the message display for a reported member name.</summary>
    /// <param name="methodName">The invoked member name.</param>
    /// <returns>The simple display of the compared call.</returns>
    private static string GetDisplay(string methodName)
    {
        switch (methodName)
        {
            case CompareName:
            {
                return CompareDisplay;
            }

            case CompareOrdinalName:
            {
                return CompareOrdinalDisplay;
            }

            default:
            {
                return CompareToName;
            }
        }
    }
}
