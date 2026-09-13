// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
        context.RegisterCompilationStartAction(static start =>
        {
            var types = new RegexTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, types), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether a member access reads a member off something named <c>Regex</c>.</summary>
    /// <param name="access">The invoked member access.</param>
    /// <returns><see langword="true"/> when the receiver's rightmost identifier is <c>Regex</c>.</returns>
    /// <remarks>
    /// A free syntax gate that runs before the semantic model is touched. Without it every member call
    /// taking two or more arguments — <c>dict.TryGetValue(key, out value)</c>, <c>string.Format(a, b)</c> —
    /// pays a <c>GetSymbolInfo</c>, which is the cost the rejection path is made of. Matching the name
    /// rather than the type keeps it free and only over-approximates: a real <c>Regex</c> call always
    /// passes, and anything else that happens to be spelled <c>Regex</c> is turned back by the binding
    /// that follows.
    /// </remarks>
    private static bool IsRegexReceiverShape(MemberAccessExpressionSyntax access)
    {
        var receiver = access.Expression;
        while (receiver is MemberAccessExpressionSyntax nested)
        {
            receiver = nested.Name;
        }

        return receiver is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == RegexTypeName;
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
        var loopState = new RefreshedNameScanState(refreshed);

        // The descendant walk starts below its root, and a foreach declares its iteration variable on the
        // loop node itself — the single most common way a pattern changes between passes.
        AddRefreshedName(loop, refreshed);
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, RefreshedNameScanState>(loop, ref loopState, VisitLoopNode);

        if (refreshed.Count == 0)
        {
            return false;
        }

        var patternState = new PatternScanState(refreshed);
        if (pattern is IdentifierNameSyntax self && !VisitPatternIdentifier(self, ref patternState))
        {
            return true;
        }

        _ = DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, PatternScanState>(pattern, ref patternState, VisitPatternIdentifier);
        return patternState.Varies;
    }

    /// <summary>Records one name the loop declares or writes.</summary>
    /// <param name="node">The visited node.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitLoopNode(SyntaxNode node, ref RefreshedNameScanState state)
    {
        AddRefreshedName(node, state.Names);
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
            IdentifierNameSyntax identifier when IsWriteTarget(identifier) => identifier.Identifier.ValueText,
            _ => null,
        };

        if (name is null)
        {
            return;
        }

        _ = names.Add(name);
    }

    /// <summary>Returns whether an identifier occurrence is the target of a write.</summary>
    /// <param name="identifier">The identifier occurrence.</param>
    /// <returns><see langword="true"/> for assignment targets, increments, decrements, and ref/out arguments.</returns>
    private static bool IsWriteTarget(IdentifierNameSyntax identifier) =>
        identifier.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == identifier,
            PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
            ArgumentSyntax argument => !argument.RefOrOutKeyword.IsKind(SyntaxKind.None),
            _ => false,
        };

    /// <summary>Classifies one identifier read by the pattern expression.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once the pattern is known to vary.</returns>
    private static bool VisitPatternIdentifier(IdentifierNameSyntax identifier, ref PatternScanState state)
    {
        if (!state.Refreshed.Contains(identifier.Identifier.ValueText))
        {
            return true;
        }

        state.Varies = true;
        return false;
    }

    /// <summary>Reports one static <c>Regex</c> call whose pattern is resolved again on every call.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="types">The lazily resolved regular-expression type.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, RegexTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } access
            || invocation.ArgumentList.Arguments.Count < 2
            || !IsRegexReceiverShape(access))
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
    private static ExpressionSyntax? GetRegexPattern(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, RegexTypes types) =>
        types.Get() is { } regex
        && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { IsStatic: true } method
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, regex)
            ? GetPatternArgument(invocation, method)
            : null;

    /// <summary>Collects the names a loop declares or writes, in one pass over it.</summary>
    /// <param name="Names">The names collected so far.</param>
    private readonly record struct RefreshedNameScanState(HashSet<string> Names);

    /// <summary>Decides whether the identifiers a pattern reads are among the names the loop refreshes.</summary>
    /// <param name="Refreshed">The names the loop declares or writes.</param>
    private record struct PatternScanState(HashSet<string> Refreshed)
    {
        /// <summary>Gets or sets a value indicating whether the pattern changes between iterations.</summary>
        public bool Varies { get; set; }
    }

    /// <summary>Resolves the regular-expression type only for candidate calls, caching misses too.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    private sealed class RegexTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the regular-expression type.</summary>
        private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

        /// <summary>The resolved type in a published array, or null before the first candidate.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the regular-expression type on first demand.</summary>
        /// <returns>The resolved type, or null when it is unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(RegexMetadataName)])[0];
    }
}
