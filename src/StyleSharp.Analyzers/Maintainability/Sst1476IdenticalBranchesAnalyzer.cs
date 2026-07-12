// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a conditional construct whose branches all do the same thing (SST1476): an <c>if</c>/<c>else</c>,
/// an <c>if</c>/<c>else if</c>/<c>else</c> chain, a conditional expression, a <c>switch</c> statement and a
/// <c>switch</c> expression. When every branch has the same body the condition decides nothing, and the code
/// claims to distinguish cases it does not — usually because one branch was meant to differ.
/// </summary>
/// <remarks>
/// <para>
/// Only an <b>exhaustive</b> construct is reported. An <c>if</c> without an <c>else</c>, or a <c>switch</c>
/// without a <c>default</c>, is a different shape: its unwritten branch does nothing, which the written ones
/// do not, so the condition still decides something. Exhaustiveness is syntactic for the statement forms — a
/// terminal <c>else</c>, a <c>default</c> label — and semantic for a <c>switch</c> expression, where the
/// compiler's own exhaustiveness answer covers a complete <c>bool</c> or enum switch that never spells out a
/// discard arm.
/// </para>
/// <para>
/// The minimum body size that counts is configured with <c>stylesharp.SST1476.minimum_statements</c> and
/// defaults to 1, so even a one-statement body is reported. An expression-bodied arm — a conditional
/// expression's, a switch expression's — counts as one statement, so raising the minimum drops those shapes
/// with the rest of the trivially short duplicates.
/// </para>
/// <para>
/// Ordered so a clean file pays almost nothing. Exhaustiveness is a pointer walk and is settled first; the
/// body comparison is a trivia-insensitive structural match that bails on the first difference and never
/// copies a statement list; and only a construct that has already been proven duplicated reads its options or
/// touches the semantic model. Each chain is analyzed exactly once, from its head — an <c>else if</c> is
/// parented by an <see cref="ElseClauseSyntax"/> and returns immediately.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1476IdenticalBranchesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The size, in statements, of a branch whose body is a single expression.</summary>
    private const int ExpressionBodySize = 1;

    /// <summary>The number of branches an <c>if</c>/<c>else</c> has before any <c>else if</c> is added.</summary>
    private const int PlainIfElseBranchCount = 2;

    /// <summary>The fewest branches a construct needs before "every branch is the same" means anything.</summary>
    private const int MinimumBranchCount = 2;

    /// <summary>The phrase used for a conditional expression, whose two arms are always both of them.</summary>
    private const string ConditionalArmsPhrase = "Both arms of this conditional expression";

    /// <summary>The phrase used for a plain <c>if</c>/<c>else</c>, whose two branches are always both of them.</summary>
    private const string IfElseBranchesPhrase = "Both branches of this 'if'";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.IdenticalBranches);

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

    /// <summary>Analyzes one <c>if</c> chain, from its head, and reports it when every branch is the same.</summary>
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

        if (!TryGetExhaustiveChain(head, out var terminalElse, out var branches)
            || !ChainBodiesMatch(head, terminalElse)
            || !IsBodyLargeEnough(context, optionsByTree, GetStatementCount(head.Statement)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.IdenticalBranches,
            head.IfKeyword.GetLocation(),
            DescribeIfChain(branches)));
    }

    /// <summary>Walks an <c>if</c> chain to its end, looking for the <c>else</c> that makes it exhaustive.</summary>
    /// <param name="head">The chain's first branch.</param>
    /// <param name="terminalElse">The body of the trailing <c>else</c>, when there is one.</param>
    /// <param name="branches">The number of branches in the chain, counting the trailing <c>else</c>.</param>
    /// <returns><see langword="true"/> when the chain ends in a plain <c>else</c>.</returns>
    /// <remarks>A chain with no trailing <c>else</c> has an unwritten branch that does nothing, which is not the same as what the written ones do.</remarks>
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

    /// <summary>Analyzes a <c>switch</c> statement and reports it when every section runs the same body.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void AnalyzeSwitchStatement(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        var sections = switchStatement.Sections;
        if (sections.Count < MinimumBranchCount
            || !HasDefaultLabel(sections)
            || !SectionBodiesMatch(sections)
            || !IsBodyLargeEnough(context, optionsByTree, sections[0].Statements.Count))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.IdenticalBranches,
            switchStatement.SwitchKeyword.GetLocation(),
            Describe(sections.Count, " branches of this 'switch'")));
    }

    /// <summary>Returns whether a <c>switch</c> statement handles every value it is not given a case for.</summary>
    /// <param name="sections">The switch's sections.</param>
    /// <returns><see langword="true"/> when a <c>default</c> label is present.</returns>
    private static bool HasDefaultLabel(SyntaxList<SwitchSectionSyntax> sections)
    {
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (labels[labelIndex].IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    return true;
                }
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

    /// <summary>Analyzes a <c>switch</c> expression and reports it when every arm produces the same value.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <remarks>
    /// The structural match runs before the exhaustiveness question, so the semantic model is only reached for
    /// a switch expression whose arms are already known to be identical — which is to say, almost never.
    /// </remarks>
    private static void AnalyzeSwitchExpression(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, IdenticalBranchesOptions> optionsByTree)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;
        var arms = switchExpression.Arms;
        if (arms.Count < MinimumBranchCount
            || !ArmValuesMatch(arms)
            || !IsExhaustive(context, switchExpression)
            || !IsBodyLargeEnough(context, optionsByTree, ExpressionBodySize))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.IdenticalBranches,
            switchExpression.SwitchKeyword.GetLocation(),
            Describe(arms.Count, " arms of this 'switch' expression")));
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
    /// <remarks>
    /// The compiler's own answer is used rather than a search for a discard arm, so a complete <c>bool</c> or
    /// enum switch counts as exhaustive even though it never writes <c>_</c>.
    /// </remarks>
    private static bool IsExhaustive(SyntaxNodeAnalysisContext context, SwitchExpressionSyntax switchExpression)
        => context.SemanticModel.GetOperation(switchExpression, context.CancellationToken) is ISwitchExpressionOperation { IsExhaustive: true };

    /// <summary>Returns whether a duplicated body is big enough to be worth reporting.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="statements">The size of one branch's body, in statements.</param>
    /// <returns><see langword="true"/> when the body meets the configured minimum.</returns>
    /// <remarks>
    /// Every branch has already been proven identical by the time this runs, so one branch's size is every
    /// branch's size. Two empty bodies never reach the default minimum of one — an empty block is SST1439's
    /// business, not this rule's.
    /// </remarks>
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
    /// <remarks>
    /// A body is read as its statement list, so a braced branch and a bare one are compared on what they
    /// actually do: <c>if (c) Run(); else { Run(); }</c> is the same duplicate as the braced pair, and the
    /// braces are not what the rule is about.
    /// </remarks>
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
