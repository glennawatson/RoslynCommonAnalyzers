// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>for</c> loop whose body reassigns a variable its condition depends on (SST2465), so the number of
/// iterations silently diverges from what the header states — <c>for (var i = 0; i &lt; n; i++) { n = 0; }</c> mutates
/// the bound, and <c>for (var i = 0; i &lt; 10; i++) { i = 5; }</c> mutates the counter.
/// </summary>
/// <remarks>
/// <para>
/// The policy is deliberately narrow, to keep false positives near zero. A loop is considered only when it has the
/// unambiguous counted shape: the condition is a single relational or equality comparison built only from identifiers,
/// literals, and arithmetic; the loop has exactly one incrementer that steps a single counter with <c>++</c>,
/// <c>--</c>, or a compound assignment; and that counter is tested by the condition. Anything else is left alone.
/// </para>
/// <para>
/// Within such a loop the body is reported when it reassigns — with <c>=</c>, a compound assignment, <c>++</c>, or
/// <c>--</c> — an identifier the condition reads (the counter, or the local it is compared against), and the write
/// binds to a local or a parameter. Only an <em>unconditional</em> write is reported: one that runs on every
/// iteration, sitting directly in the loop body rather than inside an <c>if</c>, a <c>switch</c>, a nested loop, a
/// <c>try</c>, or any other guarded context. A guarded write may implement an intended early advance the rule cannot
/// disprove, so it is treated as silence-worthy. A write to a field or property is not reported: something else may
/// own that value, and the write may be intended. The header's own incrementer is never reported, because only the
/// body is scanned.
/// </para>
/// <para>
/// The clean path never binds. The condition shape and the single-counter incrementer reject nearly every loop up
/// front; a candidate write's symbol is resolved only after its identifier has already matched a condition variable,
/// which is rare, so a well-formed counted loop pays only a handful of syntactic comparisons.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2465LoopConditionVariableReassignedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The most condition variables the rule will consider.</summary>
    private const int MaximumConditionVariables = 4;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.LoopConditionVariableReassigned);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForStatement);
    }

    /// <summary>Analyzes one for statement for a body write that undermines its condition.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var forStatement = (ForStatementSyntax)context.Node;
        if (forStatement.Condition is not { } condition
            || !IsSimpleRelationalComparison(condition)
            || !TryGetCounter(forStatement, out var counter)
            || !Mentions(condition, counter))
        {
            return;
        }

        InspectBody(context, condition, forStatement.Statement);
    }

    /// <summary>Inspects a loop body's unconditional statements for a write to a condition variable.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="body">The loop body.</param>
    private static void InspectBody(SyntaxNodeAnalysisContext context, ExpressionSyntax condition, StatementSyntax? body)
    {
        if (body is BlockSyntax block)
        {
            InspectStatements(context, condition, block.Statements);
        }
        else if (body is not null)
        {
            InspectStatement(context, condition, body);
        }
    }

    /// <summary>Inspects each statement in an unconditional statement list.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="statements">The statements to inspect.</param>
    private static void InspectStatements(SyntaxNodeAnalysisContext context, ExpressionSyntax condition, SyntaxList<StatementSyntax> statements)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            InspectStatement(context, condition, statements[i]);
        }
    }

    /// <summary>Inspects one unconditional statement, descending only through bare blocks.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="statement">The statement to inspect.</param>
    /// <remarks>
    /// Only a bare block and an expression statement run unconditionally on every iteration. An <c>if</c>, a loop, a
    /// <c>switch</c>, a <c>try</c>, and every other guarded construct are skipped: a write nested inside one may be an
    /// intended early advance, which the rule leaves alone.
    /// </remarks>
    private static void InspectStatement(SyntaxNodeAnalysisContext context, ExpressionSyntax condition, StatementSyntax statement)
    {
        if (statement is BlockSyntax nested)
        {
            InspectStatements(context, condition, nested.Statements);
        }
        else if (statement is ExpressionStatementSyntax expressionStatement)
        {
            TryReportWrite(context, condition, expressionStatement.Expression);
        }
    }

    /// <summary>Reports an unconditional write when its target is a condition variable bound to a local or parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="expression">The expression-statement's expression.</param>
    private static void TryReportWrite(SyntaxNodeAnalysisContext context, ExpressionSyntax condition, ExpressionSyntax expression)
    {
        if (GetWrittenIdentifier(expression) is not { } written)
        {
            return;
        }

        var name = written.Identifier.ValueText;
        if (!Mentions(condition, name))
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(written, context.CancellationToken).Symbol;
        if (symbol is not (ILocalSymbol { IsConst: false } or IParameterSymbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.LoopConditionVariableReassigned,
            expression.GetLocation(),
            name));
    }

    /// <summary>Gets the identifier a write targets, when the expression is a simple write.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns>The written identifier, or <see langword="null"/> when the expression is not a simple write.</returns>
    private static IdentifierNameSyntax? GetWrittenIdentifier(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } => target,
        PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax operand } prefix when IsIncrementOrDecrement(prefix.Kind()) => operand,
        PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax operand } postfix when IsIncrementOrDecrement(postfix.Kind()) => operand,
        _ => null,
    };

    /// <summary>Classifies a for loop's single incrementer into the counter it steps.</summary>
    /// <param name="forStatement">The for statement.</param>
    /// <param name="counter">The counter's name.</param>
    /// <returns><see langword="true"/> for a single step of one named counter.</returns>
    private static bool TryGetCounter(ForStatementSyntax forStatement, out string counter)
    {
        counter = string.Empty;
        var incrementors = forStatement.Incrementors;
        if (incrementors.Count != 1)
        {
            return false;
        }

        var incrementor = incrementors[0];
        if (incrementor is PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax postfixName } postfix && IsIncrementOrDecrement(postfix.Kind()))
        {
            counter = postfixName.Identifier.ValueText;
            return true;
        }

        if (incrementor is PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax prefixName } prefix && IsIncrementOrDecrement(prefix.Kind()))
        {
            counter = prefixName.Identifier.ValueText;
            return true;
        }

        if (incrementor is not AssignmentExpressionSyntax { Left: IdentifierNameSyntax assignName })
        {
            return false;
        }

        counter = assignName.Identifier.ValueText;
        return true;
    }

    /// <summary>Returns whether a condition is a single relational or equality comparison over simple operands.</summary>
    /// <param name="condition">The loop's condition.</param>
    /// <returns><see langword="true"/> when the condition is a comparison built only from names, literals and operators.</returns>
    private static bool IsSimpleRelationalComparison(ExpressionSyntax condition)
    {
        if (condition is not BinaryExpressionSyntax binary || !IsRelationalOrEquality(binary.Kind()))
        {
            return false;
        }

        var scan = default(ShapeScan);
        if (!VisitConditionNode(binary, ref scan))
        {
            return false;
        }

        DescendantTraversalHelper.VisitDescendants<SyntaxNode, ShapeScan>(binary, ref scan, VisitConditionNode);
        return !scan.Rejected && scan.Identifiers is > 0 and <= MaximumConditionVariables;
    }

    /// <summary>Rejects a condition containing anything the rule cannot reason about.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once the condition is rejected, which stops the walk.</returns>
    private static bool VisitConditionNode(SyntaxNode node, ref ShapeScan scan)
    {
        if (node is IdentifierNameSyntax)
        {
            scan.Identifiers++;
            return true;
        }

        if (node is LiteralExpressionSyntax or ParenthesizedExpressionSyntax or BinaryExpressionSyntax
            || (node is PrefixUnaryExpressionSyntax prefix && !IsIncrementOrDecrement(prefix.Kind())))
        {
            return true;
        }

        scan.Rejected = true;
        return false;
    }

    /// <summary>Returns whether an expression mentions a name.</summary>
    /// <param name="expression">The expression to search.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns><see langword="true"/> when the name appears.</returns>
    private static bool Mentions(ExpressionSyntax expression, string name)
    {
        if (expression is IdentifierNameSyntax self && self.Identifier.ValueText == name)
        {
            return true;
        }

        var scan = new NameScan(name);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, NameScan>(expression, ref scan, VisitName);
        return scan.Found;
    }

    /// <summary>Records whether an identifier is the name being looked for.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once the name is found, which stops the walk.</returns>
    private static bool VisitName(IdentifierNameSyntax identifier, ref NameScan scan)
    {
        if (identifier.Identifier.ValueText != scan.Name)
        {
            return true;
        }

        scan.Found = true;
        return false;
    }

    /// <summary>Returns whether a syntax kind is a relational or equality comparison.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>==</c> and <c>!=</c>.</returns>
    private static bool IsRelationalOrEquality(SyntaxKind kind)
        => kind is SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression
            or SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression;

    /// <summary>Returns whether an operator kind increments or decrements its operand.</summary>
    /// <param name="kind">The operator kind.</param>
    /// <returns><see langword="true"/> for <c>++</c> and <c>--</c> in either position.</returns>
    private static bool IsIncrementOrDecrement(SyntaxKind kind)
        => kind is SyntaxKind.PreIncrementExpression
            or SyntaxKind.PreDecrementExpression
            or SyntaxKind.PostIncrementExpression
            or SyntaxKind.PostDecrementExpression;

    /// <summary>The state threaded through the condition's shape scan.</summary>
    private record struct ShapeScan
    {
        /// <summary>Gets or sets the number of variables the condition reads.</summary>
        public int Identifiers { get; set; }

        /// <summary>Gets or sets a value indicating whether the condition was rejected.</summary>
        public bool Rejected { get; set; }
    }

    /// <summary>The state threaded through a name search.</summary>
    /// <param name="Name">The name being looked for.</param>
    private record struct NameScan(string Name)
    {
        /// <summary>Gets or sets a value indicating whether the name was found.</summary>
        public bool Found { get; set; }
    }
}
