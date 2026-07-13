// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a prefix test written as a search (PSH1221) — <c>text.IndexOf(value) == 0</c> and its
/// <c>!= 0</c> twin. <c>IndexOf</c> walks the whole string looking for a match it is going to reject
/// everywhere except position zero; <c>StartsWith</c> compares at position zero and stops. On a long
/// string with no match the difference is the whole string.
/// </summary>
/// <remarks>
/// <para>
/// <b>The comparison basis never moves.</b> This is the rule where culture bites, so the invariant is
/// stated plainly: whatever comparison the <c>IndexOf</c> call was doing, the <c>StartsWith</c> call
/// does the same one. Three shapes qualify and no others.
/// </para>
/// <list type="bullet">
/// <item>
/// <c>IndexOf(char)</c> is ordinal and <c>StartsWith(char)</c> is ordinal, so the pair carries across
/// exactly — but <c>StartsWith(char)</c> does not exist on netstandard2.0 or .NET Framework, so this
/// shape is reported only where the overload is really there.
/// </item>
/// <item>
/// <c>IndexOf(string, StringComparison)</c> hands the very same <see cref="StringComparison"/> to
/// <c>StartsWith(string, StringComparison)</c>. Ordinal stays ordinal; a culture-sensitive comparison
/// stays culture-sensitive.
/// </item>
/// <item>
/// <c>IndexOf(string)</c> and <c>StartsWith(string)</c> are <i>both</i> current-culture searches, so
/// the rewrite leaves the basis exactly where the author put it. It is never turned into an ordinal
/// comparison — that would be a behavior change dressed up as an optimization, and
/// <see cref="Psh1207SpecifyStringComparisonAnalyzer"/> is the rule that asks for an explicit
/// <see cref="StringComparison"/> there.
/// </item>
/// </list>
/// <para>
/// Everything else is refused: an <c>IndexOf</c> that takes a start index is not asking a prefix
/// question at all, and <c>IndexOf(char, StringComparison)</c> has no <c>StartsWith</c> counterpart to
/// carry its comparison. The rewritten call is bound speculatively before the rule fires, so an
/// overload the target framework lacks is never suggested.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1221UseStartsWithOverIndexOfAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The searched member name the syntax gate requires.</summary>
    internal const string IndexOfMethodName = "IndexOf";

    /// <summary>The replacement member name.</summary>
    internal const string StartsWithMethodName = "StartsWith";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseStartsWithOverIndexOf);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var overloads = StartsWithOverloads.Resolve(start.Compilation);
            if (!overloads.HasAny)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeComparison(nodeContext, overloads),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression);
        });
    }

    /// <summary>Splits a <c>x.IndexOf(...) == 0</c> comparison into its search, syntactically.</summary>
    /// <param name="comparison">The candidate comparison.</param>
    /// <returns>The <c>IndexOf</c> invocation, or <see langword="null"/> when the shape does not match.</returns>
    internal static InvocationExpressionSyntax? TryGetIndexOfCall(BinaryExpressionSyntax comparison)
    {
        if (IsZero(comparison.Right) && IsIndexOfShape(comparison.Left))
        {
            return (InvocationExpressionSyntax)comparison.Left;
        }

        return IsZero(comparison.Left) && IsIndexOfShape(comparison.Right)
            ? (InvocationExpressionSyntax)comparison.Right
            : null;
    }

    /// <summary>Builds the <c>StartsWith</c> rewrite, negated when the comparison was <c>!= 0</c>.</summary>
    /// <param name="comparison">The reported comparison.</param>
    /// <param name="indexOf">The <c>IndexOf</c> invocation.</param>
    /// <returns>The prefix test that replaces the comparison.</returns>
    internal static ExpressionSyntax BuildPrefixTest(BinaryExpressionSyntax comparison, InvocationExpressionSyntax indexOf)
    {
        var access = (MemberAccessExpressionSyntax)indexOf.Expression;
        var startsWith = indexOf.WithExpression(
            access.WithName(SyntaxFactory.IdentifierName(StartsWithMethodName).WithTriviaFrom(access.Name)));

        return comparison.IsKind(SyntaxKind.NotEqualsExpression)
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, startsWith)
            : startsWith;
    }

    /// <summary>Returns whether an expression is the literal <c>0</c>.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> for a zero literal.</returns>
    private static bool IsZero(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression, Token.ValueText: "0" };

    /// <summary>Returns whether an expression is a plain <c>x.IndexOf(...)</c> call.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    private static bool IsIndexOfShape(ExpressionSyntax expression)
        => expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } invocation
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == IndexOfMethodName;

    /// <summary>Reports PSH1221 for a prefix question asked with a whole-string search.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="overloads">The <c>StartsWith</c> overloads available in this compilation.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context, in StartsWithOverloads overloads)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (TryGetIndexOfCall(comparison) is not { } indexOf)
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (model.GetSymbolInfo(indexOf, cancellationToken).Symbol is not IMethodSymbol search
            || search.IsStatic
            || search.ContainingType.SpecialType != SpecialType.System_String
            || !PreservesComparison(search, overloads)
            || SpanRewriteGuard.IsInsideExpressionTree(comparison, model, cancellationToken)
            || !RewriteBindsToStartsWith(model, comparison, indexOf, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseStartsWithOverIndexOf,
            comparison.SyntaxTree,
            comparison.Span,
            indexOf.ToString()));
    }

    /// <summary>Returns whether the search's comparison basis survives the move to <c>StartsWith</c> untouched.</summary>
    /// <param name="search">The bound <c>IndexOf</c> method.</param>
    /// <param name="overloads">The <c>StartsWith</c> overloads available in this compilation.</param>
    /// <returns><see langword="true"/> when the rewrite asks the same question.</returns>
    private static bool PreservesComparison(IMethodSymbol search, in StartsWithOverloads overloads)
    {
        var parameters = search.Parameters;
        if (parameters.Length == 1)
        {
            return parameters[0].Type.SpecialType switch
            {
                SpecialType.System_Char => overloads.Char,
                SpecialType.System_String => overloads.String,
                _ => false,
            };
        }

        return parameters.Length == 2
            && parameters[0].Type.SpecialType == SpecialType.System_String
            && SpanRewriteGuard.IsStringComparison(parameters[1].Type)
            && overloads.StringComparison;
    }

    /// <summary>Confirms the <c>StartsWith</c> rewrite binds to a boolean prefix test on <see cref="string"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="comparison">The reported comparison.</param>
    /// <param name="indexOf">The <c>IndexOf</c> invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the fix compiles and answers the same question.</returns>
    private static bool RewriteBindsToStartsWith(
        SemanticModel model,
        BinaryExpressionSyntax comparison,
        InvocationExpressionSyntax indexOf,
        CancellationToken cancellationToken)
    {
        // Speculative binding is the most expensive step in the rule and has no cancellable overload,
        // so the token is honoured on the way in instead.
        cancellationToken.ThrowIfCancellationRequested();

        var access = (MemberAccessExpressionSyntax)indexOf.Expression;
        var rewritten = indexOf.WithExpression(access.WithName(SyntaxFactory.IdentifierName(StartsWithMethodName)));
        return model.GetSpeculativeSymbolInfo(comparison.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol
            {
                Name: StartsWithMethodName,
                IsStatic: false,
                ReturnType.SpecialType: SpecialType.System_Boolean,
                ContainingType.SpecialType: SpecialType.System_String,
            };
    }

    /// <summary>The <c>StartsWith</c> overloads a compilation can actually offer.</summary>
    /// <param name="Char">Whether <c>StartsWith(char)</c> exists — it does not on netstandard2.0 or .NET Framework.</param>
    /// <param name="String">Whether <c>StartsWith(string)</c> exists.</param>
    /// <param name="StringComparison">Whether <c>StartsWith(string, StringComparison)</c> exists.</param>
    internal readonly record struct StartsWithOverloads(bool Char, bool String, bool StringComparison)
    {
        /// <summary>Gets a value indicating whether any shape can ever be reported.</summary>
        public bool HasAny => Char || String || StringComparison;

        /// <summary>Probes the compilation's <see cref="string"/> member list once for the prefix tests.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The available overloads.</returns>
        public static StartsWithOverloads Resolve(Compilation compilation)
        {
            var stringType = compilation.GetSpecialType(SpecialType.System_String);
            var hasChar = false;
            var hasString = false;
            var hasStringComparison = false;
            foreach (var member in stringType.GetMembers(StartsWithMethodName))
            {
                if (member is not IMethodSymbol { IsStatic: false, ReturnType.SpecialType: SpecialType.System_Boolean } method)
                {
                    continue;
                }

                hasChar |= method.Parameters is [{ Type.SpecialType: SpecialType.System_Char }];
                hasString |= method.Parameters is [{ Type.SpecialType: SpecialType.System_String }];
                hasStringComparison |= method.Parameters is [{ Type.SpecialType: SpecialType.System_String }, { } second]
                    && SpanRewriteGuard.IsStringComparison(second.Type);
            }

            return new(hasChar, hasString, hasStringComparison);
        }
    }
}
