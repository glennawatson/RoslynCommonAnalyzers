// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Reports function complexity that exceeds the configured limits.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionComplexityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default SST1442 maximum.</summary>
    private const int DefaultCyclomaticMaximum = 10;

    /// <summary>The default SST1443 method maximum.</summary>
    private const int DefaultCognitiveMaximum = 15;

    /// <summary>The default SST1443 property/accessor maximum.</summary>
    private const int DefaultPropertyCognitiveMaximum = 3;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.CyclomaticComplexity,
        MaintainabilityRules.CognitiveComplexity);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var provider = start.Options.AnalyzerConfigOptionsProvider;
            var thresholdByTree = new ConditionalWeakTable<SyntaxTree, ComplexityThresholds>();
            ConditionalWeakTable<SyntaxTree, ComplexityThresholds>.CreateValueCallback factory =
                tree => ComplexityThresholds.Read(provider.GetOptions(tree));

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeDeclaration(nodeContext, thresholdByTree, factory),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration,
                SyntaxKind.DestructorDeclaration,
                SyntaxKind.OperatorDeclaration,
                SyntaxKind.ConversionOperatorDeclaration,
                SyntaxKind.LocalFunctionStatement,
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration,
                SyntaxKind.InitAccessorDeclaration,
                SyntaxKind.AddAccessorDeclaration,
                SyntaxKind.RemoveAccessorDeclaration,
                SyntaxKind.PropertyDeclaration);
        });
    }

    /// <summary>Analyzes a function-like declaration for complexity.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="thresholdByTree">The per-tree threshold cache.</param>
    /// <param name="factory">The threshold cache-miss factory.</param>
    private static void AnalyzeDeclaration(
        SyntaxNodeAnalysisContext context,
        ConditionalWeakTable<SyntaxTree, ComplexityThresholds> thresholdByTree,
        ConditionalWeakTable<SyntaxTree, ComplexityThresholds>.CreateValueCallback factory)
    {
        if (!TryGetDeclarationInfo(context.Node, out var nodeToAnalyze, out var declarationType, out var isPropertyLike))
        {
            return;
        }

        ComplexityCounter.Count(nodeToAnalyze, context.Node, out var branching, out var nestedFlow);

        var location = context.Node.GetLocation();
        var thresholds = thresholdByTree.GetValue(context.Node.SyntaxTree, factory);
        ReportCyclomatic(context, location, declarationType, thresholds.CyclomaticMaximum, branching);
        var cognitiveMaximum = isPropertyLike ? thresholds.PropertyCognitiveMaximum : thresholds.CognitiveMaximum;
        ReportCognitive(context, location, declarationType, cognitiveMaximum, nestedFlow);
    }

    /// <summary>Reports a cyclomatic-complexity diagnostic when the value exceeds the configured maximum.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <param name="declarationType">The declaration type.</param>
    /// <param name="maximum">The configured maximum.</param>
    /// <param name="cyclomatic">The computed complexity.</param>
    private static void ReportCyclomatic(SyntaxNodeAnalysisContext context, Location location, string declarationType, int maximum, int cyclomatic)
    {
        if (cyclomatic <= maximum)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MaintainabilityRules.CyclomaticComplexity,
                location,
                maximum,
                cyclomatic,
                declarationType));
    }

    /// <summary>Reports a cognitive-complexity diagnostic when the value exceeds the configured maximum.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <param name="declarationType">The declaration type.</param>
    /// <param name="maximum">The configured maximum.</param>
    /// <param name="cognitive">The computed complexity.</param>
    private static void ReportCognitive(SyntaxNodeAnalysisContext context, Location location, string declarationType, int maximum, int cognitive)
    {
        if (cognitive <= maximum)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                MaintainabilityRules.CognitiveComplexity,
                location,
                declarationType,
                cognitive,
                maximum));
    }

    /// <summary>Returns the declaration body and reporting metadata.</summary>
    /// <param name="node">The declaration node.</param>
    /// <param name="nodeToAnalyze">The node to scan for complexity.</param>
    /// <param name="declarationType">The message declaration type.</param>
    /// <param name="isPropertyLike">Whether property/accessor thresholds apply for cognitive complexity.</param>
    /// <returns><see langword="true"/> when there is executable syntax to analyze.</returns>
    private static bool TryGetDeclarationInfo(
        SyntaxNode node,
        out SyntaxNode nodeToAnalyze,
        out string declarationType,
        out bool isPropertyLike)
    {
        nodeToAnalyze = node;
        declarationType = "method";
        isPropertyLike = false;

        switch (node)
        {
            case MethodDeclarationSyntax method when method.Body is not null || method.ExpressionBody is not null:
            {
                return true;
            }

            case ConstructorDeclarationSyntax constructor when constructor.Body is not null || constructor.ExpressionBody is not null:
            {
                declarationType = "constructor";
                return true;
            }

            case DestructorDeclarationSyntax destructor when destructor.Body is not null || destructor.ExpressionBody is not null:
            {
                declarationType = "destructor";
                return true;
            }

            case OperatorDeclarationSyntax operatorDeclaration when operatorDeclaration.Body is not null || operatorDeclaration.ExpressionBody is not null:
            {
                declarationType = "operator";
                return true;
            }

            case ConversionOperatorDeclarationSyntax conversion when conversion.Body is not null || conversion.ExpressionBody is not null:
            {
                declarationType = "operator";
                return true;
            }

            case LocalFunctionStatementSyntax localFunction when localFunction.Body is not null || localFunction.ExpressionBody is not null:
            {
                declarationType = "local function";
                return true;
            }

            case AccessorDeclarationSyntax accessor when accessor.Body is not null || accessor.ExpressionBody is not null:
            {
                declarationType = "accessor";
                isPropertyLike = true;
                return true;
            }

            case PropertyDeclarationSyntax { ExpressionBody: { } expressionBody }:
            {
                nodeToAnalyze = expressionBody;
                declarationType = "property";
                isPropertyLike = true;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Per-tree complexity thresholds.</summary>
    private sealed class ComplexityThresholds
    {
        /// <summary>Initializes a new instance of the <see cref="ComplexityThresholds"/> class.</summary>
        /// <param name="cyclomaticMaximum">The SST1442 maximum.</param>
        /// <param name="cognitiveMaximum">The SST1443 function maximum.</param>
        /// <param name="propertyCognitiveMaximum">The SST1443 property/accessor maximum.</param>
        public ComplexityThresholds(int cyclomaticMaximum, int cognitiveMaximum, int propertyCognitiveMaximum)
        {
            CyclomaticMaximum = cyclomaticMaximum;
            CognitiveMaximum = cognitiveMaximum;
            PropertyCognitiveMaximum = propertyCognitiveMaximum;
        }

        /// <summary>Gets the configured SST1442 maximum.</summary>
        public int CyclomaticMaximum { get; }

        /// <summary>Gets the configured SST1443 function maximum.</summary>
        public int CognitiveMaximum { get; }

        /// <summary>Gets the configured SST1443 property/accessor maximum.</summary>
        public int PropertyCognitiveMaximum { get; }

        /// <summary>Reads complexity thresholds from analyzer options.</summary>
        /// <param name="options">The analyzer config options.</param>
        /// <returns>The resolved thresholds.</returns>
        public static ComplexityThresholds Read(AnalyzerConfigOptions options)
            => new(
                ReadPositiveInt(options, "stylesharp.SST1442.max_cyclomatic_complexity", "stylesharp.max_cyclomatic_complexity", DefaultCyclomaticMaximum),
                ReadPositiveInt(options, "stylesharp.SST1443.max_cognitive_complexity", "stylesharp.max_cognitive_complexity", DefaultCognitiveMaximum),
                ReadPositiveInt(options, "stylesharp.SST1443.max_property_cognitive_complexity", "stylesharp.max_property_cognitive_complexity", DefaultPropertyCognitiveMaximum));

        /// <summary>Reads a positive integer setting.</summary>
        /// <param name="options">The analyzer config options.</param>
        /// <param name="ruleKey">The rule-specific key.</param>
        /// <param name="generalKey">The general key.</param>
        /// <param name="fallback">The fallback value.</param>
        /// <returns>The configured positive integer, or <paramref name="fallback"/>.</returns>
        private static int ReadPositiveInt(AnalyzerConfigOptions options, string ruleKey, string generalKey, int fallback)
        {
            if (options.TryGetValue(ruleKey, out var value) && int.TryParse(value, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            return options.TryGetValue(generalKey, out value) && int.TryParse(value, out parsed) && parsed > 0
                ? parsed
                : fallback;
        }
    }

    /// <summary>Counts branching and nested-flow complexity in one syntax pass.</summary>
    private sealed class ComplexityCounter : CSharpSyntaxWalker
    {
        /// <summary>The declaration currently being scanned.</summary>
        private readonly SyntaxNode _root;

        /// <summary>The current nesting depth.</summary>
        private int _nesting;

        /// <summary>The current method name, if direct recursion can be detected syntactically.</summary>
        private string? _methodName;

        /// <summary>The current method parameter count.</summary>
        private int _methodParameterCount;

        /// <summary>Initializes a new instance of the <see cref="ComplexityCounter"/> class.</summary>
        /// <param name="root">The declaration root.</param>
        private ComplexityCounter(SyntaxNode root)
        {
            _root = root;
        }

        /// <summary>Gets the computed direct-branch count.</summary>
        public int BranchingComplexity { get; private set; } = 1;

        /// <summary>Gets the computed nested-flow count.</summary>
        public int NestedFlowComplexity { get; private set; }

        /// <summary>Counts complexity for a declaration.</summary>
        /// <param name="node">The node to scan.</param>
        /// <param name="declaration">The declaration that owns <paramref name="node"/>.</param>
        /// <param name="branching">The computed direct-branch count.</param>
        /// <param name="nestedFlow">The computed nested-flow count.</param>
        public static void Count(SyntaxNode node, SyntaxNode declaration, out int branching, out int nestedFlow)
        {
            var counter = new ComplexityCounter(node);
            if (declaration is MethodDeclarationSyntax method)
            {
                counter._methodName = method.Identifier.ValueText;
                counter._methodParameterCount = method.ParameterList.Parameters.Count;
            }

            counter.Visit(node);
            branching = counter.BranchingComplexity;
            nestedFlow = counter.NestedFlowComplexity;
        }

        /// <inheritdoc/>
        public override void Visit(SyntaxNode? node)
        {
            if (node is null || (node != _root && IsNestedFunctionBoundary(node)))
            {
                return;
            }

            base.Visit(node);
        }

        /// <inheritdoc/>
        public override void VisitIfStatement(IfStatementSyntax node)
        {
            AddBranch();
            if (node.Parent is ElseClauseSyntax)
            {
                base.VisitIfStatement(node);
                return;
            }

            AddNestingPlusOne();
            VisitWithNesting(node, static (counter, current) => counter.VisitIfStatementCore(current));
        }

        /// <inheritdoc/>
        public override void VisitElseClause(ElseClauseSyntax node)
        {
            AddNestedFlow();
            base.VisitElseClause(node);
        }

        /// <inheritdoc/>
        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            AddBranch();
            VisitNestedComplexity(node, static (counter, current) => counter.VisitConditionalExpressionCore(current));
        }

        /// <inheritdoc/>
        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            AddBranch();
            VisitNestedComplexity(node, static (counter, current) => counter.VisitSwitchStatementCore(current));
        }

        /// <inheritdoc/>
        public override void VisitSwitchExpression(SwitchExpressionSyntax node)
        {
            AddBranch();
            base.VisitSwitchExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
        {
            AddBranch();
            base.VisitConditionalAccessExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitForStatement(ForStatementSyntax node)
        {
            AddBranch();
            VisitNestedComplexity(node, static (counter, current) => counter.VisitForStatementCore(current));
        }

        /// <inheritdoc/>
        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            AddBranch();
            VisitNestedComplexity(node, static (counter, current) => counter.VisitForEachStatementCore(current));
        }

        /// <inheritdoc/>
        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            AddBranch();
            VisitNestedComplexity(node, static (counter, current) => counter.VisitWhileStatementCore(current));
        }

        /// <inheritdoc/>
        public override void VisitDoStatement(DoStatementSyntax node)
        {
            AddBranch();
            VisitNestedComplexity(node, static (counter, current) => counter.VisitDoStatementCore(current));
        }

        /// <inheritdoc/>
        public override void VisitCatchClause(CatchClauseSyntax node)
            => VisitNestedComplexity(node, static (counter, current) => counter.VisitCatchClauseCore(current));

        /// <inheritdoc/>
        public override void VisitGotoStatement(GotoStatementSyntax node)
            => VisitNestedComplexity(node, static (counter, current) => counter.VisitGotoStatementCore(current));

        /// <inheritdoc/>
        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            => VisitWithNesting(node, static (counter, current) => counter.VisitSimpleLambdaExpressionCore(current));

        /// <inheritdoc/>
        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            => VisitWithNesting(node, static (counter, current) => counter.VisitParenthesizedLambdaExpressionCore(current));

        /// <inheritdoc/>
        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            => VisitWithNesting(node, static (counter, current) => counter.VisitAnonymousMethodExpressionCore(current));

        /// <inheritdoc/>
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_methodName is not null
                && node.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == _methodName
                && node.ArgumentList.Arguments.Count == _methodParameterCount)
            {
                AddNestedFlow();
            }

            base.VisitInvocationExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var kind = node.Kind();
            if (IsBranchingBinary(kind))
            {
                AddBranch();
            }

            if (IsLogicalBinary(kind) && !IsSameLogicalBinary(node.Left, kind))
            {
                AddNestedFlow();
            }

            base.VisitBinaryExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.CoalesceAssignmentExpression))
            {
                AddBranch();
            }

            base.VisitAssignmentExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitBinaryPattern(BinaryPatternSyntax node)
        {
            if (!node.IsKind(SyntaxKind.AndPattern) && !node.IsKind(SyntaxKind.OrPattern))
            {
                base.VisitBinaryPattern(node);
                return;
            }

            AddBranch();
            if (!IsSameBinaryPattern(node.Left, node.Kind()))
            {
                AddNestedFlow();
            }

            base.VisitBinaryPattern(node);
        }

        /// <summary>Returns whether a node starts a nested function boundary.</summary>
        /// <param name="node">The candidate node.</param>
        /// <returns><see langword="true"/> for nested lambdas and local functions.</returns>
        private static bool IsNestedFunctionBoundary(SyntaxNode node)
            => node is LocalFunctionStatementSyntax
                or SimpleLambdaExpressionSyntax
                or ParenthesizedLambdaExpressionSyntax
                or AnonymousMethodExpressionSyntax;

        /// <summary>Returns whether an expression is the same logical binary kind after parentheses.</summary>
        /// <param name="expression">The expression to inspect.</param>
        /// <param name="kind">The expected kind.</param>
        /// <returns><see langword="true"/> when the expression is the same logical operation.</returns>
        private static bool IsSameLogicalBinary(ExpressionSyntax expression, SyntaxKind kind)
            => Unwrap(expression).IsKind(kind);

        /// <summary>Returns whether a pattern is the same binary-pattern kind after parentheses.</summary>
        /// <param name="pattern">The pattern to inspect.</param>
        /// <param name="kind">The expected kind.</param>
        /// <returns><see langword="true"/> when the pattern is the same binary operation.</returns>
        private static bool IsSameBinaryPattern(PatternSyntax pattern, SyntaxKind kind)
            => Unwrap(pattern).IsKind(kind);

        /// <summary>Returns whether the binary expression increments cognitive complexity.</summary>
        /// <param name="kind">The binary kind.</param>
        /// <returns><see langword="true"/> for logical operators.</returns>
        private static bool IsLogicalBinary(SyntaxKind kind)
            => kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;

        /// <summary>Returns whether the binary expression increments direct branching complexity.</summary>
        /// <param name="kind">The binary kind.</param>
        /// <returns><see langword="true"/> for supported branching operators.</returns>
        private static bool IsBranchingBinary(SyntaxKind kind)
            => kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression;

        /// <summary>Removes parentheses around an expression.</summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The unwrapped expression.</returns>
        private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Expression;
            }

            return expression;
        }

        /// <summary>Removes parentheses around a pattern.</summary>
        /// <param name="pattern">The pattern.</param>
        /// <returns>The unwrapped pattern.</returns>
        private static PatternSyntax Unwrap(PatternSyntax pattern)
        {
            while (pattern is ParenthesizedPatternSyntax parenthesized)
            {
                pattern = parenthesized.Pattern;
            }

            return pattern;
        }

        /// <summary>Visits a node after adding nested complexity.</summary>
        /// <typeparam name="TNode">The node type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="visit">The base visit callback.</param>
        private void VisitNestedComplexity<TNode>(TNode node, Action<ComplexityCounter, TNode> visit)
            where TNode : SyntaxNode
        {
            AddNestingPlusOne();
            VisitWithNesting(node, visit);
        }

        /// <summary>Visits a node with one extra nesting level.</summary>
        /// <typeparam name="TNode">The node type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="visit">The base visit callback.</param>
        private void VisitWithNesting<TNode>(TNode node, Action<ComplexityCounter, TNode> visit)
            where TNode : SyntaxNode
        {
            _nesting++;
            visit(this, node);
            _nesting--;
        }

        /// <summary>Visits an if statement without adding the decision point again.</summary>
        /// <param name="node">The if statement.</param>
        private void VisitIfStatementCore(IfStatementSyntax node) => base.VisitIfStatement(node);

        /// <summary>Visits a conditional expression without adding the decision point again.</summary>
        /// <param name="node">The conditional expression.</param>
        private void VisitConditionalExpressionCore(ConditionalExpressionSyntax node) => base.VisitConditionalExpression(node);

        /// <summary>Visits a switch statement without adding the decision point again.</summary>
        /// <param name="node">The switch statement.</param>
        private void VisitSwitchStatementCore(SwitchStatementSyntax node) => base.VisitSwitchStatement(node);

        /// <summary>Visits a for statement without adding the decision point again.</summary>
        /// <param name="node">The for statement.</param>
        private void VisitForStatementCore(ForStatementSyntax node) => base.VisitForStatement(node);

        /// <summary>Visits a foreach statement without adding the decision point again.</summary>
        /// <param name="node">The foreach statement.</param>
        private void VisitForEachStatementCore(ForEachStatementSyntax node) => base.VisitForEachStatement(node);

        /// <summary>Visits a while statement without adding the decision point again.</summary>
        /// <param name="node">The while statement.</param>
        private void VisitWhileStatementCore(WhileStatementSyntax node) => base.VisitWhileStatement(node);

        /// <summary>Visits a do statement without adding the decision point again.</summary>
        /// <param name="node">The do statement.</param>
        private void VisitDoStatementCore(DoStatementSyntax node) => base.VisitDoStatement(node);

        /// <summary>Visits a catch clause without adding the decision point again.</summary>
        /// <param name="node">The catch clause.</param>
        private void VisitCatchClauseCore(CatchClauseSyntax node) => base.VisitCatchClause(node);

        /// <summary>Visits a goto statement without adding the decision point again.</summary>
        /// <param name="node">The goto statement.</param>
        private void VisitGotoStatementCore(GotoStatementSyntax node) => base.VisitGotoStatement(node);

        /// <summary>Visits a simple lambda with the current nesting state.</summary>
        /// <param name="node">The lambda expression.</param>
        private void VisitSimpleLambdaExpressionCore(SimpleLambdaExpressionSyntax node) => base.VisitSimpleLambdaExpression(node);

        /// <summary>Visits a parenthesized lambda with the current nesting state.</summary>
        /// <param name="node">The lambda expression.</param>
        private void VisitParenthesizedLambdaExpressionCore(ParenthesizedLambdaExpressionSyntax node) => base.VisitParenthesizedLambdaExpression(node);

        /// <summary>Visits an anonymous method with the current nesting state.</summary>
        /// <param name="node">The anonymous method.</param>
        private void VisitAnonymousMethodExpressionCore(AnonymousMethodExpressionSyntax node) => base.VisitAnonymousMethodExpression(node);

        /// <summary>Adds one direct-branch point.</summary>
        private void AddBranch() => BranchingComplexity++;

        /// <summary>Adds one nested-flow point.</summary>
        private void AddNestedFlow() => NestedFlowComplexity++;

        /// <summary>Adds complexity for the current nesting depth.</summary>
        private void AddNestingPlusOne() => NestedFlowComplexity += _nesting + 1;
    }
}
