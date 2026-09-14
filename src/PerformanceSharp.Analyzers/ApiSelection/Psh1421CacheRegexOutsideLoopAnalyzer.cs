// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a static <c>Regex</c> call written inside a loop body (PSH1421). Each call re-resolves the
/// pattern through the bounded process-wide cache; one instance built outside the loop resolves it once.
/// </summary>
/// <remarks>
/// The rule resolves <c>Regex</c> on first demand after the receiver and argument syntax checks pass,
/// caching the result per compilation even when the type is absent. Only a call that
/// actually takes a pattern qualifies — found by parameter name, so <c>Escape</c> and <c>Unescape</c>, which
/// rewrite a literal string and compile nothing, are never reported. Inside a loop the pattern must also be
/// the same string on every pass: one read from the loop's iteration variable, or from anything the loop
/// assigns, is a new pattern each time and has nothing to hoist.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1421CacheRegexOutsideLoopAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The parameter name every pattern-taking static shares.</summary>
    private const string PatternParameterName = "pattern";

    /// <summary>The receiver type name the syntax prepass requires before any binding.</summary>
    private const string RegexTypeName = "Regex";

    /// <summary>The metadata name of the regular-expression type.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.CacheRegexOutsideLoop);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, RegexMetadataName),
            Analyze,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns the loop that runs a node once per iteration, or <see langword="null"/> when none does.</summary>
    /// <param name="node">The call to locate.</param>
    /// <returns>The enclosing loop statement, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The walk stops at the enclosing member or at a nested function, because a call inside a lambda declared
    /// in a loop runs when the delegate does, not once per iteration.
    /// </remarks>
    private static SyntaxNode? GetEnclosingLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax:
                    return current;

                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax:
                    return null;

                default:
                    continue;
            }
        }

        return null;
    }

    /// <summary>Returns the call's pattern argument, or <see langword="null"/> when it takes no pattern.</summary>
    /// <param name="invocation">The static <c>Regex</c> call.</param>
    /// <param name="method">The bound method symbol.</param>
    /// <returns>The pattern expression, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The parameter is found by name rather than by position, so a static that takes no pattern at all —
    /// <c>Escape</c> and <c>Unescape</c>, which only rewrite a literal string — is never reported. There is
    /// no compiled pattern behind them to hoist.
    /// </remarks>
    private static ExpressionSyntax? GetPatternArgument(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name != PatternParameterName)
            {
                continue;
            }

            var arguments = invocation.ArgumentList.Arguments;
            return i < arguments.Count ? arguments[i].Expression : null;
        }

        return null;
    }

    /// <summary>Returns whether a pattern expression is a compile-time constant.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="pattern">The pattern argument.</param>
    /// <returns><see langword="true"/> when the pattern is fixed at compile time.</returns>
    private static bool IsConstantPattern(in SyntaxNodeAnalysisContext context, ExpressionSyntax pattern) =>
        pattern is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            || context.SemanticModel.GetConstantValue(pattern, context.CancellationToken) is { HasValue: true, Value: string };

    /// <summary>Returns whether the pattern is a different string on each pass of the loop.</summary>
    /// <param name="pattern">The pattern argument.</param>
    /// <param name="loop">The enclosing loop.</param>
    /// <returns><see langword="true"/> when the pattern cannot be hoisted out of the loop.</returns>
    /// <remarks>
    /// Hoisting only pays when the same pattern is compiled every pass. A pattern read from the loop's own
    /// iteration variable, or from anything the loop writes, is a new expression each time — there is one
    /// instance per pattern to build, not one to lift out, and the suggestion has no valid rewrite.
    /// <para>
    /// The loop is read once, into the set of names it refreshes, and the pattern's identifiers are then a
    /// lookup each. Asking the question per identifier meant re-reading the whole loop for every name the
    /// pattern mentions, so a pattern built from three of them read the loop three times. Matching on the
    /// name alone also keeps the semantic model out of it: the answer only ever widens the set of patterns
    /// left alone, and this rule would rather stay quiet than suggest a hoist that does not hold.
    /// </para>
    /// </remarks>
    private static bool PatternVariesPerIteration(ExpressionSyntax pattern, SyntaxNode loop)
    {
        var refreshed = new HashSet<string>(StringComparer.Ordinal);

        // The descendant walk starts below its root, and a foreach declares its iteration variable on the
        // loop node itself — the single most common way a pattern changes between passes.
        AddRefreshedName(loop, refreshed);
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, HashSet<string>>(loop, ref refreshed, VisitLoopNode);

        return refreshed.Count != 0
            && ((pattern is IdentifierNameSyntax self && !IsUnrefreshedName(self, ref refreshed))
                || !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HashSet<string>>(pattern, ref refreshed, IsUnrefreshedName));
    }

    /// <summary>Records one name the loop declares or writes.</summary>
    /// <param name="node">The visited node.</param>
    /// <param name="names">The names the loop refreshes.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitLoopNode(SyntaxNode node, ref HashSet<string> names)
    {
        AddRefreshedName(node, names);
        return true;
    }

    /// <summary>Records the name a node declares or writes, when it does either.</summary>
    /// <param name="node">The node to classify.</param>
    /// <param name="names">The set to add to.</param>
    private static void AddRefreshedName(SyntaxNode node, HashSet<string> names)
    {
        var name = node switch
        {
            VariableDeclaratorSyntax declarator => declarator.Identifier.ValueText,
            ForEachStatementSyntax forEach => forEach.Identifier.ValueText,
            SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
            ParameterSyntax parameter => parameter.Identifier.ValueText,
            IdentifierNameSyntax identifier when WriteTargetSyntax.IsIdentifierWriteTarget(identifier) => identifier.Identifier.ValueText,
            _ => null,
        };

        if (name is null)
        {
            return;
        }

        _ = names.Add(name);
    }

    /// <summary>Continues the walk past an identifier the loop does not refresh.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="refreshed">The names the loop declares or writes.</param>
    /// <returns><see langword="false"/> at a refreshed name, which stops the walk.</returns>
    private static bool IsUnrefreshedName(IdentifierNameSyntax identifier, ref HashSet<string> refreshed) =>
        !refreshed.Contains(identifier.Identifier.ValueText);

    /// <summary>Reports one static <c>Regex</c> call whose pattern is resolved again on every call.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="types">The lazily resolved regular-expression type.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, LazyMetadataType types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } access
            || invocation.ArgumentList.Arguments.Count < 2
            || !TypeNameReceiver.EndsWithTypeName(access.Expression, RegexTypeName))
        {
            return;
        }

        if (GetRegexPattern(context, invocation, types) is not { } pattern)
        {
            return;
        }

        var loop = GetEnclosingLoop(invocation);
        if (loop is null ? !IsConstantPattern(context, pattern) : PatternVariesPerIteration(pattern, loop))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.CacheRegexOutsideLoop,
            invocation.SyntaxTree,
            invocation.Span,
            name.Identifier.ValueText,
            loop is not null ? ApiSelectionRules.RegexCalledPerIteration : ApiSelectionRules.RegexConstantPattern));
    }

    /// <summary>Resolves the pattern argument only for calls bound to the framework's static regex methods.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The candidate regex invocation.</param>
    /// <param name="types">The regular-expression type cached for this compilation.</param>
    /// <returns>The pattern argument, or null when the call does not match.</returns>
    private static ExpressionSyntax? GetRegexPattern(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, LazyMetadataType types) =>
        types.Get() is { } regex
        && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { IsStatic: true } method
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, regex)
            ? GetPatternArgument(invocation, method)
            : null;
}
