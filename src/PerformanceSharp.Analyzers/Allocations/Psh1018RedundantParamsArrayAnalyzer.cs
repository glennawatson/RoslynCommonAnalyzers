// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a call that hands a hand-written array to a <c>params</c> parameter (PSH1018):
/// <c>Log(new[] { a, b })</c> and <c>Log(new object[] { a, b })</c> against
/// <c>Log(params object[] args)</c> allocate exactly the array the compiler would have built from
/// <c>Log(a, b)</c>. An empty one — <c>Log(new object[0])</c>, <c>Log(new object[] { })</c>,
/// <c>Log(Array.Empty&lt;object&gt;())</c> — is worse than redundant: written as <c>Log()</c> the
/// compiler reuses the shared empty array instead of allocating a fresh one.
/// </summary>
/// <remarks>
/// The rule reports only when unwrapping provably means the same call. The syntax gate wants the
/// last argument to be an inline array with no name colon and no ref kind; the bind then wants the
/// last parameter to be a <c>params</c> <em>array</em> sitting in that argument slot, and the
/// written array's type to be that exact array type — a covariant <c>string[]</c> handed to a
/// <c>params object[]</c> is passed straight through by the compiler, not rebuilt, so unwrapping it
/// would change the array's runtime type. Two guards then protect the meaning of the call:
/// <list type="bullet">
/// <item>A single element that is itself convertible to the params array type (an
/// <c>object[]</c> value, or <c>null</c>) is skipped — <c>Log(arr)</c> would pass <c>arr</c> as the
/// whole array rather than as its one element.</item>
/// <item>The unwrapped call is speculatively bound, and reported only when it still resolves to the
/// same method. An overload such as <c>Log(object a, object b)</c> alongside
/// <c>Log(params object[] args)</c> would quietly capture <c>Log(a, b)</c>, and is left alone.</item>
/// </list>
/// A <c>params</c> collection that is not an array (<c>params ReadOnlySpan&lt;T&gt;</c>,
/// <c>params List&lt;T&gt;</c>) is skipped: an array argument binds to those in normal form through
/// a conversion, so there is no compiler-built array to remove. Collection expressions are also left
/// alone; they are already the spelling that lets the compiler choose the storage.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1018RedundantParamsArrayAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the shared empty-array factory.</summary>
    private const string EmptyMethodName = "Empty";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.RedundantParamsArray);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns the call's last argument when it is an inline array that could be unwrapped, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="arrayExpression">The inline array expression in the last argument slot.</param>
    /// <returns><see langword="true"/> when the call has the hand-written-array shape.</returns>
    internal static bool TryGetArrayArgument(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out ExpressionSyntax? arrayExpression)
    {
        arrayExpression = null;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].NameColon is not null)
            {
                return false;
            }
        }

        var last = arguments[arguments.Count - 1];
        if (!last.RefKindKeyword.IsKind(SyntaxKind.None) || !IsInlineArray(last.Expression))
        {
            return false;
        }

        arrayExpression = last.Expression;
        return true;
    }

    /// <summary>Returns the elements a hand-written array carries.</summary>
    /// <param name="arrayExpression">The inline array expression; callers must have validated the shape.</param>
    /// <returns>The array's elements, empty when the array is empty.</returns>
    internal static SeparatedSyntaxList<ExpressionSyntax> GetArrayElements(ExpressionSyntax arrayExpression)
        => arrayExpression switch
        {
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
            ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
            _ => default,
        };

    /// <summary>Builds the argument list the call would have without the hand-written array.</summary>
    /// <param name="argumentList">The original argument list.</param>
    /// <param name="elements">The array's elements.</param>
    /// <returns>The argument list carrying the leading arguments followed by the array's elements.</returns>
    internal static ArgumentListSyntax BuildUnwrappedArgumentList(ArgumentListSyntax argumentList, SeparatedSyntaxList<ExpressionSyntax> elements)
    {
        var leadingCount = argumentList.Arguments.Count - 1;
        var total = leadingCount + elements.Count;
        var arguments = new ArgumentSyntax[total];
        for (var index = 0; index < leadingCount; index++)
        {
            arguments[index] = argumentList.Arguments[index];
        }

        for (var index = 0; index < elements.Count; index++)
        {
            arguments[leadingCount + index] = SyntaxFactory.Argument(elements[index].WithoutTrivia());
        }

        var separators = new SyntaxToken[total == 0 ? 0 : total - 1];
        for (var index = 0; index < separators.Length; index++)
        {
            separators[index] = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);
        }

        return argumentList.WithArguments(SyntaxFactory.SeparatedList(arguments, separators));
    }

    /// <summary>Returns whether an expression is an array the compiler could have built from the call's own arguments.</summary>
    /// <param name="expression">The last argument's expression.</param>
    /// <returns><see langword="true"/> for an inline array creation with an initializer, an empty array creation, or an empty-array call.</returns>
    private static bool IsInlineArray(ExpressionSyntax expression)
        => expression switch
        {
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Commas.Count == 0,
            ArrayCreationExpressionSyntax array => IsSingleRankArrayCreation(array),
            InvocationExpressionSyntax invocation => IsEmptyArrayCallShape(invocation),
            _ => false,
        };

    /// <summary>Returns whether an array creation is one-dimensional and carries no elements the unwrap would lose.</summary>
    /// <param name="array">The array creation to inspect.</param>
    /// <returns><see langword="true"/> when the creation has an initializer, or is an explicit zero-length array.</returns>
    private static bool IsSingleRankArrayCreation(ArrayCreationExpressionSyntax array)
    {
        var rankSpecifiers = array.Type.RankSpecifiers;
        if (rankSpecifiers.Count == 0 || rankSpecifiers[0].Sizes.Count != 1)
        {
            return false;
        }

        // Without an initializer the elements are implicit defaults, so only a zero-length array
        // has nothing to lose; 'new object[3]' is three nulls the call site never wrote.
        return array.Initializer is not null || rankSpecifiers[0].Sizes[0] is LiteralExpressionSyntax { Token.Value: 0 };
    }

    /// <summary>Returns whether an invocation looks like a call to the shared empty-array factory.</summary>
    /// <param name="invocation">The last argument's invocation.</param>
    /// <returns><see langword="true"/> for a no-argument call to a one-type-argument <c>Empty</c>.</returns>
    private static bool IsEmptyArrayCallShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => IsEmptyName(name),
                GenericNameSyntax name => IsEmptyName(name),
                _ => false,
            };

    /// <summary>Returns whether a generic name spells the shared empty-array factory.</summary>
    /// <param name="name">The invoked generic name.</param>
    /// <returns><see langword="true"/> for <c>Empty&lt;T&gt;</c>.</returns>
    private static bool IsEmptyName(GenericNameSyntax name)
        => name.Identifier.ValueText == EmptyMethodName && name.TypeArgumentList.Arguments.Count == 1;

    /// <summary>Reports a call that hands a hand-written array to a params parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetArrayArgument(invocation, out var arrayExpression))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !TryGetParamsArrayType(method, invocation, out var arrayType)
            || !IsCompilerBuildableArray(context, arrayExpression, arrayType))
        {
            return;
        }

        var elements = GetArrayElements(arrayExpression);
        if (!SurvivesUnwrap(context, invocation, method, arrayType, elements))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.RedundantParamsArray,
            arrayExpression.GetLocation(),
            method.Name));
    }

    /// <summary>Returns the params array type when the call's last argument lands in a params array slot.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="invocation">The invocation being analyzed.</param>
    /// <param name="arrayType">The params parameter's array type.</param>
    /// <returns><see langword="true"/> when the last argument is positionally the params parameter.</returns>
    private static bool TryGetParamsArrayType(IMethodSymbol method, InvocationExpressionSyntax invocation, [NotNullWhen(true)] out IArrayTypeSymbol? arrayType)
    {
        arrayType = null;
        var parameters = method.Parameters;
        if (parameters.Length == 0 || invocation.ArgumentList.Arguments.Count != parameters.Length)
        {
            return false;
        }

        var last = parameters[parameters.Length - 1];
        if (!last.IsParams || last.Type is not IArrayTypeSymbol array)
        {
            return false;
        }

        arrayType = array;
        return true;
    }

    /// <summary>Returns whether the written array is exactly the array the compiler would have built.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="arrayExpression">The inline array expression.</param>
    /// <param name="arrayType">The params parameter's array type.</param>
    /// <returns><see langword="true"/> when the array's type matches the params type and an empty-array call really is one.</returns>
    private static bool IsCompilerBuildableArray(SyntaxNodeAnalysisContext context, ExpressionSyntax arrayExpression, IArrayTypeSymbol arrayType)
    {
        if (!SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(arrayExpression, context.CancellationToken).Type, arrayType))
        {
            return false;
        }

        return arrayExpression is not InvocationExpressionSyntax
            || (context.SemanticModel.GetSymbolInfo(arrayExpression, context.CancellationToken).Symbol is IMethodSymbol { Name: EmptyMethodName } factory
                && factory.ContainingType.SpecialType == SpecialType.System_Array);
    }

    /// <summary>Returns whether removing the array leaves a call that means the same thing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The invocation being analyzed.</param>
    /// <param name="method">The bound method.</param>
    /// <param name="arrayType">The params parameter's array type.</param>
    /// <param name="elements">The array's elements.</param>
    /// <returns><see langword="true"/> when the unwrapped call still binds to the same method in expanded form.</returns>
    private static bool SurvivesUnwrap(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IArrayTypeSymbol arrayType,
        SeparatedSyntaxList<ExpressionSyntax> elements)
    {
        // One element that is itself an array of the params type would be passed as the whole
        // array, not as its single element, so the unwrapped call is a different call.
        if (elements.Count == 1)
        {
            var conversion = context.SemanticModel.ClassifyConversion(elements[0], arrayType);
            if (conversion.Exists && conversion.IsImplicit)
            {
                return false;
            }
        }

        // A call reached through a conditional access cannot be speculatively rebound: detaching it to test the
        // unwrapped argument list orphans its member or element binding and Roslyn's binder then dereferences
        // null. The unwrap stays unverified, so the explicit params array is left in place.
        if (ConditionalAccessSpeculation.ReachedThroughConditionalAccess(invocation.Expression))
        {
            return false;
        }

        var unwrapped = invocation.WithArgumentList(BuildUnwrappedArgumentList(invocation.ArgumentList, elements));
        var speculative = context.SemanticModel.GetSpeculativeSymbolInfo(invocation.SpanStart, unwrapped, SpeculativeBindingOption.BindAsExpression);
        return SymbolEqualityComparer.Default.Equals(speculative.Symbol, method);
    }
}
