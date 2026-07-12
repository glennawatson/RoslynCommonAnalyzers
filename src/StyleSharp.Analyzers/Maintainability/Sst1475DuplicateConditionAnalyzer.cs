// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a condition that repeats one already tested earlier in the same construct (SST1475): an
/// <c>if</c>/<c>else if</c> chain whose later branch restates an earlier branch's condition, a <c>switch</c>
/// whose case labels repeat, and a <c>switch</c> expression whose arms repeat a pattern. The first match wins
/// in all three, so the later branch is unreachable and the condition that was meant to differ never got
/// written.
/// </summary>
/// <remarks>
/// <para>
/// Only side-effect-free conditions are compared. <c>if (Check()) ... else if (Check())</c> reads as a
/// duplicate but is not one: the second call may legitimately answer differently, and telling the author to
/// delete the branch would be wrong. An invocation, an <c>await</c>, an assignment, an increment or an object
/// creation anywhere inside the condition therefore disqualifies it.
/// </para>
/// <para>
/// A chain is walked exactly once. Only the head of a chain is analyzed — an <c>else if</c> is the
/// <see cref="ElseClauseSyntax.Statement"/> of its predecessor, so it is rejected on the parent check and a
/// five-branch chain costs one walk rather than five. The comparison itself is allocation-free: the chain is
/// re-entered through its <c>else</c> pointers instead of being copied into a list, which matters because a
/// two-branch chain — the overwhelmingly common shape — would otherwise allocate on every clean file. The
/// structural comparison runs first and the side-effect scan only behind it, so nothing walks a condition's
/// subtree until two conditions have already been proven identical.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1475DuplicateConditionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest comparable labels a switch needs before any pair can repeat.</summary>
    private const int MinimumComparableLabels = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.DuplicateCondition);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIfChain, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
    }

    /// <summary>Analyzes one <c>if</c>/<c>else if</c> chain, from its head.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// The parent check is what makes the walk happen once: an <c>else if</c> is parented by an
    /// <see cref="ElseClauseSyntax"/>, so every branch but the head returns immediately, and the chain below the
    /// head is walked from here rather than re-entered per branch. A lone <c>if</c> has no second branch, so the
    /// loop never runs and no condition is read at all.
    /// </remarks>
    private static void AnalyzeIfChain(SyntaxNodeAnalysisContext context)
    {
        var head = (IfStatementSyntax)context.Node;
        if (head.Parent is ElseClauseSyntax)
        {
            return;
        }

        var later = GetNextBranch(head);
        while (later is not null)
        {
            ReportRepeatedCondition(context, head, later);
            later = GetNextBranch(later);
        }
    }

    /// <summary>Reports one branch's condition when an earlier branch in the chain already tested it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="head">The chain's first branch.</param>
    /// <param name="later">The branch being checked.</param>
    /// <remarks>
    /// The scan runs forward from the head so the <em>earliest</em> duplicate is the one named in the message —
    /// that is the branch the author meant to keep.
    /// </remarks>
    private static void ReportRepeatedCondition(SyntaxNodeAnalysisContext context, IfStatementSyntax head, IfStatementSyntax later)
    {
        var condition = later.Condition;
        IfStatementSyntax? earlier = head;
        while (earlier is not null && !ReferenceEquals(earlier, later))
        {
            if (SyntaxFactory.AreEquivalent(earlier.Condition, condition, topLevel: false))
            {
                // The conditions are structurally identical, so if one calls out the other does too: a single
                // scan settles both, and a re-tested impure condition is left alone entirely.
                if (IsSideEffectFree(condition))
                {
                    Report(context, condition.GetLocation(), earlier.Condition);
                }

                return;
            }

            earlier = GetNextBranch(earlier);
        }
    }

    /// <summary>Gets the next branch of an <c>if</c> chain.</summary>
    /// <param name="branch">The current branch.</param>
    /// <returns>The <c>else if</c> that follows, or <see langword="null"/> at the end of the chain.</returns>
    /// <remarks>A plain <c>else</c> ends the chain: it tests no condition, so there is nothing to repeat.</remarks>
    private static IfStatementSyntax? GetNextBranch(IfStatementSyntax branch) => branch.Else?.Statement as IfStatementSyntax;

    /// <summary>Analyzes a <c>switch</c> statement for case labels that repeat.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
    {
        var sections = ((SwitchStatementSyntax)context.Node).Sections;
        if (!HasComparableLabels(sections))
        {
            return;
        }

        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                var later = labels[labelIndex];
                if (later.IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    continue;
                }

                if (FindEarlierLabel(sections, sectionIndex, labelIndex, later) is { } earlier)
                {
                    Report(context, later.GetLocation(), earlier);
                }
            }
        }
    }

    /// <summary>Returns whether a switch declares at least two labels that can repeat one another.</summary>
    /// <param name="sections">The switch's sections.</param>
    /// <returns><see langword="true"/> when two or more non-default labels are present.</returns>
    /// <remarks>The default label is excluded: the language already allows only one of it.</remarks>
    private static bool HasComparableLabels(SyntaxList<SwitchSectionSyntax> sections)
    {
        var comparable = 0;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (labels[labelIndex].IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    continue;
                }

                comparable++;
                if (comparable >= MinimumComparableLabels)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Finds the first label, in document order, that the given label repeats.</summary>
    /// <param name="sections">The switch's sections.</param>
    /// <param name="laterSection">The index of the section holding the later label.</param>
    /// <param name="laterIndex">The index of the later label inside its section.</param>
    /// <param name="later">The later label.</param>
    /// <returns>The earlier label, or <see langword="null"/> when nothing repeats or the label is impure.</returns>
    /// <remarks>
    /// The whole label is compared, not just its constant: <c>case 1 when flag:</c> only repeats
    /// <c>case 1 when flag:</c>, and never <c>case 1:</c>, which tests something else.
    /// </remarks>
    private static SwitchLabelSyntax? FindEarlierLabel(
        SyntaxList<SwitchSectionSyntax> sections,
        int laterSection,
        int laterIndex,
        SwitchLabelSyntax later)
    {
        for (var sectionIndex = 0; sectionIndex <= laterSection; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            var limit = sectionIndex == laterSection ? laterIndex : labels.Count;
            for (var labelIndex = 0; labelIndex < limit; labelIndex++)
            {
                var earlier = labels[labelIndex];
                if (!SyntaxFactory.AreEquivalent(earlier, later, topLevel: false))
                {
                    continue;
                }

                return IsSideEffectFree(later) ? earlier : null;
            }
        }

        return null;
    }

    /// <summary>Analyzes a <c>switch</c> expression for arms that repeat a pattern.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
    {
        var arms = ((SwitchExpressionSyntax)context.Node).Arms;
        if (arms.Count < MinimumComparableLabels)
        {
            return;
        }

        for (var laterIndex = 1; laterIndex < arms.Count; laterIndex++)
        {
            var later = arms[laterIndex];

            // A discard arm is the catch-all, not a test; the language already allows only one that matters.
            if (later.Pattern.IsKind(SyntaxKind.DiscardPattern))
            {
                continue;
            }

            ReportRepeatedArm(context, arms, laterIndex, later);
        }
    }

    /// <summary>Reports one switch-expression arm when an earlier arm already tests the same case.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="arms">The switch expression's arms.</param>
    /// <param name="laterIndex">The index of the arm being checked.</param>
    /// <param name="later">The arm being checked.</param>
    private static void ReportRepeatedArm(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<SwitchExpressionArmSyntax> arms,
        int laterIndex,
        SwitchExpressionArmSyntax later)
    {
        for (var earlierIndex = 0; earlierIndex < laterIndex; earlierIndex++)
        {
            var earlier = arms[earlierIndex];
            if (!IsSameCase(earlier, later))
            {
                continue;
            }

            if (IsSideEffectFree(later.Pattern) && (later.WhenClause is null || IsSideEffectFree(later.WhenClause)))
            {
                Report(context, GetCaseLocation(later), earlier.Pattern);
            }

            return;
        }
    }

    /// <summary>Returns whether two switch-expression arms select on the same case.</summary>
    /// <param name="earlier">The earlier arm.</param>
    /// <param name="later">The later arm.</param>
    /// <returns><see langword="true"/> when the patterns and the guards both match.</returns>
    /// <remarks>Only the selector is compared. Two arms that select the same case but produce different values are still a bug — the second one cannot run.</remarks>
    private static bool IsSameCase(SwitchExpressionArmSyntax earlier, SwitchExpressionArmSyntax later)
        => SyntaxFactory.AreEquivalent(earlier.Pattern, later.Pattern, topLevel: false)
            && SyntaxFactory.AreEquivalent(earlier.WhenClause, later.WhenClause, topLevel: false);

    /// <summary>Gets the location covering an arm's selector, excluding the value it produces.</summary>
    /// <param name="arm">The switch-expression arm.</param>
    /// <returns>The location spanning the pattern and its guard.</returns>
    private static Location GetCaseLocation(SwitchExpressionArmSyntax arm)
    {
        var end = arm.WhenClause is { } when ? when.Span.End : arm.Pattern.Span.End;
        return Location.Create(arm.SyntaxTree, TextSpan.FromBounds(arm.Pattern.SpanStart, end));
    }

    /// <summary>Reports a repeated condition, naming the line of the one it repeats.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="location">The location of the unreachable condition.</param>
    /// <param name="earlier">The condition that already covers this case.</param>
    private static void Report(SyntaxNodeAnalysisContext context, Location location, SyntaxNode earlier)
    {
        var line = earlier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.DuplicateCondition,
            location,
            line.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Returns whether evaluating a condition twice is guaranteed to produce the same answer.</summary>
    /// <param name="node">The condition, case label, pattern or guard.</param>
    /// <returns><see langword="true"/> when nothing inside it can observably change state.</returns>
    /// <remarks>
    /// This is the whole reason a repeated condition is worth reporting: if the condition is pure, the second
    /// branch provably cannot run. Once anything in it can change state or answer differently — an invocation
    /// above all — the repetition may be intentional, and the rule stays quiet.
    /// </remarks>
    private static bool IsSideEffectFree(SyntaxNode node)
    {
        if (HasSideEffect(node))
        {
            return false;
        }

        var free = true;
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, bool>(node, ref free, MatchSideEffect);
        return free;
    }

    /// <summary>Records the first side-effecting descendant and stops the walk.</summary>
    /// <param name="node">The current descendant.</param>
    /// <param name="free">Set to <see langword="false"/> once a side effect is found.</param>
    /// <returns><see langword="false"/> to stop the walk.</returns>
    private static bool MatchSideEffect(SyntaxNode node, ref bool free)
    {
        if (!HasSideEffect(node))
        {
            return true;
        }

        free = false;
        return false;
    }

    /// <summary>Returns whether one node can change state or answer differently on a second evaluation.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a call, an allocation, an await, an assignment or an increment.</returns>
    /// <remarks>
    /// An allocation counts because a fresh instance is a fresh identity: two <c>new</c> expressions that read
    /// identically do not compare equal by reference.
    /// </remarks>
    private static bool HasSideEffect(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax => true,
        BaseObjectCreationExpressionSyntax => true,
        ArrayCreationExpressionSyntax => true,
        ImplicitArrayCreationExpressionSyntax => true,
        AnonymousObjectCreationExpressionSyntax => true,
        AwaitExpressionSyntax => true,
        AssignmentExpressionSyntax => true,
        _ => IsIncrementOrDecrement(node),
    };

    /// <summary>Returns whether a node increments or decrements its operand, in either position.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for <c>++x</c>, <c>--x</c>, <c>x++</c> and <c>x--</c>.</returns>
    /// <remarks>Only a prefix or postfix unary expression carries one of these kinds, so the kind alone settles it.</remarks>
    private static bool IsIncrementOrDecrement(SyntaxNode node) => node.RawKind
        is (int)SyntaxKind.PreIncrementExpression
        or (int)SyntaxKind.PreDecrementExpression
        or (int)SyntaxKind.PostIncrementExpression
        or (int)SyntaxKind.PostDecrementExpression;
}
