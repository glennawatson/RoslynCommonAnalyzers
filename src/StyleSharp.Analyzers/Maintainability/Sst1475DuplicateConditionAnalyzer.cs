// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a condition that repeats one already tested earlier (SST1475): an <c>if</c>/<c>else if</c> chain
/// whose later branch restates an earlier branch's condition, a <c>switch</c> whose case labels repeat, a
/// <c>switch</c> expression whose arms repeat a pattern, and two <b>adjacent</b> <c>if</c> statements in one
/// block that test the same thing.
/// </summary>
/// <remarks>
/// <para>
/// The first three shapes are unreachable code: the first match wins, so the later branch never runs and the
/// condition that was meant to differ never got written. The adjacent pair is not — both <c>if</c> statements
/// run — so it is reported with a different consequence: either the two were meant to be one <c>if</c>, or
/// one of the conditions is wrong. The message says which of the two the reader is looking at.
/// </para>
/// <para>
/// Only side-effect-free conditions are compared. <c>if (Check()) ... else if (Check())</c> reads as a
/// duplicate but is not one: the second call may legitimately answer differently, and telling the author to
/// delete the branch would be wrong. An invocation, an <c>await</c>, an assignment, an increment or an object
/// creation anywhere inside the condition therefore disqualifies it.
/// </para>
/// <para>
/// <b>The adjacent pair needs one guarantee the chain does not.</b> In a chain, nothing runs between the two
/// tests. Between two sequential <c>if</c> statements, the first one's <em>body</em> runs — and if that body
/// can change what the condition reads, the second test is not a duplicate at all but a re-read of something
/// that has since moved. So the pair is reported only when the first <c>if</c>, body and <c>else</c>
/// included, contains no call, allocation or <c>await</c> (any of which can touch anything), and writes
/// nothing but plain local variables the condition does not read. That is deliberately strict: it is the
/// difference between a rule that finds copy-paste bugs and one that invents them.
/// </para>
/// <para>
/// A chain is walked exactly once. Only the head of a chain is analyzed — an <c>else if</c> is the
/// <see cref="ElseClauseSyntax.Statement"/> of its predecessor, so it is rejected on the parent check and a
/// five-branch chain costs one walk rather than five. The comparison itself is allocation-free: the chain is
/// re-entered through its <c>else</c> pointers instead of being copied into a list, which matters because a
/// two-branch chain — the overwhelmingly common shape — would otherwise allocate on every clean file. The
/// structural comparison runs first and the side-effect scan only behind it, so nothing walks a condition's
/// subtree until two conditions have already been proven identical, and the block scan is one indexed pass
/// over statements that reads no condition at all until it finds two <c>if</c> statements side by side.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1475DuplicateConditionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest comparable labels a switch needs before any pair can repeat.</summary>
    private const int MinimumComparableLabels = 2;

    /// <summary>The consequence named in the message when the repeated condition guards dead code.</summary>
    private const string UnreachableConsequence = "the branch it guards can never run";

    /// <summary>The consequence named in the message when both of two sequential tests run.</summary>
    private const string SequentialConsequence = "both tests run, so merge them into one 'if' — or one of the conditions is wrong";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.DuplicateCondition);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIfChain, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
    }

    /// <summary>Analyzes a block for two adjacent <c>if</c> statements that test the same condition.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// Only an immediately adjacent pair is considered. Anything at all between the two — a statement the
    /// rule cannot see through, a declaration, a call — is a reason for the condition to have changed, and
    /// the rule is not in the business of proving that it did not.
    /// </remarks>
    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
    {
        var statements = ((BlockSyntax)context.Node).Statements;
        for (var i = 1; i < statements.Count; i++)
        {
            if (statements[i - 1] is not IfStatementSyntax earlier || statements[i] is not IfStatementSyntax later)
            {
                continue;
            }

            ReportRepeatedSequentialCondition(context, earlier, later);
        }
    }

    /// <summary>Reports the second of two sequential <c>if</c> statements that test the same condition.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="earlier">The first <c>if</c> statement.</param>
    /// <param name="later">The <c>if</c> statement immediately after it.</param>
    private static void ReportRepeatedSequentialCondition(SyntaxNodeAnalysisContext context, IfStatementSyntax earlier, IfStatementSyntax later)
    {
        var condition = later.Condition;
        if (!SyntaxFactory.AreEquivalent(earlier.Condition, condition, topLevel: false)
            || !IsSideEffectFree(condition)
            || CanChangeCondition(context, earlier, condition))
        {
            return;
        }

        Report(context, condition.GetLocation(), earlier.Condition, SequentialConsequence);
    }

    /// <summary>Returns whether running the first <c>if</c> could change what the repeated condition reads.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="earlier">The first <c>if</c> statement.</param>
    /// <param name="condition">The condition both statements test.</param>
    /// <returns><see langword="true"/> when the second test may legitimately answer differently.</returns>
    /// <remarks>
    /// The condition is already known to be pure, so it reads names and nothing else. What the first branch
    /// must not do is move any of them. A call, an allocation or an <c>await</c> is treated as moving
    /// everything, because it can: a method it reaches may write any field the condition reads. A write is
    /// allowed only to a plain, non-<c>ref</c> local or parameter whose name the condition never mentions —
    /// which can change no field, no property and no other variable, and so cannot change the answer.
    /// </remarks>
    private static bool CanChangeCondition(SyntaxNodeAnalysisContext context, IfStatementSyntax earlier, ExpressionSyntax condition)
    {
        var scan = new ConditionScan(context.SemanticModel, CollectReadNames(condition), context.CancellationToken);
        VisitForHazards(earlier.Statement, ref scan);
        if (earlier.Else is { } elseClause)
        {
            VisitForHazards(elseClause, ref scan);
        }

        return scan.Hazard;
    }

    /// <summary>Walks one branch of the first <c>if</c>, stopping at the first thing that could move the condition.</summary>
    /// <param name="branch">The branch to walk.</param>
    /// <param name="scan">The scan state.</param>
    private static void VisitForHazards(SyntaxNode branch, ref ConditionScan scan)
    {
        // The traversal helper visits descendants only, so the branch itself is judged first: a bare
        // 'if (c) x = Compute();' has its call in the statement node, not below it.
        if (!MatchHazard(branch, ref scan))
        {
            return;
        }

        DescendantTraversalHelper.VisitDescendants<SyntaxNode, ConditionScan>(branch, ref scan, MatchHazard);
    }

    /// <summary>Records the first node in a branch that could change the condition, and stops the walk.</summary>
    /// <param name="node">The current node.</param>
    /// <param name="scan">The scan state.</param>
    /// <returns><see langword="false"/> once a hazard is found.</returns>
    private static bool MatchHazard(SyntaxNode node, ref ConditionScan scan)
    {
        if (IsUnrestrictedSideEffect(node))
        {
            scan.Hazard = true;
            return false;
        }

        if (GetWriteTarget(node) is not { } target || scan.IsHarmlessWrite(target))
        {
            return true;
        }

        scan.Hazard = true;
        return false;
    }

    /// <summary>Gets the expression a node writes to, if it writes to one.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns>The written expression, or <see langword="null"/> when the node writes nothing.</returns>
    private static ExpressionSyntax? GetWriteTarget(SyntaxNode node) => node switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left,
        PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix) => prefix.Operand,
        PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix) => postfix.Operand,
        _ => null,
    };

    /// <summary>Collects the identifiers a condition reads.</summary>
    /// <param name="condition">The condition, already known to be side-effect-free.</param>
    /// <returns>The set of names it mentions.</returns>
    /// <remarks>
    /// Names, not symbols: a write to <c>value</c> disqualifies a condition that reads <c>value</c>, and also
    /// one that reads some other <c>value</c> in another scope. Erring towards silence is the whole point.
    /// </remarks>
    private static HashSet<string> CollectReadNames(ExpressionSyntax condition)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (condition is IdentifierNameSyntax root)
        {
            names.Add(root.Identifier.ValueText);
        }

        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HashSet<string>>(condition, ref names, CollectName);
        return names;
    }

    /// <summary>Records one identifier a condition reads.</summary>
    /// <param name="name">The identifier.</param>
    /// <param name="names">The set being built.</param>
    /// <returns>Always <see langword="true"/>, so the whole condition is walked.</returns>
    private static bool CollectName(IdentifierNameSyntax name, ref HashSet<string> names)
    {
        names.Add(name.Identifier.ValueText);
        return true;
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
                    Report(context, condition.GetLocation(), earlier.Condition, UnreachableConsequence);
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
                    Report(context, later.GetLocation(), earlier, UnreachableConsequence);
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
                Report(context, GetCaseLocation(later), earlier.Pattern, UnreachableConsequence);
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

    /// <summary>Reports a repeated condition, naming the line of the one it repeats and what follows from that.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="location">The location of the repeated condition.</param>
    /// <param name="earlier">The condition it repeats.</param>
    /// <param name="consequence">What the repetition means here: dead code, or two tests that both run.</param>
    private static void Report(SyntaxNodeAnalysisContext context, Location location, SyntaxNode earlier, string consequence)
    {
        var line = earlier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.DuplicateCondition,
            location,
            line.ToString(CultureInfo.InvariantCulture),
            consequence));
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
    private static bool HasSideEffect(SyntaxNode node)
        => IsUnrestrictedSideEffect(node) || node is AssignmentExpressionSyntax || IsIncrementOrDecrement(node);

    /// <summary>Returns whether one node can change state the rule cannot put a bound on.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a call, an allocation or an <c>await</c>.</returns>
    /// <remarks>
    /// These are the effects with no visible target. A method may write any field it can reach, so a call
    /// anywhere in the first branch is a reason to assume the condition moved. An allocation counts on its own
    /// account too: a fresh instance is a fresh identity, so two <c>new</c> expressions that read identically
    /// do not compare equal by reference. A write, by contrast, names what it changes, and is judged on that.
    /// </remarks>
    private static bool IsUnrestrictedSideEffect(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax => true,
        BaseObjectCreationExpressionSyntax => true,
        ArrayCreationExpressionSyntax => true,
        ImplicitArrayCreationExpressionSyntax => true,
        AnonymousObjectCreationExpressionSyntax => true,
        AwaitExpressionSyntax => true,
        _ => false,
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

    /// <summary>The state threaded through the scan of the first <c>if</c>'s branches.</summary>
    private sealed class ConditionScan
    {
        /// <summary>The semantic model, used to tell a local apart from a field.</summary>
        private readonly SemanticModel _model;

        /// <summary>The identifiers the repeated condition reads.</summary>
        private readonly HashSet<string> _readNames;

        /// <summary>A token that cancels the binding the scan does.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>Initializes a new instance of the <see cref="ConditionScan"/> class.</summary>
        /// <param name="model">The semantic model.</param>
        /// <param name="readNames">The identifiers the repeated condition reads.</param>
        /// <param name="cancellationToken">A token that cancels the binding the scan does.</param>
        public ConditionScan(SemanticModel model, HashSet<string> readNames, CancellationToken cancellationToken)
        {
            _model = model;
            _readNames = readNames;
            _cancellationToken = cancellationToken;
        }

        /// <summary>Gets or sets a value indicating whether the branch can change the condition's value.</summary>
        public bool Hazard { get; set; }

        /// <summary>Returns whether a write provably cannot change what the condition reads.</summary>
        /// <param name="target">The written expression.</param>
        /// <returns><see langword="true"/> when the write is confined to an unrelated variable.</returns>
        /// <remarks>
        /// Three things have to hold. The target must be a bare identifier — a <c>x.Y = …</c> runs a property
        /// setter, which can do anything, and an <c>a[i] = …</c> can be seen through any alias of the array.
        /// The name must be one the condition never reads. And it must bind to a by-value local or parameter:
        /// a <c>ref</c> local can be an alias for the very field the condition reads, and writing through it
        /// would change the answer while naming something else entirely.
        /// </remarks>
        public bool IsHarmlessWrite(ExpressionSyntax target)
            => target is IdentifierNameSyntax identifier
                && !_readNames.Contains(identifier.Identifier.ValueText)
                && _model.GetSymbolInfo(identifier, _cancellationToken).Symbol is ILocalSymbol { RefKind: RefKind.None }
                    or IParameterSymbol { RefKind: RefKind.None };
    }
}
