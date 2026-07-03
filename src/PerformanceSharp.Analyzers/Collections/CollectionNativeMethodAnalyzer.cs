// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports LINQ predicate calls that the receiver can answer with its own native
/// methods, in a single invocation walk with syntax-first candidate filtering.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>PSH1110 — FirstOrDefault/Any/All predicate calls on <c>List&lt;T&gt;</c>, <c>ImmutableList&lt;T&gt;</c>,
/// and single-dimensional arrays should use Find/Exists/TrueForAll.</description></item>
/// <item><description>PSH1111 — Any with an equality-only predicate is a membership test and should be Contains.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionNativeMethodAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the replacement method name for the code fix.</summary>
    internal const string TargetNameKey = "TargetName";

    /// <summary>Cached diagnostic properties suggesting <c>Find</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> FindProperties = CreateTargetProperties("Find");

    /// <summary>Cached diagnostic properties suggesting <c>Exists</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> ExistsProperties = CreateTargetProperties("Exists");

    /// <summary>Cached diagnostic properties suggesting <c>TrueForAll</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> TrueForAllProperties = CreateTargetProperties("TrueForAll");

    /// <summary>Cached diagnostic properties suggesting <c>Array.Find</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> ArrayFindProperties = CreateTargetProperties("Array.Find");

    /// <summary>Cached diagnostic properties suggesting <c>Array.Exists</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> ArrayExistsProperties = CreateTargetProperties("Array.Exists");

    /// <summary>Cached diagnostic properties suggesting <c>Array.TrueForAll</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> ArrayTrueForAllProperties = CreateTargetProperties("Array.TrueForAll");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CollectionRules.UseCollectionNativePredicate,
        CollectionRules.UseContainsForMembership);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports native-predicate and membership replacements for one invocation.</summary>
    /// <param name="context">The syntax context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetCandidate(invocation, out var memberAccess, out var name, out var parameterName, out var expressionBody))
        {
            return;
        }

        if (!IsEnumerableInvocation(invocation, context.SemanticModel, context.CancellationToken, out var method)
            || context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type is not { } receiverType)
        {
            return;
        }

        if (IsMembershipTest(name.Identifier.ValueText, expressionBody, parameterName, receiverType, method))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(CollectionRules.UseContainsForMembership, name.GetLocation()));
            return;
        }

        ReportNativePredicate(context, memberAccess, name, receiverType);
    }

    /// <summary>Reports PSH1110 when the receiver ships a native replacement for the LINQ predicate call.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="memberAccess">The candidate member access.</param>
    /// <param name="name">The invoked method name.</param>
    /// <param name="receiverType">The receiver's static type.</param>
    private static void ReportNativePredicate(
        SyntaxNodeAnalysisContext context,
        MemberAccessExpressionSyntax memberAccess,
        IdentifierNameSyntax name,
        ITypeSymbol receiverType)
    {
        if (IsListWithNativePredicates(receiverType))
        {
            var target = GetListTargetName(name.Identifier.ValueText);
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CollectionRules.UseCollectionNativePredicate,
                name.SyntaxTree,
                name.Span,
                GetTargetProperties(target),
                target));
            return;
        }

        if (receiverType is not IArrayTypeSymbol { Rank: 1 })
        {
            return;
        }

        var arrayTarget = GetArrayTargetName(name.Identifier.ValueText);
        if (IsSimpleReceiver(memberAccess.Expression))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CollectionRules.UseCollectionNativePredicate,
                name.SyntaxTree,
                name.Span,
                GetTargetProperties(arrayTarget),
                arrayTarget));
            return;
        }

        // A non-trivial receiver would have to move into argument position for the
        // static System.Array rewrite, so report without the code-fix properties.
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseCollectionNativePredicate,
            name.SyntaxTree,
            name.Span,
            arrayTarget));
    }

    /// <summary>Finds a FirstOrDefault/Any/All member call whose single argument is a one-parameter lambda.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="memberAccess">The candidate member access.</param>
    /// <param name="name">The invoked method name.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="expressionBody">The lambda expression body, or <see langword="null"/> for statement bodies.</param>
    /// <returns><see langword="true"/> when the invocation matches the candidate shape.</returns>
    private static bool TryGetCandidate(
        InvocationExpressionSyntax invocation,
        out MemberAccessExpressionSyntax memberAccess,
        out IdentifierNameSyntax name,
        out string parameterName,
        out ExpressionSyntax? expressionBody)
    {
        memberAccess = null!;
        name = null!;
        parameterName = null!;
        expressionBody = null;
        if (invocation.ArgumentList.Arguments.Count != 1
            || invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax candidateName } candidateAccess
            || !candidateAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || candidateName.Identifier.ValueText is not ("FirstOrDefault" or "Any" or "All")
            || !TryGetPredicateLambda(invocation.ArgumentList.Arguments[0].Expression, out parameterName, out expressionBody))
        {
            return false;
        }

        memberAccess = candidateAccess;
        name = candidateName;
        return true;
    }

    /// <summary>Gets the parameter name and expression body of a one-parameter lambda argument.</summary>
    /// <param name="argument">The argument expression.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="expressionBody">The lambda expression body, or <see langword="null"/> for statement bodies.</param>
    /// <returns><see langword="true"/> when the argument is a one-parameter lambda.</returns>
    private static bool TryGetPredicateLambda(ExpressionSyntax argument, out string parameterName, out ExpressionSyntax? expressionBody)
    {
        switch (argument)
        {
            case SimpleLambdaExpressionSyntax simple:
                {
                    parameterName = simple.Parameter.Identifier.ValueText;
                    expressionBody = simple.ExpressionBody;
                    return true;
                }

            case ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } parenthesized:
                {
                    parameterName = parenthesized.ParameterList.Parameters[0].Identifier.ValueText;
                    expressionBody = parenthesized.ExpressionBody;
                    return true;
                }

            default:
                {
                    parameterName = null!;
                    expressionBody = null;
                    return false;
                }
        }
    }

    /// <summary>Returns whether an Any call carries an equality-only predicate the receiver can answer with Contains.</summary>
    /// <param name="methodName">The invoked method name.</param>
    /// <param name="expressionBody">The lambda expression body, or <see langword="null"/> for statement bodies.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="receiverType">The receiver's static type.</param>
    /// <param name="method">The bound Enumerable method.</param>
    /// <returns><see langword="true"/> when the call is a Contains-style membership test.</returns>
    private static bool IsMembershipTest(
        string methodName,
        ExpressionSyntax? expressionBody,
        string parameterName,
        ITypeSymbol receiverType,
        IMethodSymbol method)
    {
        if (methodName != "Any"
            || expressionBody is not BinaryExpressionSyntax equality
            || !equality.IsKind(SyntaxKind.EqualsExpression)
            || !TryGetComparedValue(equality, parameterName, out var value)
            || ReferencesParameter(value, parameterName))
        {
            return false;
        }

        return method.TypeArguments.Length == 1
            && HasAccessibleContains(receiverType, method.TypeArguments[0]);
    }

    /// <summary>Gets the non-parameter side of a <c>param == expr</c> or <c>expr == param</c> equality.</summary>
    /// <param name="equality">The equality expression.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <param name="value">The compared value expression.</param>
    /// <returns><see langword="true"/> when one side is exactly the lambda parameter.</returns>
    private static bool TryGetComparedValue(BinaryExpressionSyntax equality, string parameterName, out ExpressionSyntax value)
    {
        if (IsParameterReference(equality.Left, parameterName))
        {
            value = equality.Right;
            return true;
        }

        if (IsParameterReference(equality.Right, parameterName))
        {
            value = equality.Left;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>Returns whether an expression is exactly the lambda parameter identifier.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <returns><see langword="true"/> for a bare reference to the parameter.</returns>
    private static bool IsParameterReference(ExpressionSyntax expression, string parameterName)
        => expression is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == parameterName;

    /// <summary>Returns whether an expression mentions the lambda parameter anywhere.</summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="parameterName">The lambda parameter name.</param>
    /// <returns><see langword="true"/> when the parameter is referenced.</returns>
    private static bool ReferencesParameter(ExpressionSyntax expression, string parameterName)
    {
        if (expression is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText == parameterName;
        }

        var state = (ParameterName: parameterName, Found: false);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, (string ParameterName, bool Found)>(
            expression,
            ref state,
            static (IdentifierNameSyntax candidate, ref (string ParameterName, bool Found) current) =>
            {
                if (candidate.Identifier.ValueText != current.ParameterName)
                {
                    return true;
                }

                current.Found = true;
                return false;
            });
        return state.Found;
    }

    /// <summary>Returns whether the receiver's static type exposes an accessible instance <c>bool Contains(T)</c>.</summary>
    /// <param name="receiverType">The receiver's static type.</param>
    /// <param name="elementType">The sequence element type.</param>
    /// <returns><see langword="true"/> when the type, a base type, or an implemented interface declares the method.</returns>
    private static bool HasAccessibleContains(ITypeSymbol receiverType, ITypeSymbol elementType)
    {
        for (var current = receiverType; current is not null; current = current.BaseType)
        {
            if (HasDirectContains(current, elementType))
            {
                return true;
            }
        }

        var interfaces = receiverType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (HasDirectContains(interfaces[i], elementType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type directly declares a public instance <c>bool Contains(T)</c>.</summary>
    /// <param name="type">The type whose declared members are searched.</param>
    /// <param name="elementType">The required parameter type.</param>
    /// <returns><see langword="true"/> when a matching method is declared on <paramref name="type"/> itself.</returns>
    private static bool HasDirectContains(ITypeSymbol type, ITypeSymbol elementType)
    {
        var members = type.GetMembers("Contains");
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 1 } contains
                && SymbolEqualityComparer.Default.Equals(contains.Parameters[0].Type, elementType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <c>System.Collections.Generic.List&lt;T&gt;</c> or <c>System.Collections.Immutable.ImmutableList&lt;T&gt;</c>.</summary>
    /// <param name="type">The receiver's static type.</param>
    /// <returns><see langword="true"/> when the type ships Find/Exists/TrueForAll natively.</returns>
    private static bool IsListWithNativePredicates(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { Arity: 1 } named)
        {
            return false;
        }

        var definition = named.OriginalDefinition;
        return definition.Name switch
        {
            "List" => IsSystemCollectionsNamespace(definition.ContainingNamespace, "Generic"),
            "ImmutableList" => IsSystemCollectionsNamespace(definition.ContainingNamespace, "Immutable"),
            _ => false
        };
    }

    /// <summary>Returns whether a namespace is <c>System.Collections.&lt;leafName&gt;</c>.</summary>
    /// <param name="ns">The namespace to test.</param>
    /// <param name="leafName">The innermost namespace name.</param>
    /// <returns><see langword="true"/> for the requested System.Collections namespace.</returns>
    private static bool IsSystemCollectionsNamespace(INamespaceSymbol? ns, string leafName)
        => ns?.Name == leafName
            && ns.ContainingNamespace?.Name == "Collections"
            && ns.ContainingNamespace.ContainingNamespace?.Name == "System"
            && ns.ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;

    /// <summary>Returns whether a receiver expression can move into argument position without re-evaluation concerns.</summary>
    /// <param name="expression">The receiver expression.</param>
    /// <returns><see langword="true"/> for identifiers, <c>this</c>, and simple member-access chains over them.</returns>
    private static bool IsSimpleReceiver(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                    {
                        return true;
                    }

                case MemberAccessExpressionSyntax memberAccess when memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    {
                        current = memberAccess.Expression;
                        break;
                    }

                default:
                    {
                        return false;
                    }
            }
        }
    }

    /// <summary>Maps a LINQ predicate method to the List/ImmutableList native method name.</summary>
    /// <param name="methodName">The invoked method name.</param>
    /// <returns>The native method name.</returns>
    private static string GetListTargetName(string methodName)
        => methodName switch
        {
            "FirstOrDefault" => "Find",
            "Any" => "Exists",
            _ => "TrueForAll"
        };

    /// <summary>Maps a LINQ predicate method to the static <c>System.Array</c> helper name.</summary>
    /// <param name="methodName">The invoked method name.</param>
    /// <returns>The static helper name.</returns>
    private static string GetArrayTargetName(string methodName)
        => methodName switch
        {
            "FirstOrDefault" => "Array.Find",
            "Any" => "Array.Exists",
            _ => "Array.TrueForAll"
        };

    /// <summary>Gets the cached diagnostic properties carrying a replacement target name.</summary>
    /// <param name="target">The replacement target name.</param>
    /// <returns>The cached properties.</returns>
    private static ImmutableDictionary<string, string?> GetTargetProperties(string target)
        => target switch
        {
            "Find" => FindProperties,
            "Exists" => ExistsProperties,
            "TrueForAll" => TrueForAllProperties,
            "Array.Find" => ArrayFindProperties,
            "Array.Exists" => ArrayExistsProperties,
            _ => ArrayTrueForAllProperties
        };

    /// <summary>Creates the diagnostic properties carrying a replacement target name.</summary>
    /// <param name="target">The replacement target name.</param>
    /// <returns>The properties dictionary.</returns>
    private static ImmutableDictionary<string, string?> CreateTargetProperties(string target)
        => ImmutableDictionary<string, string?>.Empty.Add(TargetNameKey, target);

    /// <summary>Returns whether the invocation resolves to <see cref="System.Linq.Enumerable"/>.</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> when the target is an in-memory LINQ method.</returns>
    private static bool IsEnumerableInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out IMethodSymbol method)
    {
        method = null!;
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol bound)
        {
            return false;
        }

        var original = bound.ReducedFrom ?? bound;
        if (!IsSystemLinqEnumerable(original.ContainingType))
        {
            return false;
        }

        method = bound;
        return true;
    }

    /// <summary>Returns whether a named type is <c>System.Linq.Enumerable</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for <c>System.Linq.Enumerable</c>.</returns>
    private static bool IsSystemLinqEnumerable(INamedTypeSymbol? type)
        => type?.Name == "Enumerable"
            && type.ContainingNamespace?.Name == "Linq"
            && type.ContainingNamespace.ContainingNamespace?.Name == "System"
            && type.ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;
}
