// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports conditional constructs whose branches duplicate one another, in a single walk over each
/// <c>if</c>/<c>else</c> chain, conditional expression, <c>switch</c> statement and <c>switch</c> expression.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1476 — <b>every</b> branch of an exhaustive construct has the same body, so the
/// condition decides nothing.</description></item>
/// <item><description>SST2414 — <b>any two</b> branches share a body while others differ, so one of them was
/// probably meant to differ. SST1476 takes precedence: when every branch matches, only it is reported.</description></item>
/// </list>
/// <para>
/// The structural body comparison is trivia-insensitive and bails on the first difference; the two-arm scan
/// compares span lengths before tokens, and only runs after the exhaustive check has declined the construct.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IdenticalBranchesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The size, in statements, of a branch whose body is a single expression.</summary>
    private const int ExpressionBodySize = 1;

    /// <summary>The number of branches an <c>if</c>/<c>else</c> has before any <c>else if</c> is added.</summary>
    private const int PlainIfElseBranchCount = 2;

    /// <summary>The fewest branches a construct needs before "every branch is the same" means anything.</summary>
    private const int MinimumBranchCount = 2;

    /// <summary>The fewest statements a shared <c>if</c>-branch body needs before SST2414 reports it.</summary>
    private const int PairMinimumStatements = 2;

    /// <summary>The phrase used for a conditional expression, whose two arms are always both of them.</summary>
    private const string ConditionalArmsPhrase = "Both arms of this conditional expression";

    /// <summary>The phrase used for a plain <c>if</c>/<c>else</c>, whose two branches are always both of them.</summary>
    private const string IfElseBranchesPhrase = "Both branches of this 'if'";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.IdenticalBranches,
        CorrectnessRules.DuplicateBranchImplementation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the per-compilation option cache, then analyzes every conditional construct.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions>();
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeIfChain(nodeContext, optionsByTree), SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeConditionalExpression(nodeContext, optionsByTree), SyntaxKind.ConditionalExpression);
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeSwitchStatement(nodeContext, optionsByTree), SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(nodeContext => AnalyzeSwitchExpression(nodeContext, optionsByTree), SyntaxKind.SwitchExpression);
    }

    /// <summary>Analyzes one <c>if</c> chain, from its head.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void AnalyzeIfChain(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var head = (IfStatementSyntax)context.Node;
        if (head.Parent is ElseClauseSyntax)
        {
            return;
        }

        if (TryGetExhaustiveChain(head, out var terminalElse, out var branches)
            && ChainBodiesMatch(head, terminalElse)
            && IsBodyLargeEnough(context, optionsByTree, GetStatementCount(head.Statement)))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.IdenticalBranches,
                head.IfKeyword.GetLocation(),
                DescribeIfChain(branches)));
            return;
        }

        ReportDuplicateIfBranch(context, head);
    }

    /// <summary>Reports the first pair of <c>if</c> branches with the same body.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="head">The chain's first branch.</param>
    private static void ReportDuplicateIfBranch(SyntaxNodeAnalysisContext context, IfStatementSyntax head)
    {
        // Walk the conditioned branches (the head and each 'else if'), ignoring the terminal 'else', and
        // report the first branch whose body matches an earlier one.
        var branches = CollectConditionedBranches(head);
        for (var i = 0; i < branches.Count; i++)
        {
            if (GetStatementCount(branches[i].Statement) < PairMinimumStatements)
            {
                continue;
            }

            for (var j = i + 1; j < branches.Count; j++)
            {
                if (HaveSameBody(branches[i].Statement, branches[j].Statement))
                {
                    context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.DuplicateBranchImplementation, branches[j].IfKeyword.GetLocation()));
                    return;
                }
            }
        }
    }

    /// <summary>Collects the conditioned branches of an <c>if</c> chain, excluding the terminal <c>else</c>.</summary>
    /// <param name="head">The chain's first branch.</param>
    /// <returns>The <c>if</c> and <c>else if</c> branches in order.</returns>
    private static List<IfStatementSyntax> CollectConditionedBranches(IfStatementSyntax head)
    {
        var branches = new List<IfStatementSyntax>();
        var current = head;
        while (true)
        {
            branches.Add(current);
            if (current.Else?.Statement is IfStatementSyntax next)
            {
                current = next;
                continue;
            }

            return branches;
        }
    }

    /// <summary>Walks an <c>if</c> chain to its end, looking for the <c>else</c> that makes it exhaustive.</summary>
    /// <param name="head">The chain's first branch.</param>
    /// <param name="terminalElse">The body of the trailing <c>else</c>, when there is one.</param>
    /// <param name="branches">The number of branches in the chain, counting the trailing <c>else</c>.</param>
    /// <returns><see langword="true"/> when the chain ends in a plain <c>else</c>.</returns>
    private static bool TryGetExhaustiveChain(
        IfStatementSyntax head,
        [NotNullWhen(true)] out StatementSyntax? terminalElse,
        out int branches)
    {
        terminalElse = null;
        branches = 1;

        var current = head;
        while (current.Else is { } elseClause)
        {
            branches++;
            if (elseClause.Statement is IfStatementSyntax next)
            {
                current = next;
                continue;
            }

            terminalElse = elseClause.Statement;
            return true;
        }

        return false;
    }

    /// <summary>Returns whether every branch of an <c>if</c> chain has the same body as the first.</summary>
    /// <param name="head">The chain's first branch.</param>
    /// <param name="terminalElse">The body of the trailing <c>else</c>.</param>
    /// <returns><see langword="true"/> when all bodies match.</returns>
    private static bool ChainBodiesMatch(IfStatementSyntax head, StatementSyntax terminalElse)
    {
        var reference = head.Statement;
        var current = head;
        while (current.Else?.Statement is IfStatementSyntax next)
        {
            if (!HaveSameBody(reference, next.Statement))
            {
                return false;
            }

            current = next;
        }

        return HaveSameBody(reference, terminalElse);
    }

    /// <summary>Analyzes a conditional expression and reports it when both arms produce the same value.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void AnalyzeConditionalExpression(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;
        if (!SyntaxFactory.AreEquivalent(conditional.WhenTrue, conditional.WhenFalse, topLevel: false)
            || !IsBodyLargeEnough(context, optionsByTree, ExpressionBodySize))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.IdenticalBranches,
            conditional.QuestionToken.GetLocation(),
            ConditionalArmsPhrase));
    }

    /// <summary>Analyzes a <c>switch</c> statement for a fully or partially duplicated body.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void AnalyzeSwitchStatement(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        var sections = switchStatement.Sections;
        if (sections.Count >= MinimumBranchCount
            && HasDefaultLabel(sections)
            && SectionBodiesMatch(sections)
            && IsBodyLargeEnough(context, optionsByTree, sections[0].Statements.Count))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.IdenticalBranches,
                switchStatement.SwitchKeyword.GetLocation(),
                Describe(sections.Count, " branches of this 'switch'")));
            return;
        }

        ReportDuplicateSection(context, switchStatement, sections);
    }

    /// <summary>Reports the first pair of switch sections that run the same body.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="sections">The switch's sections.</param>
    private static void ReportDuplicateSection(SyntaxNodeAnalysisContext context, SwitchStatementSyntax switchStatement, SyntaxList<SwitchSectionSyntax> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].Statements.Count == 0 || HasDefaultOrGotoLabel(sections[i]))
            {
                continue;
            }

            for (var j = i + 1; j < sections.Count; j++)
            {
                if (!HasDefaultOrGotoLabel(sections[j])
                    && AreEquivalentStatements(sections[i].Statements, sections[j].Statements)
                    && !ContainsGoto(switchStatement))
                {
                    context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.DuplicateBranchImplementation, sections[j].Labels[0].GetLocation()));
                    return;
                }
            }
        }
    }

    /// <summary>Analyzes a <c>switch</c> expression for a fully or partially duplicated value.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void AnalyzeSwitchExpression(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;
        var arms = switchExpression.Arms;
        if (arms.Count >= MinimumBranchCount
            && ArmValuesMatch(arms)
            && IsExhaustive(context, switchExpression)
            && IsBodyLargeEnough(context, optionsByTree, ExpressionBodySize))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.IdenticalBranches,
                switchExpression.SwitchKeyword.GetLocation(),
                Describe(arms.Count, " arms of this 'switch' expression")));
            return;
        }

        ReportDuplicateArm(context, arms);
    }

    /// <summary>Reports the first pair of switch-expression arms that produce the same value.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="arms">The switch expression's arms.</param>
    private static void ReportDuplicateArm(SyntaxNodeAnalysisContext context, SeparatedSyntaxList<SwitchExpressionArmSyntax> arms)
    {
        for (var i = 0; i < arms.Count; i++)
        {
            if (IsDiscardArm(arms[i]))
            {
                continue;
            }

            for (var j = i + 1; j < arms.Count; j++)
            {
                if (!IsDiscardArm(arms[j]) && SyntaxFactory.AreEquivalent(arms[i].Expression, arms[j].Expression, topLevel: false))
                {
                    context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.DuplicateBranchImplementation, arms[j].Pattern.GetLocation()));
                    return;
                }
            }
        }
    }

    /// <summary>Returns whether a switch-expression arm matches the discard pattern with no guard.</summary>
    /// <param name="arm">The switch-expression arm.</param>
    /// <returns><see langword="true"/> for a <c>_</c> arm.</returns>
    private static bool IsDiscardArm(SwitchExpressionArmSyntax arm)
        => arm.Pattern is DiscardPatternSyntax && arm.WhenClause is null;

    /// <summary>Returns whether a switch section carries a <c>default</c> label.</summary>
    /// <param name="section">The switch section.</param>
    /// <returns><see langword="true"/> for a section that includes <c>default</c>.</returns>
    private static bool HasDefaultOrGotoLabel(SwitchSectionSyntax section)
    {
        var labels = section.Labels;
        for (var i = 0; i < labels.Count; i++)
        {
            if (labels[i].IsKind(SyntaxKind.DefaultSwitchLabel))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a switch statement contains a <c>goto case</c> / <c>goto default</c>.</summary>
    /// <param name="switchStatement">The switch statement.</param>
    /// <returns><see langword="true"/> when a jump could target a section by label.</returns>
    private static bool ContainsGoto(SwitchStatementSyntax switchStatement)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendants<GotoStatementSyntax, bool>(switchStatement, ref found, VisitGoto);
        return found;
    }

    /// <summary>Records that a <c>goto case</c> / <c>goto default</c> was found.</summary>
    /// <param name="node">The goto statement.</param>
    /// <param name="found">Whether a targeting jump was found.</param>
    /// <returns><see langword="false"/> once one is found, stopping the walk.</returns>
    private static bool VisitGoto(GotoStatementSyntax node, ref bool found)
    {
        if (!node.IsKind(SyntaxKind.GotoCaseStatement) && !node.IsKind(SyntaxKind.GotoDefaultStatement))
        {
            return true;
        }

        found = true;
        return false;
    }

    /// <summary>Returns whether a switch statement handles every value it is not given a case for.</summary>
    /// <param name="sections">The switch's sections.</param>
    /// <returns><see langword="true"/> when a <c>default</c> label is present.</returns>
    private static bool HasDefaultLabel(SyntaxList<SwitchSectionSyntax> sections)
    {
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            if (HasDefaultOrGotoLabel(sections[sectionIndex]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether every section of a <c>switch</c> statement runs the same statements.</summary>
    /// <param name="sections">The switch's sections.</param>
    /// <returns><see langword="true"/> when all section bodies match.</returns>
    private static bool SectionBodiesMatch(SyntaxList<SwitchSectionSyntax> sections)
    {
        var reference = sections[0].Statements;
        for (var sectionIndex = 1; sectionIndex < sections.Count; sectionIndex++)
        {
            if (!AreEquivalentStatements(reference, sections[sectionIndex].Statements))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether every arm of a <c>switch</c> expression produces the same value.</summary>
    /// <param name="arms">The switch expression's arms.</param>
    /// <returns><see langword="true"/> when all arm expressions match.</returns>
    private static bool ArmValuesMatch(SeparatedSyntaxList<SwitchExpressionArmSyntax> arms)
    {
        var reference = arms[0].Expression;
        for (var armIndex = 1; armIndex < arms.Count; armIndex++)
        {
            if (!SyntaxFactory.AreEquivalent(reference, arms[armIndex].Expression, topLevel: false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a <c>switch</c> expression covers every input the compiler can see.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="switchExpression">The switch expression.</param>
    /// <returns><see langword="true"/> when no input falls through to a thrown match failure.</returns>
    private static bool IsExhaustive(SyntaxNodeAnalysisContext context, SwitchExpressionSyntax switchExpression)
        => context.SemanticModel.GetOperation(switchExpression, context.CancellationToken) is ISwitchExpressionOperation { IsExhaustive: true };

    /// <summary>Returns whether a duplicated body is big enough to be worth reporting.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="statements">The size of one branch's body, in statements.</param>
    /// <returns><see langword="true"/> when the body meets the configured minimum.</returns>
    private static bool IsBodyLargeEnough(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree,
        int statements)
        => statements >= GetOptions(context, optionsByTree).MinimumStatements;

    /// <summary>Reads the settings for the construct's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static IdenticalBranchesOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = IdenticalBranchesOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Returns whether two branch bodies run the same statements in the same order.</summary>
    /// <param name="first">The first branch's body.</param>
    /// <param name="second">The second branch's body.</param>
    /// <returns><see langword="true"/> when the bodies match, ignoring trivia and braces.</returns>
    private static bool HaveSameBody(StatementSyntax first, StatementSyntax second)
    {
        var count = GetStatementCount(first);
        if (count != GetStatementCount(second))
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            if (!SyntaxFactory.AreEquivalent(GetStatement(first, index), GetStatement(second, index), topLevel: false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether two statement lists run the same statements in the same order.</summary>
    /// <param name="first">The first statement list.</param>
    /// <param name="second">The second statement list.</param>
    /// <returns><see langword="true"/> when the lists match, ignoring trivia.</returns>
    private static bool AreEquivalentStatements(SyntaxList<StatementSyntax> first, SyntaxList<StatementSyntax> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (!SyntaxFactory.AreEquivalent(first[index], second[index], topLevel: false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the number of statements a branch body runs.</summary>
    /// <param name="body">The branch's body.</param>
    /// <returns>The block's statement count, or 1 for a bare embedded statement.</returns>
    private static int GetStatementCount(StatementSyntax body)
        => body is BlockSyntax block ? block.Statements.Count : 1;

    /// <summary>Gets one statement of a branch body by position.</summary>
    /// <param name="body">The branch's body.</param>
    /// <param name="index">The statement's position in the body.</param>
    /// <returns>The statement at that position.</returns>
    private static StatementSyntax GetStatement(StatementSyntax body, int index)
        => body is BlockSyntax block ? block.Statements[index] : body;

    /// <summary>Names what is duplicated in an <c>if</c> chain.</summary>
    /// <param name="branches">The number of branches, counting the trailing <c>else</c>.</param>
    /// <returns>The phrase the message opens with.</returns>
    private static string DescribeIfChain(int branches)
        => branches == PlainIfElseBranchCount ? IfElseBranchesPhrase : Describe(branches, " branches of this 'if' chain");

    /// <summary>Names what is duplicated in a construct with a countable number of branches.</summary>
    /// <param name="count">The number of branches or arms.</param>
    /// <param name="suffix">The phrase naming the construct.</param>
    /// <returns>The phrase the message opens with.</returns>
    private static string Describe(int count, string suffix)
        => "All " + count.ToString(CultureInfo.InvariantCulture) + suffix;
}
