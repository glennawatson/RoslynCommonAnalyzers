// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a <c>string.Concat</c> call that materializes a substring only to concatenate it
/// (PSH1222) — <c>string.Concat(a.Substring(i), b)</c>. The substring is allocated, the characters are
/// copied into it, and then they are copied a second time into the result. The span overloads of
/// <c>string.Concat</c> take the slice itself and copy once.
/// </summary>
/// <remarks>
/// <para>
/// <b>All the arguments move, or none do.</b> <c>string.Concat</c> has no overload that mixes a
/// <see cref="string"/> with a <c>ReadOnlySpan&lt;char&gt;</c>, so the rewrite has to turn every
/// argument into a span: the slices become <c>AsSpan(i)</c> and the plain strings become
/// <c>AsSpan()</c>. That is still a strict improvement — <c>AsSpan()</c> on a string allocates
/// nothing, and a <see langword="null"/> string yields an empty span, which is exactly what
/// <c>Concat</c> already did with it.
/// </para>
/// <para>
/// Only the all-<see cref="string"/> overloads of two to four arguments are reported. The
/// <c>object</c> and <c>params</c> overloads have no span counterpart, and a concatenation written
/// with <c>+</c> is left to the compiler. The span overloads did not exist before .NET Core 2.1, so
/// the rule is switched off at compilation start when they are absent and the rewritten call is bound
/// speculatively before anything is reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1222UseSpanBasedConcatAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The concatenated member name the syntax gate requires.</summary>
    internal const string ConcatMethodName = "Concat";

    /// <summary>The sliced member name that makes a concatenation worth reporting.</summary>
    internal const string SubstringMethodName = "Substring";

    /// <summary>The span slice method name.</summary>
    internal const string AsSpanMethodName = "AsSpan";

    /// <summary>The fewest arguments a reported concatenation carries.</summary>
    private const int MinConcatArguments = 2;

    /// <summary>The most arguments a span <c>Concat</c> overload accepts.</summary>
    private const int MaxConcatArguments = 4;

    /// <summary>The fewest arguments a <c>Substring</c> call carries.</summary>
    private const int MinSubstringArguments = 1;

    /// <summary>The most arguments a <c>Substring</c> call carries.</summary>
    private const int MaxSubstringArguments = 2;

    /// <summary>The metadata name of the extensions type providing <c>AsSpan</c>.</summary>
    private const string MemoryExtensionsMetadataName = "System.MemoryExtensions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseSpanBasedConcat);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (!HasSpanConcat(start.Compilation)
                || start.Compilation.GetTypeByMetadataName(MemoryExtensionsMetadataName) is not { } extensions
                || extensions.GetMembers(AsSpanMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeConcat, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a <c>Concat</c> of two to four arguments, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsConcatShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count is >= MinConcatArguments and <= MaxConcatArguments
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == ConcatMethodName
            && HasSubstringArgument(invocation)
            && !HasNamedOrModifiedArgument(invocation);

    /// <summary>Returns the first argument that is a <c>Substring</c> call, or <see langword="null"/>.</summary>
    /// <param name="invocation">The concatenation.</param>
    /// <returns>The first sliced argument.</returns>
    internal static InvocationExpressionSyntax? FindFirstSubstring(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (IsSubstringCall(arguments[i].Expression))
            {
                return (InvocationExpressionSyntax)arguments[i].Expression;
            }
        }

        return null;
    }

    /// <summary>Builds the all-span rewrite of a reported concatenation.</summary>
    /// <param name="invocation">The reported concatenation.</param>
    /// <returns>The concatenation whose arguments are all spans.</returns>
    internal static InvocationExpressionSyntax BuildSpanConcat(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var rewritten = new ArgumentSyntax[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            rewritten[i] = SyntaxFactory.Argument(ToSpan(arguments[i].Expression.WithoutTrivia()));
        }

        return invocation.WithArgumentList(
            invocation.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(rewritten)));
    }

    /// <summary>Turns one concatenated argument into the span the overload wants.</summary>
    /// <param name="expression">The original argument expression.</param>
    /// <returns>The span-producing expression.</returns>
    /// <remarks>
    /// A <c>Substring</c> becomes the <c>AsSpan</c> that produces the same slice with its arguments
    /// untouched; anything else keeps its value and gains a zero-cost <c>AsSpan()</c>.
    /// </remarks>
    private static InvocationExpressionSyntax ToSpan(ExpressionSyntax expression)
    {
        if (IsSubstringCall(expression))
        {
            var slice = (InvocationExpressionSyntax)expression;
            var access = (MemberAccessExpressionSyntax)slice.Expression;
            return slice.WithExpression(access.WithName(SyntaxFactory.IdentifierName(AsSpanMethodName)));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                NeedsParentheses(expression) ? SyntaxFactory.ParenthesizedExpression(expression) : expression,
                SyntaxFactory.IdentifierName(AsSpanMethodName)));
    }

    /// <summary>Returns whether an expression must be parenthesized before a member access is appended.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when <c>.AsSpan()</c> would otherwise bind to the wrong operand.</returns>
    private static bool NeedsParentheses(ExpressionSyntax expression)
        => expression is not (IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or ThisExpressionSyntax);

    /// <summary>Returns whether an expression is a plain <c>x.Substring(...)</c> call.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    private static bool IsSubstringCall(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax invocation
            && invocation.ArgumentList.Arguments.Count is >= MinSubstringArguments and <= MaxSubstringArguments
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == SubstringMethodName;

    /// <summary>Returns whether any argument is a <c>Substring</c> call.</summary>
    /// <param name="invocation">The concatenation.</param>
    /// <returns><see langword="true"/> when at least one argument is sliced.</returns>
    private static bool HasSubstringArgument(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (IsSubstringCall(arguments[i].Expression))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any argument carries a name colon or a ref-kind keyword.</summary>
    /// <param name="invocation">The concatenation.</param>
    /// <returns><see langword="true"/> when an argument cannot be moved positionally.</returns>
    private static bool HasNamedOrModifiedArgument(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null || !arguments[i].RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether <see cref="string"/> declares an all-span <c>Concat</c> overload.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when the span overloads exist.</returns>
    private static bool HasSpanConcat(Compilation compilation)
    {
        foreach (var member in compilation.GetSpecialType(SpecialType.System_String).GetMembers(ConcatMethodName))
        {
            if (member is IMethodSymbol { IsStatic: true, Parameters.Length: MinConcatArguments } method
                && IsCharSpan(method.Parameters[0].Type)
                && IsCharSpan(method.Parameters[1].Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <c>ReadOnlySpan&lt;char&gt;</c>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for a read-only char span.</returns>
    private static bool IsCharSpan(ITypeSymbol type)
        => type is INamedTypeSymbol
        {
            Name: "ReadOnlySpan",
            IsGenericType: true,
            TypeArguments: [{ SpecialType: SpecialType.System_Char }],
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };

    /// <summary>Reports PSH1222 for a concatenation that materializes a slice it did not need.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConcat(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsConcatShape(invocation))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (!BindsToStringConcat(model, invocation, cancellationToken)
            || FindFirstSubstring(invocation) is not { } slice
            || !SlicesAString(model, slice, cancellationToken)
            || SpanRewriteGuard.IsInsideExpressionTree(invocation, model, cancellationToken)
            || !RewriteBindsToSpanConcat(model, invocation, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseSpanBasedConcat,
            invocation.SyntaxTree,
            invocation.Span,
            slice.ToString()));
    }

    /// <summary>Returns whether the call is an all-<see cref="string"/> <c>string.Concat</c> overload.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The concatenation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when every parameter is a plain string.</returns>
    private static bool BindsToStringConcat(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol concat
            || !concat.IsStatic
            || concat.IsGenericMethod
            || concat.Name != ConcatMethodName
            || concat.ContainingType.SpecialType != SpecialType.System_String
            || concat.Parameters.Length != invocation.ArgumentList.Arguments.Count)
        {
            return false;
        }

        foreach (var parameter in concat.Parameters)
        {
            if (parameter.IsParams || parameter.Type.SpecialType != SpecialType.System_String)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a sliced argument is a <see cref="string"/> <c>Substring</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="slice">The sliced argument.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the slice is the framework's own <c>Substring</c>.</returns>
    private static bool SlicesAString(SemanticModel model, InvocationExpressionSyntax slice, CancellationToken cancellationToken)
        => model.GetSymbolInfo(slice, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: false,
            Name: SubstringMethodName,
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>Confirms the all-span rewrite binds to a <c>string.Concat</c> overload.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The reported concatenation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the fix compiles.</returns>
    private static bool RewriteBindsToSpanConcat(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        // Speculative binding is the most expensive step in the rule and has no cancellable overload,
        // so the token is honoured on the way in instead.
        cancellationToken.ThrowIfCancellationRequested();

        var rewritten = BuildSpanConcat(invocation);
        var symbol = model.GetSpeculativeSymbolInfo(invocation.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol;
        if (symbol is not IMethodSymbol { IsStatic: true, Name: ConcatMethodName, ReturnType.SpecialType: SpecialType.System_String } resolved
            || resolved.ContainingType.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        foreach (var parameter in resolved.Parameters)
        {
            if (!IsCharSpan(parameter.Type))
            {
                return false;
            }
        }

        return true;
    }
}
