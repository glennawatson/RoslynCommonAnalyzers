// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a collection element that is assigned twice in adjacent statements with nothing reading it in
/// between (SST1487), so the first value is thrown away. The index or the key was almost certainly meant to
/// differ — the loop counter that was supposed to advance did not.
/// </summary>
/// <remarks>
/// <para>
/// Only a literal, obvious repeat is reported; there is no dataflow analysis here and there is deliberately
/// no code fix, because which of the two assignments the author meant is not knowable from the code. The two
/// statements must be adjacent in the same statement list, both must be plain <c>=</c> assignments to an
/// element access, and the receiver and every index must be the same side-effect-free expression, so
/// <c>map[Next()] = 1; map[Next()] = 2;</c> stays clean. A compound assignment reads before it writes and can
/// never lose a value, and it is excluded by construction: <c>+=</c> and <c>??=</c> are not
/// <see cref="SyntaxKind.SimpleAssignmentExpression"/>. If the second assignment's right-hand side mentions
/// the collection at all — <c>v[i] = v[i] + 1;</c>, or even <c>v[i] = v.Length;</c> — the first write is read
/// rather than lost, and the rule stays quiet.
/// </para>
/// <para>
/// The scan is one indexed pass per statement list, and the clean path is a pair of type tests per statement:
/// a statement that is not an element assignment is rejected on its shape before anything else is looked at,
/// which is every statement in almost every block. No semantic model is touched at all — the whole rule is
/// syntactic, so it costs nothing to bind and works the same on broken code.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1487OverwrittenCollectionElementAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest statements a list needs before any pair can exist.</summary>
    private const int MinimumStatements = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.OverwrittenCollectionElement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchSection, SyntaxKind.SwitchSection);
    }

    /// <summary>Analyzes the statements of one block.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
        => AnalyzeStatements(context, ((BlockSyntax)context.Node).Statements);

    /// <summary>Analyzes the statements of one switch section, which are not wrapped in a block.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSwitchSection(SyntaxNodeAnalysisContext context)
        => AnalyzeStatements(context, ((SwitchSectionSyntax)context.Node).Statements);

    /// <summary>Walks a statement list once, checking each adjacent pair.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="statements">The statements to walk.</param>
    /// <remarks>
    /// Each statement is classified once and the result is carried into the next iteration, so a list of N
    /// statements costs N classifications rather than the 2N a per-statement lookaround would cost. Three
    /// assignments in a row produce two diagnostics, on the first and the second — both of those writes are
    /// lost, and both are worth seeing.
    /// </remarks>
    private static void AnalyzeStatements(SyntaxNodeAnalysisContext context, SyntaxList<StatementSyntax> statements)
    {
        if (statements.Count < MinimumStatements)
        {
            return;
        }

        var earlier = TryGetElementWrite(statements[0]);
        for (var i = 1; i < statements.Count; i++)
        {
            var later = TryGetElementWrite(statements[i]);
            if (earlier is not null && later is not null)
            {
                ReportLostWrite(context, statements[i - 1], statements[i], earlier, later);
            }

            earlier = later;
        }
    }

    /// <summary>Classifies a statement as a plain write to a collection element, or as nothing.</summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The assignment, or <see langword="null"/> when the statement is anything else.</returns>
    /// <remarks>
    /// Requiring <see cref="SyntaxKind.SimpleAssignmentExpression"/> is what excludes every compound form.
    /// <c>sum[i] += x</c> and <c>cache[k] ??= v</c> read the element before they write it, so neither can ever
    /// throw a value away, and neither is an <c>=</c>.
    /// </remarks>
    private static AssignmentExpressionSyntax? TryGetElementWrite(StatementSyntax statement)
        => statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
            && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            && assignment.Left is ElementAccessExpressionSyntax
                ? assignment
                : null;

    /// <summary>Reports the earlier of two writes when the later one provably overwrites it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="earlierStatement">The earlier statement.</param>
    /// <param name="laterStatement">The later statement.</param>
    /// <param name="earlier">The earlier assignment.</param>
    /// <param name="later">The later assignment.</param>
    /// <remarks>
    /// The diagnostic lands on the earlier element access, because that is the write that does nothing — the
    /// squiggle marks the dead line rather than the line that killed it.
    /// </remarks>
    private static void ReportLostWrite(
        SyntaxNodeAnalysisContext context,
        StatementSyntax earlierStatement,
        StatementSyntax laterStatement,
        AssignmentExpressionSyntax earlier,
        AssignmentExpressionSyntax later)
    {
        // Two statements that are adjacent in the tree may not be adjacent in every build. A directive between
        // them means an inactive region was skipped, and whatever it contains could read the element.
        if (earlierStatement.ContainsDirectives || laterStatement.ContainsDirectives)
        {
            return;
        }

        var target = (ElementAccessExpressionSyntax)earlier.Left;
        var overwrite = (ElementAccessExpressionSyntax)later.Left;
        if (!SyntaxFactory.AreEquivalent(target, overwrite, topLevel: false)
            || !IsStableTarget(target)
            || MentionsReceiver(later.Right, target.Expression))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.OverwrittenCollectionElement,
            target.GetLocation(),
            target.ToString()));
    }

    /// <summary>Returns whether an element access denotes the same slot every time it is written.</summary>
    /// <param name="access">The element access on the left of the assignment.</param>
    /// <returns><see langword="true"/> when the receiver and every index can be evaluated twice for the same slot.</returns>
    /// <remarks>
    /// <see cref="SideEffectFreeExpression"/> deliberately rejects an element access, which is what makes
    /// <c>grid[i][j] = 1;</c> and <c>map[keys[0]] = 1;</c> both stay clean: an inner indexer is a call this rule
    /// cannot see through, so two spellings of it are not provably the same slot. That is a conservative miss,
    /// not a wrong report.
    /// </remarks>
    private static bool IsStableTarget(ElementAccessExpressionSyntax access)
    {
        if (!SideEffectFreeExpression.IsSideEffectFree(access.Expression))
        {
            return false;
        }

        var arguments = access.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            // `map[ref key]` hands the index out to be written; nothing about it is stable.
            if (!argument.RefKindKeyword.IsKind(SyntaxKind.None) || !SideEffectFreeExpression.IsSideEffectFree(argument.Expression))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression reads the collection whose element is being written.</summary>
    /// <param name="expression">The right-hand side of the later assignment.</param>
    /// <param name="receiver">The collection the element belongs to.</param>
    /// <returns><see langword="true"/> when the value being assigned depends on the collection.</returns>
    /// <remarks>
    /// The whole receiver is looked for, not just the element: <c>v[i] = v[i] + 1;</c> is a read-modify-write
    /// and is obviously not a lost write, but <c>v[i] = v.Length;</c> and <c>v[i] = Sum(v);</c> could also read
    /// the value the first statement stored, so the first write is not provably dead. Rejecting the whole
    /// family costs a handful of real reports and buys back every false one. The search only runs once two
    /// adjacent writes to the same slot have already been found, so it never touches clean code.
    /// </remarks>
    private static bool MentionsReceiver(ExpressionSyntax expression, ExpressionSyntax receiver)
    {
        var search = new ReceiverSearch(receiver);
        if (!search.Visit(expression))
        {
            return true;
        }

        DescendantTraversalHelper.VisitDescendants<ExpressionSyntax, ReceiverSearch>(
            expression,
            ref search,
            static (ExpressionSyntax node, ref ReceiverSearch state) => state.Visit(node));

        return search.Found;
    }

    /// <summary>Searches a subtree for an expression that reads the same collection as the assignment target.</summary>
    private struct ReceiverSearch : IEquatable<ReceiverSearch>
    {
        /// <summary>The receiver being looked for.</summary>
        private readonly ExpressionSyntax _receiver;

        /// <summary>Whether the receiver has been found.</summary>
        private bool _found;

        /// <summary>Initializes a new instance of the <see cref="ReceiverSearch"/> struct.</summary>
        /// <param name="receiver">The receiver being looked for.</param>
        public ReceiverSearch(ExpressionSyntax receiver)
        {
            _receiver = receiver;
            _found = false;
        }

        /// <summary>Gets a value indicating whether the receiver was found.</summary>
        public readonly bool Found => _found;

        /// <summary>Visits one expression and returns whether the walk should continue.</summary>
        /// <param name="node">The expression.</param>
        /// <returns><see langword="false"/> once the receiver has been found.</returns>
        public bool Visit(ExpressionSyntax node)
        {
            if (!SyntaxFactory.AreEquivalent(_receiver, node, topLevel: false))
            {
                return true;
            }

            _found = true;
            return false;
        }

        /// <summary>Returns whether two searches are equivalent.</summary>
        /// <param name="other">The other search.</param>
        /// <returns><see langword="true"/> when both search for the same receiver and agree on the answer.</returns>
        public readonly bool Equals(ReceiverSearch other)
            => _found == other._found && ReferenceEquals(_receiver, other._receiver);

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is ReceiverSearch other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => _found ? 1 : 0;
    }
}
