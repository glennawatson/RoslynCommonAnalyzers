// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>while</c> or <c>for</c> whose condition reads only variables that nothing in the loop ever
/// writes (SST2406). The condition has already decided its answer before the first iteration: the loop either
/// never runs or never stops.
/// </summary>
/// <remarks>
/// <para>
/// Being wrong here is easy, so the rule refuses to guess. It stays silent whenever anything could make the
/// condition change, or could end the loop without it changing:
/// </para>
/// <list type="bullet">
/// <item><description>A <c>break</c>, <c>return</c>, <c>throw</c>, <c>goto</c> or <c>yield break</c> anywhere
/// in the body. That is a real way out, and a condition that never changes is then a deliberate one —
/// <c>while (true)</c> written the long way.</description></item>
/// <item><description>A condition that is not built purely from locals, literals and operators. A call may
/// return something different each time; a field or a property may be written by another thread, or by
/// something the loop calls; an element or a member may be anything at all.</description></item>
/// <item><description>A lambda or a local function in the body, which could write the variable somewhere the
/// scan cannot follow — as could a <c>ref</c> alias to it.</description></item>
/// </list>
/// <para>
/// What is left is a condition over locals and parameters that the loop demonstrably never assigns, never
/// increments and never passes by reference. Nothing else can change a local, so the condition is fixed.
/// </para>
/// <para>
/// The clean path never binds. The condition's shape rejects most loops outright, and a loop whose body
/// writes a condition variable — which is nearly all of them — is rejected on syntax before the locals are
/// resolved.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2406InvariantLoopConditionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The most condition variables the rule will consider.</summary>
    private const int MaximumConditionVariables = 4;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.InvariantLoopCondition);

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
