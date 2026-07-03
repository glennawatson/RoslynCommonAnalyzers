// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports string comparisons that allocate case-converted copies of both operands
/// (PSH1200). A comparison qualifies when both sides call the same parameterless
/// case-conversion method (<c>ToLower</c>, <c>ToUpper</c>, <c>ToLowerInvariant</c>,
/// <c>ToUpperInvariant</c>) on <see cref="string"/> and the results feed <c>==</c>,
/// <c>!=</c>, instance <c>Equals(string)</c>, or static <c>string.Equals(a, b)</c>.
/// Single-sided conversions are deliberately not reported — rewriting them to a
/// <c>StringComparison</c> overload changes which operand's casing wins, so the rule
/// stays on the shapes where the fix is behavior-preserving in intent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1200AvoidCaseConversionComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.AvoidCaseConversionComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeBinary, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns whether an expression is a parameterless case-conversion invocation, syntactically.</summary>
    /// <param name="expression">The candidate operand expression.</param>
    /// <param name="conversion">The matched conversion invocation when the probe succeeds.</param>
    /// <param name="methodName">The conversion method name (for example <c>ToLower</c>).</param>
    /// <returns><see langword="true"/> for an argument-free <c>x.ToXxx()</c> member invocation.</returns>
    internal static bool TryGetCaseConversion(ExpressionSyntax expression, out InvocationExpressionSyntax? conversion, out string? methodName)
    {
        if (expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation
            && invocation.Expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name is IdentifierNameSyntax name
            && IsCaseConversionName(name.Identifier.ValueText))
        {
            conversion = invocation;
            methodName = name.Identifier.ValueText;
            return true;
        }

        conversion = null;
        methodName = null;
        return false;
    }

    /// <summary>Extracts both case-converted operands of an <c>Equals</c> invocation, syntactically.</summary>
    /// <param name="invocation">The <c>Equals</c> invocation.</param>
    /// <param name="access">The invocation's member access.</param>
    /// <param name="left">The left conversion invocation when the shape matches.</param>
    /// <param name="right">The right conversion invocation when the shape matches.</param>
    /// <param name="methodName">The shared conversion method name.</param>
    /// <returns><see langword="true"/> when both operands are conversions with the same method name.</returns>
    internal static bool TryGetEqualsOperands(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        out InvocationExpressionSyntax? left,
        out InvocationExpressionSyntax? right,
        out string? methodName)
    {
        var arguments = invocation.ArgumentList.Arguments;
        left = null;
        right = null;
        methodName = null;
        string? leftName;
        string? rightName;

        if (arguments.Count == 1)
        {
            if (!TryGetCaseConversion(access.Expression, out left, out leftName)
                || !TryGetCaseConversion(arguments[0].Expression, out right, out rightName))
            {
                return false;
            }
        }
        else if (arguments.Count == 2)
        {
            if (!TryGetCaseConversion(arguments[0].Expression, out left, out leftName)
                || !TryGetCaseConversion(arguments[1].Expression, out right, out rightName))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (!string.Equals(leftName, rightName, StringComparison.Ordinal))
        {
            return false;
        }

        methodName = leftName;
        return true;
    }

    /// <summary>Reports PSH1200 on <c>==</c>/<c>!=</c> whose operands are matching case conversions.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryGetCaseConversion(binary.Left, out var left, out var leftName)
            || !TryGetCaseConversion(binary.Right, out var right, out var rightName)
            || !string.Equals(leftName, rightName, StringComparison.Ordinal)
            || !IsStringCaseConversion(context.SemanticModel, left!, context.CancellationToken)
            || !IsStringCaseConversion(context.SemanticModel, right!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.AvoidCaseConversionComparison,
            binary.SyntaxTree,
            binary.OperatorToken.Span,
            leftName!));
    }

    /// <summary>Reports PSH1200 on <c>Equals</c> calls whose operands are matching case conversions.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax { Identifier.ValueText: "Equals" }
            || !TryGetEqualsOperands(invocation, access, out var left, out var right, out var methodName))
        {
            return;
        }

        if (!IsStringEqualsMethod(context.SemanticModel, invocation, invocation.ArgumentList.Arguments.Count, context.CancellationToken)
            || !IsStringCaseConversion(context.SemanticModel, left!, context.CancellationToken)
            || !IsStringCaseConversion(context.SemanticModel, right!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.AvoidCaseConversionComparison,
            invocation.SyntaxTree,
            access.Name.Span,
            methodName!));
    }

    /// <summary>Returns whether a name is one of the parameterless string case-conversion methods.</summary>
    /// <param name="name">The member name to test.</param>
    /// <returns><see langword="true"/> for the four case-conversion method names.</returns>
    private static bool IsCaseConversionName(string name)
        => name is "ToLower" or "ToUpper" or "ToLowerInvariant" or "ToUpperInvariant";

    /// <summary>Returns whether a conversion invocation binds to a parameterless instance method on <see cref="string"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="conversion">The conversion invocation to bind.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation is a real string case conversion.</returns>
    private static bool IsStringCaseConversion(SemanticModel model, InvocationExpressionSyntax conversion, CancellationToken cancellationToken)
        => model.GetSymbolInfo(conversion, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: false,
            Parameters.Length: 0,
            ContainingType.SpecialType: SpecialType.System_String
        };

    /// <summary>Returns whether an <c>Equals</c> invocation binds to the expected string equality overload.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The <c>Equals</c> invocation to bind.</param>
    /// <param name="argumentCount">The argument count (1 = instance shape, 2 = static shape).</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for instance <c>Equals(string)</c> or static <c>Equals(string, string)</c>.</returns>
    private static bool IsStringEqualsMethod(SemanticModel model, InvocationExpressionSyntax invocation, int argumentCount, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String
            || method.Parameters.Length != argumentCount
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        return argumentCount == 1
            ? !method.IsStatic
            : method.IsStatic && method.Parameters[1].Type.SpecialType == SpecialType.System_String;
    }
}
