// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a static <c>Regex</c> call written inside a loop body (PSH1421). Each call re-resolves the
/// pattern through the bounded process-wide cache; one instance built outside the loop resolves it once.
/// </summary>
/// <remarks>
/// The rule resolves <c>Regex</c> once per compilation and does nothing at all when the type is absent, so a
/// project that never references the regular-expression assembly pays only that one lookup. Only a call that
/// actually takes a pattern qualifies — found by parameter name, so <c>Escape</c> and <c>Unescape</c>, which
/// rewrite a literal string and compile nothing, are never reported. Inside a loop the pattern must also be
/// the same string on every pass: one read from the loop's iteration variable, or from anything the loop
/// assigns, is a new pattern each time and has nothing to hoist.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1421CacheRegexOutsideLoopAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the regular-expression type.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The parameter name every pattern-taking static shares.</summary>
    private const string PatternParameterName = "pattern";

    /// <summary>The receiver type name the syntax prepass requires before any binding.</summary>
    private const string RegexTypeName = "Regex";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(ApiSelectionRules.CacheRegexOutsideLoop);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(RegexMetadataName) is not { } regex)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, regex), SyntaxKind.InvocationExpression);
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
                case ForStatementSyntax:
                case ForEachStatementSyntax:
                case ForEachVariableStatementSyntax:
                case WhileStatementSyntax:
                case DoStatementSyntax:
                    return current;

                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
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
    private static bool IsConstantPattern(SyntaxNodeAnalysisContext context, ExpressionSyntax pattern)
        => pattern is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
            || context.SemanticModel.GetConstantValue(pattern, context.CancellationToken) is { HasValue: true, Value: string };

    /// <summary>Returns whether the pattern is a different string on each pass of the loop.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="pattern">The pattern argument.</param>
    /// <param name="loop">The enclosing loop.</param>
    /// <returns><see langword="true"/> when the pattern cannot be hoisted out of the loop.</returns>
    /// <remarks>
    /// Hoisting only pays when the same pattern is compiled every pass. A pattern read from the loop's own
    /// iteration variable, or from anything the loop writes, is a new expression each time — there is one
    /// instance per pattern to build, not one to lift out, and the suggestion has no valid rewrite.
    /// </remarks>
    private static bool PatternVariesPerIteration(SyntaxNodeAnalysisContext context, ExpressionSyntax pattern, SyntaxNode loop)
    {
        var state = new PatternScanState(context.SemanticModel, loop, context.CancellationToken);
        if (pattern is IdentifierNameSyntax self && !VisitPatternIdentifier(self, ref state))
        {
            return true;
        }

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, PatternScanState>(pattern, ref state, VisitPatternIdentifier);
        return state.Varies;
    }

    /// <summary>Classifies one identifier read by the pattern expression.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once the pattern is known to vary.</returns>
    private static bool VisitPatternIdentifier(IdentifierNameSyntax identifier, ref PatternScanState state)
    {
        if (!state.IsLoopScoped(identifier))
        {
            return true;
        }

        state.Varies = true;
        return false;
    }

    /// <summary>Reports one static <c>Regex</c> call whose pattern is resolved again on every call.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="regex">The resolved regular-expression type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol regex)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } access
            || invocation.ArgumentList.Arguments.Count < 2
            || !IsRegexReceiverShape(access))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, regex)
            || GetPatternArgument(invocation, method) is not { } pattern)
        {
            return;
        }

        var loop = GetEnclosingLoop(invocation);
        if (loop is null ? !IsConstantPattern(context, pattern) : PatternVariesPerIteration(context, pattern, loop))
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

    /// <summary>Decides whether the identifiers a pattern reads are refreshed by the loop.</summary>
    private record struct PatternScanState
    {
        /// <summary>The semantic model for the call being analyzed.</summary>
        private readonly SemanticModel _model;

        /// <summary>The enclosing loop.</summary>
        private readonly SyntaxNode _loop;

        /// <summary>A token that cancels the operation.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>Initializes a new instance of the <see cref="PatternScanState"/> struct.</summary>
        /// <param name="model">The semantic model.</param>
        /// <param name="loop">The enclosing loop.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        public PatternScanState(SemanticModel model, SyntaxNode loop, CancellationToken cancellationToken)
        {
            _model = model;
            _loop = loop;
            _cancellationToken = cancellationToken;
            Varies = false;
        }

        /// <summary>Gets or sets a value indicating whether the pattern changes between iterations.</summary>
        public bool Varies { get; set; }

        /// <summary>Returns whether an identifier resolves to something the loop declares or writes.</summary>
        /// <param name="identifier">The identifier to classify.</param>
        /// <returns><see langword="true"/> when its value is not fixed across the loop.</returns>
        public readonly bool IsLoopScoped(IdentifierNameSyntax identifier)
        {
            if (_model.GetSymbolInfo(identifier, _cancellationToken).Symbol is not { } symbol)
            {
                return false;
            }

            var declarations = symbol.DeclaringSyntaxReferences;
            for (var i = 0; i < declarations.Length; i++)
            {
                if (_loop.Span.Contains(declarations[i].Span))
                {
                    return true;
                }
            }

            return LoopWrites(identifier.Identifier.ValueText);
        }

        /// <summary>Classifies one identifier occurrence inside the loop.</summary>
        /// <param name="identifier">The visited identifier.</param>
        /// <param name="state">The current scan state.</param>
        /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once a write is seen.</returns>
        private static bool VisitWriteCandidate(IdentifierNameSyntax identifier, ref WriteScanState state)
        {
            if (identifier.Identifier.ValueText != state.Name || !IsWriteTarget(identifier))
            {
                return true;
            }

            state.Found = true;
            return false;
        }

        /// <summary>Returns whether an identifier occurrence is the target of a write.</summary>
        /// <param name="identifier">The identifier occurrence.</param>
        /// <returns><see langword="true"/> for assignment targets, increments, decrements, and ref/out arguments.</returns>
        private static bool IsWriteTarget(IdentifierNameSyntax identifier)
            => identifier.Parent switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left == identifier,
                PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
                ArgumentSyntax argument => !argument.RefOrOutKeyword.IsKind(SyntaxKind.None),
                _ => false,
            };

        /// <summary>Returns whether the loop assigns the named identifier anywhere inside itself.</summary>
        /// <param name="name">The identifier name to look for.</param>
        /// <returns><see langword="true"/> when a write is found.</returns>
        private readonly bool LoopWrites(string name)
        {
            var state = new WriteScanState(name);
            DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, WriteScanState>(_loop, ref state, VisitWriteCandidate);
            return state.Found;
        }

        /// <summary>Tracks the search for a write to one named identifier.</summary>
        /// <param name="Name">The identifier name to look for.</param>
        private record struct WriteScanState(string Name)
        {
            /// <summary>Gets or sets a value indicating whether a write was found.</summary>
            public bool Found { get; set; }
        }
    }
}
