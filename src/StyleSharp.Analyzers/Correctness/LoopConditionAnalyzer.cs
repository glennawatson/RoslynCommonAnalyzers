// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports control-flow defects in <c>while</c> and <c>for</c> loops in a single walk. The stop condition
/// of a loop, and the way a <c>for</c> loop drives its counter, decide whether the loop terminates, runs at
/// all, or does what its author meant.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST2406 — a loop's condition reads only variables that nothing in the loop ever
/// writes, so it has already decided its answer.</description></item>
/// <item><description>SST2411 — a <c>for</c> loop declares a counter, tests it in the condition, and never
/// advances it. SST2406 is suppressed on a loop this reports.</description></item>
/// <item><description>SST2412 — a <c>for</c> loop steps its counter away from the side of its bound, so it
/// never terminates or never runs.</description></item>
/// <item><description>SST2413 — a <c>for</c> loop's condition is already false at the counter's constant
/// starting value, so the body never runs.</description></item>
/// </list>
/// <para>
/// The clean path never binds. The condition's shape rejects most loops outright, and a loop whose body
/// writes a condition variable — which is nearly all of them — is rejected on syntax before the locals are
/// resolved. The <c>for</c>-specific rules reject the ordinary stepping loop from the incrementer before any
/// allocation, so a well-formed counter loop pays only a token comparison.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoopConditionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The most condition variables the rule will consider.</summary>
    private const int MaximumConditionVariables = 4;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CorrectnessRules.InvariantLoopCondition,
        CorrectnessRules.LoopCounterNeverStepped,
        CorrectnessRules.LoopStepsAwayFromBound,
        CorrectnessRules.LoopBodyNeverRuns);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.WhileStatement, SyntaxKind.ForStatement);
    }

    /// <summary>Analyzes one loop.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // A for-specific defect takes precedence: it is the more specific report, and reporting it
        // suppresses the invariant-condition diagnostic that would otherwise fire on the same loop.
        if (context.Node is ForStatementSyntax forStatement && TryReportForDefect(context, forStatement))
        {
            return;
        }

        ReportInvariantCondition(context);
    }

    /// <summary>Reports the first for-loop counter defect that applies, if any.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="forStatement">The for statement.</param>
    /// <returns><see langword="true"/> when a diagnostic was reported.</returns>
    private static bool TryReportForDefect(SyntaxNodeAnalysisContext context, ForStatementSyntax forStatement)
    {
        if (forStatement.Condition is null)
        {
            return false;
        }

        return TryReportNeverStepped(context, forStatement)
            || TryReportStepsAway(context, forStatement)
            || TryReportBodyNeverRuns(context, forStatement);
    }

    /// <summary>Reports SST2411 for a for loop whose declared, tested counter is never advanced.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="forStatement">The for statement.</param>
    /// <returns><see langword="true"/> when the loop was reported.</returns>
    private static bool TryReportNeverStepped(SyntaxNodeAnalysisContext context, ForStatementSyntax forStatement)
    {
        if (forStatement.Declaration is not { } declaration || forStatement.Condition is not { } condition)
        {
            return false;
        }

        var variables = declaration.Variables;
        if (GetFirstTestedName(variables, condition) is not { } firstTested || !IsSimpleCondition(condition))
        {
            return false;
        }

        var scan = new StepScan(declaration, condition);
        WalkSteps(forStatement, ref scan);
        if (scan.Opaque || scan.SteppedTested)
        {
            return false;
        }

        if (!ReadsOnlyLocals(context, condition))
        {
            return false;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.LoopCounterNeverStepped,
            condition.GetLocation(),
            firstTested));
        return true;
    }

    /// <summary>Reports SST2412 for a for loop whose step moves the counter away from its bound.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="forStatement">The for statement.</param>
    /// <returns><see langword="true"/> when the loop was reported.</returns>
    private static bool TryReportStepsAway(SyntaxNodeAnalysisContext context, ForStatementSyntax forStatement)
    {
        if (!TryClassifyStep(forStatement, out var counter, out var ascending)
            || !TryReadRelation(forStatement.Condition!, counter, out var comparison, out var canonical)
            || BodyWritesCounter(forStatement, counter)
            || StepsToward(ascending, canonical))
        {
            return false;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.LoopStepsAwayFromBound,
            comparison.GetLocation(),
            counter));
        return true;
    }

    /// <summary>Reports SST2413 for a for loop whose condition is already false at the counter's start.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="forStatement">The for statement.</param>
    /// <returns><see langword="true"/> when the loop was reported.</returns>
    private static bool TryReportBodyNeverRuns(SyntaxNodeAnalysisContext context, ForStatementSyntax forStatement)
    {
        // A consistent direction is required here: an inconsistent one is SST2412's shape.
        if (!TryClassifyStep(forStatement, out var counter, out var ascending)
            || !TryReadRelation(forStatement.Condition!, counter, out var comparison, out var canonical)
            || BodyWritesCounter(forStatement, counter)
            || !StepsToward(ascending, canonical)
            || !StartsFalse(context, forStatement, counter, comparison, canonical))
        {
            return false;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.LoopBodyNeverRuns,
            comparison.GetLocation(),
            counter));
        return true;
    }

    /// <summary>Returns whether a comparison folds to false at the counter's constant starting value.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="forStatement">The for statement.</param>
    /// <param name="counter">The counter's name.</param>
    /// <param name="comparison">The comparison expression.</param>
    /// <param name="canonical">The comparison kind, counter on the left.</param>
    /// <returns><see langword="true"/> when the body cannot run once.</returns>
    private static bool StartsFalse(
        SyntaxNodeAnalysisContext context,
        ForStatementSyntax forStatement,
        string counter,
        BinaryExpressionSyntax comparison,
        SyntaxKind canonical)
    {
        if (GetCounterStart(forStatement, counter) is not { } start || GetBound(comparison, counter) is not { } bound)
        {
            return false;
        }

        return TryGetInt64(context, start, out var startValue)
            && TryGetInt64(context, bound, out var boundValue)
            && !EvaluatesTrue(canonical, startValue, boundValue);
    }

    /// <summary>Returns whether the step direction and the comparison side agree.</summary>
    /// <param name="ascending">Whether the step increases the counter.</param>
    /// <param name="canonical">The comparison kind, counter on the left.</param>
    /// <returns><see langword="true"/> when the counter moves toward the bound.</returns>
    private static bool StepsToward(bool ascending, SyntaxKind canonical)
        => ascending
            ? canonical is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            : canonical is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>Reports SST2406 for a loop whose condition can never change.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void ReportInvariantCondition(SyntaxNodeAnalysisContext context)
    {
        var loop = context.Node;
        if (GetCondition(loop) is not { } condition || !IsSimpleCondition(condition))
        {
            return;
        }

        if (LoopChangesCondition(loop, condition) || !ReadsOnlyLocals(context, condition))
        {
            return;
        }

        if (GetFirstIdentifier(condition) is not { } first)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.InvariantLoopCondition,
            condition.GetLocation(),
            first.Identifier.ValueText));
    }

    /// <summary>Gets the first declared variable that the condition reads.</summary>
    /// <param name="variables">The initializer's declared variables.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <returns>The first tested declared name, or <see langword="null"/> when none is read.</returns>
    private static string? GetFirstTestedName(SeparatedSyntaxList<VariableDeclaratorSyntax> variables, ExpressionSyntax condition)
    {
        for (var i = 0; i < variables.Count; i++)
        {
            var name = variables[i].Identifier.ValueText;
            if (Mentions(condition, name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Walks a for loop's incrementers and body, looking for a step of a tested counter or an opaque write site.</summary>
    /// <param name="forStatement">The for statement.</param>
    /// <param name="scan">The scan state.</param>
    private static void WalkSteps(ForStatementSyntax forStatement, ref StepScan scan)
    {
        var incrementors = forStatement.Incrementors;
        for (var i = 0; i < incrementors.Count && !scan.Done; i++)
        {
            WalkStepNode(incrementors[i], ref scan);
        }

        if (forStatement.Statement is not { } body || scan.Done)
        {
            return;
        }

        WalkStepNode(body, ref scan);
    }

    /// <summary>Walks one node for a step of a tested counter or an opaque construct.</summary>
    /// <param name="node">The node to walk.</param>
    /// <param name="scan">The scan state.</param>
    private static void WalkStepNode(SyntaxNode node, ref StepScan scan)
    {
        if (scan.Done || !VisitStepNode(node, ref scan))
        {
            return;
        }

        DescendantTraversalHelper.VisitDescendants<SyntaxNode, StepScan>(node, ref scan, VisitStepNode);
    }

    /// <summary>Records whether a node steps a tested counter or hides writes from the scan.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once the scan can stop.</returns>
    private static bool VisitStepNode(SyntaxNode node, ref StepScan scan)
    {
        if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or RefExpressionSyntax)
        {
            scan.Opaque = true;
        }
        else if (GetWrittenName(node) is { } written && IsTestedDeclared(scan.Declaration, scan.Condition, written))
        {
            scan.SteppedTested = true;
        }
        else
        {
            return true;
        }

        return false;
    }

    /// <summary>Returns whether a name is both a declared counter and read by the condition.</summary>
    /// <param name="declaration">The for loop's declaration.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="name">The written name.</param>
    /// <returns><see langword="true"/> when the write advances a tested counter.</returns>
    private static bool IsTestedDeclared(VariableDeclarationSyntax declaration, ExpressionSyntax condition, string name)
    {
        var variables = declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (variables[i].Identifier.ValueText == name)
            {
                return Mentions(condition, name);
            }
        }

        return false;
    }

    /// <summary>Classifies a for loop's single incrementer into a counter and a direction.</summary>
    /// <param name="forStatement">The for statement.</param>
    /// <param name="counter">The counter's name.</param>
    /// <param name="ascending">Whether the step increases the counter.</param>
    /// <returns><see langword="true"/> for a single, constant-signed step of one named counter.</returns>
    private static bool TryClassifyStep(ForStatementSyntax forStatement, out string counter, out bool ascending)
    {
        counter = string.Empty;
        ascending = false;
        var incrementors = forStatement.Incrementors;
        if (incrementors.Count != 1)
        {
            return false;
        }

        return incrementors[0] switch
        {
            PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax name } postfix
                => TrySetStep(postfix.Kind(), name, SyntaxKind.PostIncrementExpression, SyntaxKind.PostDecrementExpression, out counter, out ascending),
            PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax name } prefix
                => TrySetStep(prefix.Kind(), name, SyntaxKind.PreIncrementExpression, SyntaxKind.PreDecrementExpression, out counter, out ascending),
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } assignment when IsConstantCompoundStep(assignment)
                => TrySetStep(assignment.Kind(), name, SyntaxKind.AddAssignmentExpression, SyntaxKind.SubtractAssignmentExpression, out counter, out ascending),
            _ => false,
        };
    }

    /// <summary>Records a counter and direction when the operator increments or decrements.</summary>
    /// <param name="kind">The step operator kind.</param>
    /// <param name="name">The counter identifier.</param>
    /// <param name="up">The kind that increases the counter.</param>
    /// <param name="down">The kind that decreases the counter.</param>
    /// <param name="counter">The counter's name.</param>
    /// <param name="ascending">Whether the step increases the counter.</param>
    /// <returns><see langword="true"/> when the operator is one of the two steps.</returns>
    private static bool TrySetStep(SyntaxKind kind, IdentifierNameSyntax name, SyntaxKind up, SyntaxKind down, out string counter, out bool ascending)
    {
        counter = name.Identifier.ValueText;
        ascending = kind == up;
        return kind == up || kind == down;
    }

    /// <summary>Returns whether a compound assignment steps by a positive integer literal.</summary>
    /// <param name="assignment">The compound assignment.</param>
    /// <returns><see langword="true"/> for <c>+=</c>/<c>-=</c> by a positive literal.</returns>
    private static bool IsConstantCompoundStep(AssignmentExpressionSyntax assignment)
        => (assignment.IsKind(SyntaxKind.AddAssignmentExpression) || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression))
            && assignment.Right is LiteralExpressionSyntax { Token.Value: int step } && step > 0;

    /// <summary>Reads a relational condition, isolating the comparison against the counter.</summary>
    /// <param name="condition">The loop's condition.</param>
    /// <param name="counter">The counter's name.</param>
    /// <param name="comparison">The comparison expression.</param>
    /// <param name="canonical">The comparison kind rewritten as if the counter were on the left.</param>
    /// <returns><see langword="true"/> for a single relational comparison naming the counter exactly once.</returns>
    private static bool TryReadRelation(ExpressionSyntax condition, string counter, out BinaryExpressionSyntax comparison, out SyntaxKind canonical)
    {
        comparison = null!;
        canonical = SyntaxKind.None;
        if (condition is not BinaryExpressionSyntax binary || !IsRelational(binary.Kind()))
        {
            return false;
        }

        var counterLeft = binary.Left is IdentifierNameSyntax left && left.Identifier.ValueText == counter;
        var counterRight = binary.Right is IdentifierNameSyntax right && right.Identifier.ValueText == counter;
        if (counterLeft == counterRight)
        {
            return false;
        }

        var boundSide = counterLeft ? binary.Right : binary.Left;
        if (Mentions(boundSide, counter))
        {
            return false;
        }

        comparison = binary;
        canonical = counterLeft ? binary.Kind() : Mirror(binary.Kind());
        return true;
    }

    /// <summary>Gets the bound expression a comparison tests the counter against.</summary>
    /// <param name="comparison">The comparison expression.</param>
    /// <param name="counter">The counter's name.</param>
    /// <returns>The bound expression.</returns>
    private static ExpressionSyntax GetBound(BinaryExpressionSyntax comparison, string counter)
        => comparison.Left is IdentifierNameSyntax left && left.Identifier.ValueText == counter ? comparison.Right : comparison.Left;

    /// <summary>Gets the expression a for loop initializes the counter to.</summary>
    /// <param name="forStatement">The for statement.</param>
    /// <param name="counter">The counter's name.</param>
    /// <returns>The initializer expression, or <see langword="null"/> when the counter is not initialized here.</returns>
    private static ExpressionSyntax? GetCounterStart(ForStatementSyntax forStatement, string counter)
    {
        if (forStatement.Declaration is { } declaration)
        {
            var variables = declaration.Variables;
            for (var i = 0; i < variables.Count; i++)
            {
                if (variables[i].Identifier.ValueText == counter)
                {
                    return variables[i].Initializer?.Value;
                }
            }

            return null;
        }

        var initializers = forStatement.Initializers;
        for (var i = 0; i < initializers.Count; i++)
        {
            if (initializers[i] is AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } assignment
                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && name.Identifier.ValueText == counter)
            {
                return assignment.Right;
            }
        }

        return null;
    }

    /// <summary>Reads an expression's compile-time value as a 64-bit integer.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The constant value.</param>
    /// <returns><see langword="true"/> for an integral compile-time constant.</returns>
    private static bool TryGetInt64(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, out long value)
    {
        value = 0;
        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        if (constant is not { HasValue: true, Value: { } boxed }
            || boxed is not (int or long or short or byte or sbyte or ushort or uint))
        {
            return false;
        }

        value = Convert.ToInt64(boxed, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Evaluates a canonical comparison of two constants.</summary>
    /// <param name="canonical">The comparison kind, counter on the left.</param>
    /// <param name="start">The counter's starting value.</param>
    /// <param name="bound">The bound value.</param>
    /// <returns><see langword="true"/> when the comparison holds at the start.</returns>
    private static bool EvaluatesTrue(SyntaxKind canonical, long start, long bound) => canonical switch
    {
        SyntaxKind.LessThanExpression => start < bound,
        SyntaxKind.LessThanOrEqualExpression => start <= bound,
        SyntaxKind.GreaterThanExpression => start > bound,
        SyntaxKind.GreaterThanOrEqualExpression => start >= bound,
        _ => true,
    };

    /// <summary>Returns whether a for loop's body writes the counter (outside the incrementers).</summary>
    /// <param name="forStatement">The for statement.</param>
    /// <param name="counter">The counter's name.</param>
    /// <returns><see langword="true"/> when the body could re-step the counter.</returns>
    private static bool BodyWritesCounter(ForStatementSyntax forStatement, string counter)
    {
        if (forStatement.Statement is not { } body)
        {
            return false;
        }

        var scan = new CounterWriteScan(counter);
        WalkCounterWrites(body, ref scan);
        return scan.Written;
    }

    /// <summary>Walks a node for a write to the counter.</summary>
    /// <param name="node">The node to walk.</param>
    /// <param name="scan">The scan state.</param>
    private static void WalkCounterWrites(SyntaxNode node, ref CounterWriteScan scan)
    {
        if (scan.Written || !VisitCounterWrite(node, ref scan))
        {
            return;
        }

        DescendantTraversalHelper.VisitDescendants<SyntaxNode, CounterWriteScan>(node, ref scan, VisitCounterWrite);
    }

    /// <summary>Records whether a node writes the counter.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once a write is found.</returns>
    private static bool VisitCounterWrite(SyntaxNode node, ref CounterWriteScan scan)
    {
        if (GetWrittenName(node) != scan.Counter)
        {
            return true;
        }

        scan.Written = true;
        return false;
    }

    /// <summary>Returns whether a syntax kind is a relational comparison.</summary>
    /// <param name="kind">The syntax kind.</param>
    /// <returns><see langword="true"/> for <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c>.</returns>
    private static bool IsRelational(SyntaxKind kind)
        => kind is SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression;

    /// <summary>Mirrors a relational comparison as if its operands were swapped.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <returns>The mirrored comparison kind.</returns>
    private static SyntaxKind Mirror(SyntaxKind kind) => kind switch
    {
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        _ => kind,
    };

    /// <summary>Returns whether anything in the loop can change what the condition reads, or leave early.</summary>
    /// <param name="loop">The loop statement.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <returns><see langword="true"/> when the loop is not provably invariant.</returns>
    private static bool LoopChangesCondition(SyntaxNode loop, ExpressionSyntax condition)
    {
        var scan = new BodyScan(condition);
        if (loop is ForStatementSyntax forStatement)
        {
            var incrementors = forStatement.Incrementors;
            for (var i = 0; i < incrementors.Count; i++)
            {
                Walk(incrementors[i], ref scan);
            }
        }

        if (GetBody(loop) is not { } body)
        {
            return scan.Stop;
        }

        Walk(body, ref scan);
        return scan.Stop;
    }

    /// <summary>Walks one node of the loop, looking for a write or a way out.</summary>
    /// <param name="node">The node to walk.</param>
    /// <param name="scan">The scan state.</param>
    private static void Walk(SyntaxNode node, ref BodyScan scan)
    {
        if (scan.Stop || !VisitLoopNode(node, ref scan))
        {
            return;
        }

        DescendantTraversalHelper.VisitDescendants<SyntaxNode, BodyScan>(node, ref scan, VisitLoopNode);
    }

    /// <summary>Records whether one node ends the loop or writes what the condition reads.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once the loop is disqualified, which stops the walk.</returns>
    private static bool VisitLoopNode(SyntaxNode node, ref BodyScan scan)
    {
        var disqualified = IsEarlyExit(node)
            || node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or RefExpressionSyntax
            || (GetWrittenName(node) is { } written && Mentions(scan.Condition, written));
        if (!disqualified)
        {
            return true;
        }

        scan.Stop = true;
        return false;
    }

    /// <summary>Returns whether a node is a way out of the loop other than the condition.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> for a jump or a throw.</returns>
    private static bool IsEarlyExit(SyntaxNode node)
        => node is BreakStatementSyntax
            or ReturnStatementSyntax
            or ThrowStatementSyntax
            or ThrowExpressionSyntax
            or GotoStatementSyntax
            or YieldStatementSyntax;

    /// <summary>Gets the name a node writes to, if it writes to one.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The written name, or <see langword="null"/> when the node writes nothing.</returns>
    /// <remarks>
    /// An assignment of any kind, an increment, a decrement and a by-reference argument are all writes. A
    /// plain by-value argument is not: nothing a method does to its copy can be seen here.
    /// </remarks>
    private static string? GetWrittenName(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax target } => target.Identifier.ValueText,
        PrefixUnaryExpressionSyntax prefix when IsStep(prefix.RawKind) => NameOf(prefix.Operand),
        PostfixUnaryExpressionSyntax postfix when IsStep(postfix.RawKind) => NameOf(postfix.Operand),
        ArgumentSyntax argument when !argument.RefOrOutKeyword.IsKind(SyntaxKind.None) => NameOf(argument.Expression),
        _ => null,
    };

    /// <summary>Returns whether an operator kind steps its operand up or down.</summary>
    /// <param name="rawKind">The operator's raw kind.</param>
    /// <returns><see langword="true"/> for an increment or a decrement.</returns>
    private static bool IsStep(int rawKind) => rawKind is (int)SyntaxKind.PreIncrementExpression
        or (int)SyntaxKind.PreDecrementExpression
        or (int)SyntaxKind.PostIncrementExpression
        or (int)SyntaxKind.PostDecrementExpression;

    /// <summary>Gets an expression's identifier, when it is one.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The identifier, or <see langword="null"/>.</returns>
    private static string? NameOf(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax identifier ? identifier.Identifier.ValueText : null;

    /// <summary>Returns whether every variable the condition reads is a local or a parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="condition">The loop's condition.</param>
    /// <returns><see langword="true"/> when nothing outside the method can change what the condition reads.</returns>
    /// <remarks>
    /// A field — even a private one — may be written by something the loop calls, or by another thread, and a
    /// property read is a call. Only a local or a parameter is provably still whatever the loop last made it.
    /// </remarks>
    private static bool ReadsOnlyLocals(SyntaxNodeAnalysisContext context, ExpressionSyntax condition)
    {
        var scan = new LocalScan(context);
        if (condition is IdentifierNameSyntax self && !VisitConditionName(self, ref scan))
        {
            return false;
        }

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, LocalScan>(condition, ref scan, VisitConditionName);
        return !scan.Rejected && scan.Count > 0;
    }

    /// <summary>Rejects the loop when a condition variable is anything but a local or a parameter.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once the loop is rejected, which stops the walk.</returns>
    private static bool VisitConditionName(IdentifierNameSyntax identifier, ref LocalScan scan)
    {
        var symbol = scan.Context.SemanticModel.GetSymbolInfo(identifier, scan.Context.CancellationToken).Symbol;
        if (symbol is not (ILocalSymbol or IParameterSymbol) or ILocalSymbol { IsConst: true })
        {
            scan.Rejected = true;
            return false;
        }

        scan.Count++;
        return true;
    }

    /// <summary>Returns whether a condition is built only from names, literals and operators.</summary>
    /// <param name="condition">The loop's condition.</param>
    /// <returns><see langword="true"/> when nothing in it can produce a different value on its own.</returns>
    private static bool IsSimpleCondition(ExpressionSyntax condition)
    {
        var scan = default(ShapeScan);
        if (!VisitConditionNode(condition, ref scan))
        {
            return false;
        }

        DescendantTraversalHelper.VisitDescendants<SyntaxNode, ShapeScan>(condition, ref scan, VisitConditionNode);
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
            || (node is PrefixUnaryExpressionSyntax prefix && !IsStep(prefix.RawKind)))
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

    /// <summary>Gets the first variable a condition reads, which is the one the message names.</summary>
    /// <param name="condition">The loop's condition.</param>
    /// <returns>The first identifier, or <see langword="null"/> when there is none.</returns>
    private static IdentifierNameSyntax? GetFirstIdentifier(ExpressionSyntax condition)
    {
        if (condition is IdentifierNameSyntax self)
        {
            return self;
        }

        var scan = default(FirstNameScan);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, FirstNameScan>(condition, ref scan, VisitFirstName);
        return scan.First;
    }

    /// <summary>Records the first identifier in a condition.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/>, which stops the walk at the first one.</returns>
    private static bool VisitFirstName(IdentifierNameSyntax identifier, ref FirstNameScan scan)
    {
        scan.First = identifier;
        return false;
    }

    /// <summary>Gets a loop's condition.</summary>
    /// <param name="loop">The loop statement.</param>
    /// <returns>The condition, or <see langword="null"/> when the loop has none.</returns>
    private static ExpressionSyntax? GetCondition(SyntaxNode loop) => loop switch
    {
        WhileStatementSyntax whileStatement => whileStatement.Condition,
        ForStatementSyntax forStatement => forStatement.Condition,
        _ => null,
    };

    /// <summary>Gets a loop's body.</summary>
    /// <param name="loop">The loop statement.</param>
    /// <returns>The body, or <see langword="null"/> when the loop has none.</returns>
    private static StatementSyntax? GetBody(SyntaxNode loop) => loop switch
    {
        WhileStatementSyntax whileStatement => whileStatement.Statement,
        ForStatementSyntax forStatement => forStatement.Statement,
        _ => null,
    };

    /// <summary>The state threaded through the loop body's scan.</summary>
    /// <param name="Condition">The loop's condition.</param>
    private record struct BodyScan(ExpressionSyntax Condition)
    {
        /// <summary>Gets or sets a value indicating whether the loop is disqualified.</summary>
        public bool Stop { get; set; }
    }

    /// <summary>The state threaded through the never-stepped scan.</summary>
    /// <param name="Declaration">The for loop's declaration.</param>
    /// <param name="Condition">The loop's condition.</param>
    private record struct StepScan(VariableDeclarationSyntax Declaration, ExpressionSyntax Condition)
    {
        /// <summary>Gets or sets a value indicating whether a tested counter is stepped.</summary>
        public bool SteppedTested { get; set; }

        /// <summary>Gets or sets a value indicating whether a construct hides writes from the scan.</summary>
        public bool Opaque { get; set; }

        /// <summary>Gets a value indicating whether the scan can stop.</summary>
        public readonly bool Done => SteppedTested || Opaque;
    }

    /// <summary>The state threaded through the counter-write scan.</summary>
    /// <param name="Counter">The counter's name.</param>
    private record struct CounterWriteScan(string Counter)
    {
        /// <summary>Gets or sets a value indicating whether the counter is written.</summary>
        public bool Written { get; set; }
    }

    /// <summary>The state threaded through the condition's shape scan.</summary>
    private record struct ShapeScan
    {
        /// <summary>Gets or sets the number of variables the condition reads.</summary>
        public int Identifiers { get; set; }

        /// <summary>Gets or sets a value indicating whether the condition was rejected.</summary>
        public bool Rejected { get; set; }
    }

    /// <summary>The state threaded through the condition's symbol scan.</summary>
    /// <param name="Context">The syntax node context.</param>
    private record struct LocalScan(SyntaxNodeAnalysisContext Context)
    {
        /// <summary>Gets or sets the number of variables resolved.</summary>
        public int Count { get; set; }

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

    /// <summary>The state threaded through the search for a condition's first variable.</summary>
    private record struct FirstNameScan
    {
        /// <summary>Gets or sets the first identifier found.</summary>
        public IdentifierNameSyntax? First { get; set; }
    }
}
