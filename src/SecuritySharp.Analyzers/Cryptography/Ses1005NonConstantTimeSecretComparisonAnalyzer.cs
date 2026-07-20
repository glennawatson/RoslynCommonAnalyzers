// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a non-constant-time comparison of a secret (SES1005). The rule reports an <c>==</c>/<c>!=</c>,
/// an <c>.Equals</c>/<c>object.Equals</c>, or an <c>Enumerable</c>/<c>MemoryExtensions</c>/<c>string</c>
/// <c>SequenceEqual</c> when one operand is named like a secret and both operands are <c>byte[]</c>, a
/// byte span, or <c>string</c>. Detection is a curated, high-precision name-and-type heuristic (near-zero
/// false positives): an operand's identifier or member name must contain one of <c>hmac</c>,
/// <c>signature</c>, <c>sig</c>, <c>mac</c>, <c>tag</c>, <c>token</c>, <c>hash</c>, <c>digest</c>,
/// <c>secret</c>, or -- only inside a verify/validate/check-shaped method -- <c>expected</c>/<c>actual</c>.
/// A comparison against a compile-time constant (for example <c>token == ""</c>) is not the
/// attacker-versus-secret shape and is never reported. The rule is gated on
/// <c>System.Security.Cryptography.CryptographicOperations</c> resolving, so a target framework without
/// <c>FixedTimeEquals</c> pays nothing and never receives a diagnostic it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1005NonConstantTimeSecretComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the type whose presence gates the rule and hosts the suggested fix.</summary>
    private const string CryptographicOperationsMetadataName = "System.Security.Cryptography.CryptographicOperations";

    /// <summary>The name of the instance/static equality method that is inspected.</summary>
    private const string EqualsMethodName = "Equals";

    /// <summary>The name of the sequence-equality method that is inspected.</summary>
    private const string SequenceEqualMethodName = "SequenceEqual";

    /// <summary>The argument count of the static two-operand comparison form (a, b).</summary>
    private const int StaticComparisonArgumentCount = 2;

    /// <summary>The argument count of the instance/reduced-extension comparison form (b), the receiver being the first operand.</summary>
    private const int InstanceComparisonArgumentCount = 1;

    /// <summary>The curated, high-precision fragments that mark an operand name as a secret.</summary>
    private static readonly string[] SecretNameFragments =
    [
        "hmac",
        "signature",
        "sig",
        "mac",
        "tag",
        "token",
        "hash",
        "digest",
        "secret",
    ];

    /// <summary>The fragments that mark an enclosing method as verify/validate/check-shaped.</summary>
    private static readonly string[] VerifyMethodFragments =
    [
        "verify",
        "validate",
        "check",
        "compare",
        "authenticate",
        "match",
    ];

    /// <summary>The names that only count as a secret inside a verify/validate/check-shaped method.</summary>
    private static readonly string[] ExpectationNameFragments =
    [
        "expected",
        "actual",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArrays.Of(SecurityRules.NonConstantTimeSecretComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Gate the whole rule on the suggested API: without CryptographicOperations there is no
            // FixedTimeEquals to recommend, so nothing is registered and the clean path costs nothing.
            if (start.Compilation.GetTypeByMetadataName(CryptographicOperationsMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeBinary, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Resolves the two <c>SequenceEqual</c> byte-buffer operands a code fix can rewrite to <c>FixedTimeEquals</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The reported invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="left">The first comparison operand.</param>
    /// <param name="right">The second comparison operand.</param>
    /// <returns><see langword="true"/> when the invocation is a plain byte-buffer <c>SequenceEqual</c>.</returns>
    internal static bool TryGetFixableByteComparison(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken,
        out ExpressionSyntax left,
        out ExpressionSyntax right)
    {
        left = null!;
        right = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name.Identifier.ValueText != SequenceEqualMethodName
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { Name: SequenceEqualMethodName } method
            || method.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            return false;
        }

        // Fix only the plain content comparison. The IEqualityComparer overloads carry an extra
        // argument and have no constant-time twin, and both operands must be byte buffers.
        if (!IsPlainContentComparison(invocation, method)
            || !TryGetComparisonOperands(invocation, member, method, out var first, out var second)
            || !IsByteBuffer(model.GetTypeInfo(first, cancellationToken).Type)
            || !IsByteBuffer(model.GetTypeInfo(second, cancellationToken).Type))
        {
            return false;
        }

        left = first;
        right = second;
        return true;
    }

    /// <summary>Returns whether a <c>SequenceEqual</c> call is the plain two-buffer form with no comparer argument.</summary>
    /// <param name="invocation">The invocation being inspected.</param>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> when only the two buffers are passed.</returns>
    private static bool IsPlainContentComparison(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var argumentCount = invocation.ArgumentList.Arguments.Count;
        return IsStaticTwoOperandForm(method)
            ? argumentCount == StaticComparisonArgumentCount
            : argumentCount == InstanceComparisonArgumentCount;
    }

    /// <summary>Reports SES1005 for a secret compared with <c>==</c> or <c>!=</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeBinary(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;

        // Cheap syntactic prefilter: at least one operand is named like a secret.
        if (PickSecretName(binary, GetOperandName(binary.Left), GetOperandName(binary.Right)) is not { } secretName)
        {
            return;
        }

        if (!IsGuardedComparison(context, binary.Left, binary.Right))
        {
            return;
        }

        Report(context, binary, secretName);
    }

    /// <summary>Reports SES1005 for a secret compared with <c>.Equals</c>, <c>object.Equals</c>, or <c>SequenceEqual</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.Equals(...)'/'.SequenceEqual(...)' call carrying at least one argument.
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var methodName = member.Name.Identifier.ValueText;
        if ((methodName != EqualsMethodName && methodName != SequenceEqualMethodName)
            || !HasCandidateSecretName(invocation, member))
        {
            return;
        }

        if (!TryResolveComparisonOperands(context, invocation, member, out var left, out var right))
        {
            return;
        }

        // Authoritative name check on the two resolved operands (the syntactic pass also scans the receiver,
        // which is a type for the static forms).
        if (PickSecretName(invocation, GetOperandName(left), GetOperandName(right)) is not { } secretName
            || !IsGuardedComparison(context, left, right))
        {
            return;
        }

        Report(context, invocation, secretName);
    }

    /// <summary>Binds an <c>Equals</c>/<c>SequenceEqual</c> invocation and resolves its two comparison operands.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The invocation being inspected.</param>
    /// <param name="member">The invocation's member-access expression.</param>
    /// <param name="left">The first comparison operand.</param>
    /// <param name="right">The second comparison operand.</param>
    /// <returns><see langword="true"/> when the call is a bool-returning equality with two operands.</returns>
    private static bool TryResolveComparisonOperands(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        out ExpressionSyntax left,
        out ExpressionSyntax right)
    {
        left = null!;
        right = null!;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.ReturnType.SpecialType != SpecialType.System_Boolean
            || (method.Name != EqualsMethodName && method.Name != SequenceEqualMethodName))
        {
            return false;
        }

        return TryGetComparisonOperands(invocation, member, method, out left, out right);
    }

    /// <summary>Returns whether both operands are a secret-comparable type and neither is a compile-time constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="left">The first comparison operand.</param>
    /// <param name="right">The second comparison operand.</param>
    /// <returns><see langword="true"/> when the comparison is the attacker-versus-secret shape.</returns>
    private static bool IsGuardedComparison(SyntaxNodeAnalysisContext context, ExpressionSyntax left, ExpressionSyntax right)
    {
        if (!IsSecretComparableType(context.SemanticModel.GetTypeInfo(left, context.CancellationToken).Type)
            || !IsSecretComparableType(context.SemanticModel.GetTypeInfo(right, context.CancellationToken).Type))
        {
            return false;
        }

        // A comparison against a constant (for example token == "") is an emptiness or hard-coded check,
        // not a comparison of two runtime secrets, and has no constant-time equivalent.
        return !context.SemanticModel.GetConstantValue(left, context.CancellationToken).HasValue
            && !context.SemanticModel.GetConstantValue(right, context.CancellationToken).HasValue;
    }

    /// <summary>Returns whether the receiver or any argument is named like a secret, as a cheap prefilter.</summary>
    /// <param name="invocation">The invocation being inspected.</param>
    /// <param name="member">The invocation's member-access expression.</param>
    /// <returns><see langword="true"/> when a candidate operand is named like a secret.</returns>
    private static bool HasCandidateSecretName(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax member)
    {
        if (PickSecretName(invocation, GetOperandName(member.Expression), null) is not null)
        {
            return true;
        }

        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (PickSecretName(invocation, GetOperandName(arguments[i].Expression), null) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines the two comparison operands for an <c>Equals</c>/<c>SequenceEqual</c> invocation.</summary>
    /// <param name="invocation">The invocation being inspected.</param>
    /// <param name="member">The invocation's member-access expression.</param>
    /// <param name="method">The bound method symbol.</param>
    /// <param name="left">The first comparison operand.</param>
    /// <param name="right">The second comparison operand.</param>
    /// <returns><see langword="true"/> when both operands were identified positionally.</returns>
    private static bool TryGetComparisonOperands(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        IMethodSymbol method,
        out ExpressionSyntax left,
        out ExpressionSyntax right)
    {
        var arguments = invocation.ArgumentList.Arguments;

        // Static two-operand form: object.Equals(a, b), Enumerable/MemoryExtensions.SequenceEqual(a, b).
        if (IsStaticTwoOperandForm(method))
        {
            if (arguments.Count >= 2 && arguments[0].NameColon is null && arguments[1].NameColon is null)
            {
                left = arguments[0].Expression;
                right = arguments[1].Expression;
                return true;
            }

            left = null!;
            right = null!;
            return false;
        }

        // Instance or reduced-extension form: a.Equals(b), a.SequenceEqual(b).
        left = member.Expression;
        right = arguments[0].Expression;
        return true;
    }

    /// <summary>Returns whether the bound method is called in the static two-operand form.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> for a non-reduced static call whose operands are its arguments.</returns>
    private static bool IsStaticTwoOperandForm(IMethodSymbol method)
        => method.IsStatic && method.MethodKind != MethodKind.ReducedExtension;

    /// <summary>Returns the secret operand name, honouring the verify-method guard for expectation names.</summary>
    /// <param name="node">The comparison node, used to locate the enclosing method for the guard.</param>
    /// <param name="leftName">The first operand's name, or <see langword="null"/>.</param>
    /// <param name="rightName">The second operand's name, or <see langword="null"/>.</param>
    /// <returns>The secret operand's name, or <see langword="null"/> when neither qualifies.</returns>
    private static string? PickSecretName(SyntaxNode node, string? leftName, string? rightName)
    {
        if (ContainsAnyFragment(leftName, SecretNameFragments))
        {
            return leftName;
        }

        if (ContainsAnyFragment(rightName, SecretNameFragments))
        {
            return rightName;
        }

        // 'expected'/'actual' are ordinary in test asserts, so they only count as a secret inside a
        // verify/validate/check-shaped method.
        var leftExpectation = ContainsAnyFragment(leftName, ExpectationNameFragments);
        var rightExpectation = ContainsAnyFragment(rightName, ExpectationNameFragments);
        if (!leftExpectation && !rightExpectation)
        {
            return null;
        }

        if (!IsInVerifyShapedMethod(node))
        {
            return null;
        }

        return leftExpectation ? leftName : rightName;
    }

    /// <summary>Extracts the rightmost meaningful identifier from a comparison operand expression.</summary>
    /// <param name="expression">The operand expression.</param>
    /// <returns>The operand name, or <see langword="null"/> when none can be identified.</returns>
    private static string? GetOperandName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        InvocationExpressionSyntax invocation => GetOperandName(invocation.Expression),
        _ => null,
    };

    /// <summary>Returns whether a name contains any of the supplied fragments, case-insensitively and without allocating.</summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="fragments">The fragments to look for.</param>
    /// <returns><see langword="true"/> when the name contains a fragment.</returns>
    private static bool ContainsAnyFragment(string? name, string[] fragments)
    {
        if (name is null)
        {
            return false;
        }

        for (var i = 0; i < fragments.Length; i++)
        {
            if (name.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the nearest enclosing method or local function is verify/validate/check-shaped.</summary>
    /// <param name="node">The comparison node.</param>
    /// <returns><see langword="true"/> when the enclosing method name marks a verification routine.</returns>
    private static bool IsInVerifyShapedMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            var name = current switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                LocalFunctionStatementSyntax localFunction => localFunction.Identifier.ValueText,
                _ => null,
            };

            if (name is null)
            {
                continue;
            }

            return ContainsAnyFragment(name, VerifyMethodFragments);
        }

        return false;
    }

    /// <summary>Returns whether a type is one the constant-time comparison heuristic covers.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> for <c>byte[]</c>, a byte span, or <c>string</c>.</returns>
    private static bool IsSecretComparableType(ITypeSymbol? type)
        => type is { SpecialType: SpecialType.System_String } || IsByteBuffer(type);

    /// <summary>Returns whether a type is a byte buffer that <c>FixedTimeEquals</c> can accept.</summary>
    /// <param name="type">The operand type.</param>
    /// <returns><see langword="true"/> for <c>byte[]</c> or a byte span.</returns>
    private static bool IsByteBuffer(ITypeSymbol? type) => type switch
    {
        IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => true,
        INamedTypeSymbol named => IsByteSpan(named),
        _ => false,
    };

    /// <summary>Returns whether a named type is <c>System.ReadOnlySpan&lt;byte&gt;</c> or <c>System.Span&lt;byte&gt;</c>.</summary>
    /// <param name="type">The named type.</param>
    /// <returns><see langword="true"/> for a byte span.</returns>
    private static bool IsByteSpan(INamedTypeSymbol type)
        => (type.Name == "ReadOnlySpan" || type.Name == "Span")
            && type.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
            && type.TypeArguments.Length == 1
            && type.TypeArguments[0].SpecialType == SpecialType.System_Byte;

    /// <summary>Reports SES1005 for a comparison node with the secret operand's name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The comparison node to report.</param>
    /// <param name="secretName">The secret operand's name.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode node, string secretName)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonConstantTimeSecretComparison,
            node.SyntaxTree,
            node.Span,
            secretName));
}
