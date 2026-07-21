// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a general-purpose call that a purpose-built one states more plainly and does a little
/// less work than (PSH1227). Two shapes are reported:
/// <list type="bullet">
/// <item><description><c>string.Compare(a, b, StringComparison.Ordinal)</c> — the ordinal comparison
/// spelled as the culture-aware comparer told to be ordinal — becomes <c>string.CompareOrdinal(a, b)</c>.</description></item>
/// <item><description><c>Debug.Assert(false, message)</c> — an always-false assertion — becomes
/// <c>Debug.Fail(message)</c>.</description></item>
/// </list>
/// The <c>string.Compare</c> shape is left to the equality-versus-ordering rule when its result is
/// compared against zero, so the two never both fire on the same expression. Each shape is gated on
/// its replacement resolving in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1227PreferDedicatedCallAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name of the culture-aware string comparison.</summary>
    internal const string CompareName = "Compare";

    /// <summary>The invoked member name of the ordinal string comparison the fix calls.</summary>
    internal const string CompareOrdinalName = "CompareOrdinal";

    /// <summary>The invoked member name of the debug assertion.</summary>
    internal const string AssertName = "Assert";

    /// <summary>The invoked member name of the unconditional debug failure the fix calls.</summary>
    internal const string FailName = "Fail";

    /// <summary>The display of the reported <c>string.Compare</c> shape.</summary>
    private const string CompareDisplay = "string.Compare";

    /// <summary>The display of the <c>string.CompareOrdinal</c> replacement.</summary>
    private const string CompareOrdinalDisplay = "string.CompareOrdinal";

    /// <summary>The display of the reported <c>Debug.Assert(false, ...)</c> shape.</summary>
    private const string AssertDisplay = "Debug.Assert(false, ...)";

    /// <summary>The display of the <c>Debug.Fail</c> replacement.</summary>
    private const string FailDisplay = "Debug.Fail";

    /// <summary>The metadata name of the debug type carrying the assertion helpers.</summary>
    private const string DebugMetadataName = "System.Diagnostics.Debug";

    /// <summary>The underlying value of <c>StringComparison.Ordinal</c>.</summary>
    private const int OrdinalValue = 4;

    /// <summary>The argument count of the reported three-argument <c>string.Compare</c> shape.</summary>
    private const int CompareArgumentCount = 3;

    /// <summary>The zero-based index of the comparison argument in the reported <c>string.Compare</c> shape.</summary>
    private const int ComparisonArgumentIndex = 2;

    /// <summary>The parameter count of a two-string method (<c>CompareOrdinal</c>, <c>Fail(string, string)</c>).</summary>
    private const int StringPairParameterCount = 2;

    /// <summary>The lowest argument count of a reported <c>Debug.Assert(false, message)</c> shape.</summary>
    private const int AssertWithMessageArgumentCount = 2;

    /// <summary>The highest argument count of a reported <c>Debug.Assert(false, message, detail)</c> shape.</summary>
    private const int AssertWithDetailArgumentCount = 3;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.PreferDedicatedCall);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var hasCompareOrdinal = HasCompareOrdinal(start.Compilation.GetSpecialType(SpecialType.System_String));
            var debugType = start.Compilation.GetTypeByMetadataName(DebugMetadataName);
            var failArities = debugType is null ? default : GetFailArities(debugType);
            if (!hasCompareOrdinal && failArities == default)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, hasCompareOrdinal, debugType, failArities),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is the reported <c>string.Compare(a, b, StringComparison.Ordinal)</c> shape, syntactically.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the callee names <c>Compare</c> with three arguments and is not compared to zero.</returns>
    internal static bool IsCompareOrdinalShape(InvocationExpressionSyntax invocation)
        => IsSimpleCall(invocation, CompareName)
            && invocation.ArgumentList.Arguments.Count == CompareArgumentCount
            && !IsComparedToZero(invocation);

    /// <summary>Returns whether an invocation is the reported <c>Debug.Assert(false, message[, detail])</c> shape, syntactically.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the callee names <c>Assert</c>, its first argument is <see langword="false"/>, and it carries a message.</returns>
    internal static bool IsDebugFailShape(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return IsSimpleCall(invocation, AssertName)
            && arguments.Count is AssertWithMessageArgumentCount or AssertWithDetailArgumentCount
            && arguments[0].Expression.IsKind(SyntaxKind.FalseLiteralExpression);
    }

    /// <summary>Reports PSH1227 for a general-purpose call a purpose-built one supersedes.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="hasCompareOrdinal">Whether <c>string.CompareOrdinal</c> resolves.</param>
    /// <param name="debugType">The debug type carrying the assertion helpers, when it resolves.</param>
    /// <param name="failArities">The argument counts the <c>Debug.Fail</c> replacement supports.</param>
    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        bool hasCompareOrdinal,
        INamedTypeSymbol? debugType,
        FailArities failArities)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        switch ((invocation.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText)
        {
            case CompareName when hasCompareOrdinal:
            {
                AnalyzeCompare(context, invocation);
                break;
            }

            case AssertName when debugType is not null:
            {
                AnalyzeAssert(context, invocation, debugType, failArities);
                break;
            }
        }
    }

    /// <summary>Reports the <c>string.Compare</c> shape once it binds to the ordinal overload.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The candidate invocation.</param>
    private static void AnalyzeCompare(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (!IsCompareOrdinalShape(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsOrdinalCompareOverload(method)
            || context.SemanticModel.GetConstantValue(invocation.ArgumentList.Arguments[ComparisonArgumentIndex].Expression, context.CancellationToken) is not { HasValue: true, Value: OrdinalValue })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.PreferDedicatedCall,
            invocation.SyntaxTree,
            invocation.Span,
            CompareOrdinalDisplay,
            CompareDisplay));
    }

    /// <summary>Reports the <c>Debug.Assert(false, ...)</c> shape once it binds and a matching <c>Fail</c> overload exists.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="debugType">The debug type carrying the assertion helpers.</param>
    /// <param name="failArities">The argument counts the <c>Debug.Fail</c> replacement supports.</param>
    private static void AnalyzeAssert(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol debugType,
        FailArities failArities)
    {
        if (!IsDebugFailShape(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsAssertOverload(method, debugType)
            || !failArities.Supports(invocation.ArgumentList.Arguments.Count - 1))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.PreferDedicatedCall,
            invocation.SyntaxTree,
            invocation.Span,
            FailDisplay,
            AssertDisplay));
    }

    /// <summary>Returns whether an invocation is a simple member call of a given name, syntactically.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="name">The member name to match.</param>
    /// <returns><see langword="true"/> when the callee is <c>receiver.name(...)</c>.</returns>
    private static bool IsSimpleCall(InvocationExpressionSyntax invocation, string name)
        => invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == name;

    /// <summary>Returns whether the invocation is an operand of an equality test against the literal zero.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when a comparison against zero owns this call.</returns>
    /// <remarks>
    /// A <c>string.Compare(...) == 0</c> is the equality-versus-ordering rule's shape; reporting the
    /// ordinal call there too would double up on the same expression, so this shape stands down.
    /// </remarks>
    private static bool IsComparedToZero(InvocationExpressionSyntax invocation)
    {
        SyntaxNode current = invocation;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression } binary
            && (IsZeroLiteral(binary.Left) || IsZeroLiteral(binary.Right));
    }

    /// <summary>Returns whether an expression is the numeric literal <c>0</c>.</summary>
    /// <param name="expression">The candidate operand expression.</param>
    /// <returns><see langword="true"/> for a numeric literal whose value is the integer zero.</returns>
    private static bool IsZeroLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal
            && literal.Token.Value is 0;

    /// <summary>Returns whether a bound method is the ordinal-comparison overload of <c>string.Compare</c>.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> for the static <c>Compare(string, string, StringComparison)</c> overload.</returns>
    private static bool IsOrdinalCompareOverload(IMethodSymbol method)
    {
        if (!method.IsStatic || method.ContainingType.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        var parameters = method.Parameters;
        return parameters.Length == CompareArgumentCount
            && parameters[0].Type.SpecialType == SpecialType.System_String
            && parameters[1].Type.SpecialType == SpecialType.System_String
            && parameters[ComparisonArgumentIndex].Type.TypeKind == TypeKind.Enum;
    }

    /// <summary>Returns whether a bound method is a <c>Debug.Assert(bool, string[, string])</c> overload.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <param name="debugType">The debug type carrying the assertion helpers.</param>
    /// <returns><see langword="true"/> when the overload takes a bool condition and string message parameters the fix can carry over.</returns>
    private static bool IsAssertOverload(IMethodSymbol method, INamedTypeSymbol debugType)
    {
        if (!method.IsStatic || !SymbolEqualityComparer.Default.Equals(method.ContainingType, debugType))
        {
            return false;
        }

        var parameters = method.Parameters;
        if (parameters.Length is not (AssertWithMessageArgumentCount or AssertWithDetailArgumentCount)
            || parameters[0].Type.SpecialType != SpecialType.System_Boolean)
        {
            return false;
        }

        for (var i = 1; i < parameters.Length; i++)
        {
            if (parameters[i].Type.SpecialType != SpecialType.System_String)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether <c>string.CompareOrdinal(string, string)</c> exists on the string type.</summary>
    /// <param name="stringType">The compilation's string type.</param>
    /// <returns><see langword="true"/> when the ordinal comparison the fix calls resolves.</returns>
    private static bool HasCompareOrdinal(INamedTypeSymbol stringType)
    {
        var members = stringType.GetMembers(CompareOrdinalName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true, Parameters.Length: StringPairParameterCount } method
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String
                && method.Parameters[1].Type.SpecialType == SpecialType.System_String)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Collects which single- and two-string <c>Debug.Fail</c> overloads exist.</summary>
    /// <param name="debugType">The debug type carrying the assertion helpers.</param>
    /// <returns>The supported <c>Fail</c> argument counts.</returns>
    private static FailArities GetFailArities(INamedTypeSymbol debugType)
    {
        var one = false;
        var two = false;
        var members = debugType.GetMembers(FailName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol { IsStatic: true } method || !AllStringParameters(method))
            {
                continue;
            }

            one |= method.Parameters.Length == 1;
            two |= method.Parameters.Length == StringPairParameterCount;
        }

        return new FailArities(one, two);
    }

    /// <summary>Returns whether every parameter of a method is a string.</summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns><see langword="true"/> when all parameters are strings.</returns>
    private static bool AllStringParameters(IMethodSymbol method)
    {
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

    /// <summary>The message-only <c>Debug.Fail</c> overloads a project exposes.</summary>
    /// <param name="One">Whether <c>Fail(string)</c> exists.</param>
    /// <param name="Two">Whether <c>Fail(string, string)</c> exists.</param>
    private readonly record struct FailArities(bool One, bool Two)
    {
        /// <summary>Returns whether a <c>Fail</c> overload with the given string-parameter count exists.</summary>
        /// <param name="count">The number of message arguments carried over.</param>
        /// <returns><see langword="true"/> when the overload exists.</returns>
        public bool Supports(int count) => count switch
        {
            1 => One,
            StringPairParameterCount => Two,
            _ => false,
        };
    }
}
