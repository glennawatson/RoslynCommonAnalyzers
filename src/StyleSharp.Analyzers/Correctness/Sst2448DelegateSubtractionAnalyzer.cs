// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports delegate subtractions whose outcome depends on the order handlers were combined (SST2448).
/// </summary>
/// <remarks>
/// <para>
/// Two shapes are reported. A binary <c>a - b</c> between delegate values is reported outright: composing
/// delegates with <c>-</c> is rare, and the result depends on where <c>b</c>'s handlers sit inside
/// <c>a</c>. A <c>target -= value</c> is reported only when the removed value is itself a combination —
/// an inline <c>first + second</c>, a variable this member visibly built with <c>+</c> or <c>+=</c>, or
/// an opaque delegate-producing expression (a call result, a conditional, a coalesce) whose composition
/// the call site cannot see.
/// </para>
/// <para>
/// The mirror of a subscription is deliberately not reported: a method group, a lambda or anonymous
/// method (removing one is a different defect — it removes nothing — and is not an ordering problem), a
/// stored handler read from a field, property, parameter, element or conditional access, a delegate
/// creation such as <c>new EventHandler(Handler)</c>, and a cast around any of these. Combinations only
/// count when they are visible in the same member; a field combined elsewhere is not tracked.
/// </para>
/// <para>
/// The clean path stays syntactic: literals, unary operands and lambdas are rejected without binding, an
/// identifier is bound only after a same-member combination of that name has been found on syntax, and
/// only add/coalesce shapes among binary right-hand sides ever reach the semantic model.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2448DelegateSubtractionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.DelegateSubtraction);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeSubtraction, SyntaxKind.SubtractExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSubtractAssignment, SyntaxKind.SubtractAssignmentExpression);
    }

    /// <summary>Reports one binary subtraction whose operands are delegates.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSubtraction(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (CannotBeDelegate(binary.Left) || CannotBeDelegate(binary.Right) || !IsDelegateTyped(context, binary.Left))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DelegateSubtraction,
            binary.GetLocation(),
            StripEnclosure(binary.Right).ToString()));
    }

    /// <summary>Reports one <c>-=</c> whose removed value is a combined or opaque delegate.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSubtractAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var removed = StripEnclosure(assignment.Right);
        if (CannotBeDelegate(removed) || IsSubscriptionMirror(removed))
        {
            return;
        }

        switch (removed)
        {
            case IdentifierNameSyntax identifier:
            {
                AnalyzeIdentifierRemoval(context, assignment, identifier);
                return;
            }

            case BinaryExpressionSyntax binary:
            {
                AnalyzeBinaryRemoval(context, assignment, binary);
                return;
            }

            default:
            {
                ReportWhenDelegate(context, assignment, removed);
                return;
            }
        }
    }

    /// <summary>Returns whether the removed expression is the single-delegate mirror of a subscription.</summary>
    /// <param name="removed">The removed expression, already unwrapped.</param>
    /// <returns><see langword="true"/> for shapes that hand back the one delegate a subscription added.</returns>
    /// <remarks>
    /// A lambda or anonymous method is included here even though removing one takes off nothing at
    /// all — that is a separate defect, not an ordering problem.
    /// </remarks>
    private static bool IsSubscriptionMirror(ExpressionSyntax removed)
        => removed is AnonymousFunctionExpressionSyntax
            or MemberAccessExpressionSyntax
            or ConditionalAccessExpressionSyntax
            or ElementAccessExpressionSyntax
            or BaseObjectCreationExpressionSyntax
            or GenericNameSyntax;

    /// <summary>Reports one removal of an inline combination or a coalesced delegate.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="assignment">The subtract-assignment.</param>
    /// <param name="binary">The removed binary expression.</param>
    /// <remarks>
    /// No other binary operator produces a delegate, so everything else stays silent; a nested
    /// subtraction is reported on its own.
    /// </remarks>
    private static void AnalyzeBinaryRemoval(
        SyntaxNodeAnalysisContext context,
        AssignmentExpressionSyntax assignment,
        BinaryExpressionSyntax binary)
    {
        if (binary.IsKind(SyntaxKind.AddExpression) && HasObviouslyNonDelegateOperand(binary))
        {
            return;
        }

        if (!binary.IsKind(SyntaxKind.AddExpression) && !binary.IsKind(SyntaxKind.CoalesceExpression))
        {
            return;
        }

        ReportWhenDelegate(context, assignment, binary);
    }

    /// <summary>Reports one <c>target -= name</c> when this member visibly built <c>name</c> as a combination.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="assignment">The subtract-assignment.</param>
    /// <param name="identifier">The removed identifier.</param>
    /// <remarks>
    /// The member is scanned on syntax first; the identifier is bound only after an assignment or
    /// initializer combining that name has been found, and the diagnostic requires both to resolve to
    /// the same delegate-typed symbol — so a shadowing name or a numeric sum never reports.
    /// </remarks>
    private static void AnalyzeIdentifierRemoval(
        SyntaxNodeAnalysisContext context,
        AssignmentExpressionSyntax assignment,
        IdentifierNameSyntax identifier)
    {
        var scan = new CombinationScan(identifier.Identifier.ValueText);
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, CombinationScan>(
            GetCombinationScanRoot(assignment),
            ref scan,
            VisitCombination);
        if ((scan.AssignedTarget is null && scan.Declarator is null)
            || !IsDelegateTyped(context, identifier)
            || ResolveCombinedSymbol(context, scan) is not { } combined
            || context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not { } removed
            || !SymbolEqualityComparer.Default.Equals(removed, combined))
        {
            return;
        }

        ReportRemoval(context, assignment, identifier);
    }

    /// <summary>Resolves the symbol the found combination wrote to.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="scan">The completed combination scan.</param>
    /// <returns>The combined symbol, or <see langword="null"/> when it does not bind.</returns>
    private static ISymbol? ResolveCombinedSymbol(SyntaxNodeAnalysisContext context, CombinationScan scan)
        => scan.Declarator is { } declarator
            ? context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken)
            : context.SemanticModel.GetSymbolInfo(scan.AssignedTarget!, context.CancellationToken).Symbol;

    /// <summary>Records the first assignment or initializer that combines the scanned name.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once a combination is found, which stops the walk.</returns>
    private static bool VisitCombination(SyntaxNode node, ref CombinationScan state)
    {
        switch (node)
        {
            case AssignmentExpressionSyntax candidate when IsCombinationAssignment(candidate, state.Name):
            {
                state.AssignedTarget = candidate.Left;
                return false;
            }

            case VariableDeclaratorSyntax declarator when IsCombinationDeclarator(declarator, state.Name):
            {
                state.Declarator = declarator;
                return false;
            }

            default:
                return true;
        }
    }

    /// <summary>Returns whether an assignment builds the named value as a combination.</summary>
    /// <param name="candidate">The assignment to inspect.</param>
    /// <param name="name">The removed name.</param>
    /// <returns><see langword="true"/> for <c>name += x</c> and <c>name = a + b</c>.</returns>
    private static bool IsCombinationAssignment(AssignmentExpressionSyntax candidate, string name)
        => GetAssignedName(candidate.Left) == name
            && (candidate.IsKind(SyntaxKind.AddAssignmentExpression)
                || (candidate.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && StripEnclosure(candidate.Right).IsKind(SyntaxKind.AddExpression)));

    /// <summary>Returns whether a declarator initializes the named value as a combination.</summary>
    /// <param name="declarator">The declarator to inspect.</param>
    /// <param name="name">The removed name.</param>
    /// <returns><see langword="true"/> for <c>var name = a + b</c>.</returns>
    private static bool IsCombinationDeclarator(VariableDeclaratorSyntax declarator, string name)
        => declarator.Identifier.ValueText == name
            && declarator.Initializer is { Value: { } value }
            && StripEnclosure(value).IsKind(SyntaxKind.AddExpression);

    /// <summary>Gets the name an assignment writes to.</summary>
    /// <param name="target">The assignment's left-hand side.</param>
    /// <returns>The written name, or <see langword="null"/> for a target that is not a simple or <c>this</c>-qualified name.</returns>
    private static string? GetAssignedName(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } name } => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Gets the node whose descendants can hold a combination visible at this removal.</summary>
    /// <param name="assignment">The subtract-assignment.</param>
    /// <returns>The enclosing member, or the compilation unit for a top-level statement.</returns>
    /// <remarks>
    /// The walk deliberately passes lambdas and local functions: a local combined in the enclosing
    /// method is still the value a nested <c>-=</c> removes.
    /// </remarks>
    private static SyntaxNode GetCombinationScanRoot(AssignmentExpressionSyntax assignment)
    {
        SyntaxNode root = assignment;
        for (SyntaxNode? node = assignment.Parent; node is not null; node = node.Parent)
        {
            root = node;
            if (node is MemberDeclarationSyntax and not GlobalStatementSyntax)
            {
                break;
            }
        }

        return root;
    }

    /// <summary>Reports the removed expression once it binds to a delegate type.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="assignment">The subtract-assignment.</param>
    /// <param name="removed">The expression whose type decides the report.</param>
    private static void ReportWhenDelegate(
        SyntaxNodeAnalysisContext context,
        AssignmentExpressionSyntax assignment,
        ExpressionSyntax removed)
    {
        if (!IsDelegateTyped(context, removed))
        {
            return;
        }

        ReportRemoval(context, assignment, removed);
    }

    /// <summary>Reports one order-dependent removal on the assignment's right-hand side.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="assignment">The subtract-assignment.</param>
    /// <param name="removed">The removed delegate expression, for the message.</param>
    private static void ReportRemoval(
        SyntaxNodeAnalysisContext context,
        AssignmentExpressionSyntax assignment,
        ExpressionSyntax removed)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DelegateSubtraction,
            assignment.Right.GetLocation(),
            removed.ToString()));

    /// <summary>Returns whether an expression binds to a delegate type.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The expression to bind.</param>
    /// <returns><see langword="true"/> when the expression's type is a delegate.</returns>
    private static bool IsDelegateTyped(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        => context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is { TypeKind: TypeKind.Delegate };

    /// <summary>Returns whether an operand can be rejected without binding.</summary>
    /// <param name="expression">The operand to test.</param>
    /// <returns><see langword="true"/> for literals, interpolated strings, and unary operands — none of which is a delegate.</returns>
    private static bool CannotBeDelegate(ExpressionSyntax expression)
        => StripEnclosure(expression) is LiteralExpressionSyntax
            or InterpolatedStringExpressionSyntax
            or PrefixUnaryExpressionSyntax
            or PostfixUnaryExpressionSyntax;

    /// <summary>Returns whether any operand of an addition chain is obviously not a delegate.</summary>
    /// <param name="addition">The addition chain to inspect.</param>
    /// <returns><see langword="true"/> when a literal-shaped operand proves this is numeric or string arithmetic.</returns>
    private static bool HasObviouslyNonDelegateOperand(BinaryExpressionSyntax addition)
    {
        var current = addition;
        while (true)
        {
            if (CannotBeDelegate(current.Right))
            {
                return true;
            }

            if (StripEnclosure(current.Left) is BinaryExpressionSyntax nested && nested.IsKind(SyntaxKind.AddExpression))
            {
                current = nested;
                continue;
            }

            return CannotBeDelegate(current.Left);
        }
    }

    /// <summary>Unwraps parentheses and casts, which change nothing about what is removed.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost expression.</returns>
    private static ExpressionSyntax StripEnclosure(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            if (current is ParenthesizedExpressionSyntax parenthesized)
            {
                current = parenthesized.Expression;
            }
            else if (current is CastExpressionSyntax cast)
            {
                current = cast.Expression;
            }
            else
            {
                return current;
            }
        }
    }

    /// <summary>The state threaded through a member's combination scan.</summary>
    /// <param name="Name">The removed name being looked for.</param>
    private record struct CombinationScan(string Name)
    {
        /// <summary>Gets or sets the target of the found combination assignment.</summary>
        public ExpressionSyntax? AssignedTarget { get; set; }

        /// <summary>Gets or sets the found combination declarator.</summary>
        public VariableDeclaratorSyntax? Declarator { get; set; }
    }
}
