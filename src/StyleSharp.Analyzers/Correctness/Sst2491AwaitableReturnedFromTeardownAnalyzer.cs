// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a non-async method or local function that returns a pending task directly from inside a
/// scope that tears down at return (SST2491): a <c>using</c> statement or declaration, a <c>lock</c>,
/// or the body of a <c>try</c> that has a <c>finally</c>. The resource is disposed, the lock released,
/// or the finally run the instant the method returns — before the returned task completes.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is syntactic and allocation-free: only a <c>return</c> carrying an expression is
/// examined, an already-completed task shape (<c>Task.CompletedTask</c>, <c>Task.FromResult(...)</c>,
/// <c>new ValueTask(...)</c>, a null or default) is rejected on syntax, and a single upward walk finds
/// both the enclosing function and the teardown scope. Only a candidate that clears all of that is
/// bound once, to confirm the function is a non-async method returning a task type.
/// </para>
/// <para>
/// The task types are resolved once per compilation, so a project without <c>System.Threading.Tasks</c>
/// pays nothing. A completed task has nothing pending, so those shapes are not the bug and are skipped.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2491AwaitableReturnedFromTeardownAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.AwaitableReturnedFromTeardown);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var taskTypes = TeardownTaskTypes.Resolve(start.Compilation);
            if (!taskTypes.Any)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeReturn(nodeContext, taskTypes), SyntaxKind.ReturnStatement);
        });
    }

    /// <summary>Reports one return that hands a pending task out of a teardown scope in a non-async task method.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="taskTypes">The resolved task types for this compilation.</param>
    private static void AnalyzeReturn(SyntaxNodeAnalysisContext context, in TeardownTaskTypes taskTypes)
    {
        var returnStatement = (ReturnStatementSyntax)context.Node;
        if (returnStatement.Expression is not { } expression
            || IsCompletedTaskShape(expression)
            || !TryFindTeardown(returnStatement, out var functionNode, out var teardownKeyword)
            || !IsNonAsyncTaskMethod(context, functionNode, taskTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.AwaitableReturnedFromTeardown,
            returnStatement.SyntaxTree,
            returnStatement.Span,
            teardownKeyword));
    }

    /// <summary>Returns whether a function is a non-async method or local function returning a task type.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="functionNode">The enclosing function-like node.</param>
    /// <param name="taskTypes">The resolved task types for this compilation.</param>
    /// <returns><see langword="true"/> when awaiting the returned task would fix the teardown race.</returns>
    private static bool IsNonAsyncTaskMethod(SyntaxNodeAnalysisContext context, SyntaxNode functionNode, in TeardownTaskTypes taskTypes)
    {
        var modifiers = functionNode switch
        {
            MethodDeclarationSyntax method => method.Modifiers,
            LocalFunctionStatementSyntax localFunction => localFunction.Modifiers,
            _ => default,
        };

        return functionNode is MethodDeclarationSyntax or LocalFunctionStatementSyntax
            && !modifiers.Any(SyntaxKind.AsyncKeyword)
            && context.SemanticModel.GetDeclaredSymbol(functionNode, context.CancellationToken) is IMethodSymbol method2
            && !method2.IsAsync
            && taskTypes.IsTaskType(method2.ReturnType);
    }

    /// <summary>Walks up from a return to its function, recording the innermost teardown scope on the way.</summary>
    /// <param name="returnStatement">The return statement.</param>
    /// <param name="functionNode">The enclosing function-like node.</param>
    /// <param name="teardownKeyword">The innermost teardown keyword, when one governs the return.</param>
    /// <returns><see langword="true"/> when a teardown scope governs the return inside the same function.</returns>
    private static bool TryFindTeardown(ReturnStatementSyntax returnStatement, out SyntaxNode functionNode, out string teardownKeyword)
    {
        functionNode = returnStatement;
        teardownKeyword = string.Empty;
        SyntaxNode child = returnStatement;
        for (var node = returnStatement.Parent; node is not null; node = node.Parent)
        {
            if (IsFunctionBoundary(node))
            {
                functionNode = node;
                return teardownKeyword.Length != 0;
            }

            if (teardownKeyword.Length == 0 && TeardownKeywordFor(node, child) is { } keyword)
            {
                teardownKeyword = keyword;
            }

            child = node;
        }

        return false;
    }

    /// <summary>Returns whether a node begins a new function body, bounding the search upward.</summary>
    /// <param name="node">The node to test.</param>
    /// <returns><see langword="true"/> when the node owns the return's function scope.</returns>
    private static bool IsFunctionBoundary(SyntaxNode node)
        => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax;

    /// <summary>Returns the teardown keyword a scope contributes for the child that holds the return.</summary>
    /// <param name="node">The candidate teardown scope.</param>
    /// <param name="child">The scope's child that (transitively) contains the return.</param>
    /// <returns>The teardown keyword, or <see langword="null"/> when the scope tears nothing down at return.</returns>
    private static string? TeardownKeywordFor(SyntaxNode node, SyntaxNode child) => node switch
    {
        LockStatementSyntax lockStatement when lockStatement.Statement == child => "lock",
        UsingStatementSyntax usingStatement when usingStatement.Statement == child => "using",
        TryStatementSyntax tryStatement when tryStatement.Block == child && tryStatement.Finally is not null => "try/finally",
        BlockSyntax block when HasGoverningUsingDeclaration(block, child) => "using",
        _ => null,
    };

    /// <summary>Returns whether a block declares a using local before the statement holding the return.</summary>
    /// <param name="block">The block to scan.</param>
    /// <param name="child">The block's child that (transitively) contains the return.</param>
    /// <returns><see langword="true"/> when a using declaration earlier in the block governs the return.</returns>
    private static bool HasGoverningUsingDeclaration(BlockSyntax block, SyntaxNode child)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            if (statement == child)
            {
                return false;
            }

            if (statement is LocalDeclarationStatementSyntax local && !local.UsingKeyword.IsKind(SyntaxKind.None))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an expression is a shape whose task is already complete when returned.</summary>
    /// <param name="expression">The returned expression.</param>
    /// <returns><see langword="true"/> when nothing is left pending after the method returns.</returns>
    /// <remarks>
    /// A completed task has no continuation to outrace the teardown, so <c>Task.CompletedTask</c>,
    /// <c>Task.FromResult</c>/<c>FromException</c>/<c>FromCanceled</c>, a <c>new ValueTask(...)</c> wrapping a
    /// value, and a null or default return are all left alone on syntax without binding.
    /// </remarks>
    private static bool IsCompletedTaskShape(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression) || literal.IsKind(SyntaxKind.DefaultLiteralExpression):
            case DefaultExpressionSyntax:
            case ObjectCreationExpressionSyntax:
                return true;

            case MemberAccessExpressionSyntax { Name.Identifier.Text: "CompletedTask" }:
                return true;

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }:
                return access.Name.Identifier.Text is "FromResult" or "FromException" or "FromCanceled";

            default:
                return false;
        }
    }

    /// <summary>The task types a non-async method may return, resolved once per compilation.</summary>
    /// <param name="Task">The non-generic <c>Task</c>.</param>
    /// <param name="TaskOfT">The generic <c>Task&lt;T&gt;</c>, when present.</param>
    /// <param name="ValueTask">The non-generic <c>ValueTask</c>, when present.</param>
    /// <param name="ValueTaskOfT">The generic <c>ValueTask&lt;T&gt;</c>, when present.</param>
    internal readonly record struct TeardownTaskTypes(
        INamedTypeSymbol? Task,
        INamedTypeSymbol? TaskOfT,
        INamedTypeSymbol? ValueTask,
        INamedTypeSymbol? ValueTaskOfT)
    {
        /// <summary>Gets a value indicating whether any task type was resolved.</summary>
        public bool Any => Task is not null || ValueTask is not null;

        /// <summary>Resolves the task types from a compilation.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved task types.</returns>
        public static TeardownTaskTypes Resolve(Compilation compilation) => new(
            compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));

        /// <summary>Returns whether a return type is one of the resolved task types.</summary>
        /// <param name="returnType">The declared return type.</param>
        /// <returns><see langword="true"/> when awaiting the returned value would remove the teardown race.</returns>
        public bool IsTaskType(ITypeSymbol returnType)
        {
            if (returnType is not INamedTypeSymbol named)
            {
                return false;
            }

            var definition = named.OriginalDefinition;
            return SymbolEqualityComparer.Default.Equals(definition, Task)
                || SymbolEqualityComparer.Default.Equals(definition, TaskOfT)
                || SymbolEqualityComparer.Default.Equals(definition, ValueTask)
                || SymbolEqualityComparer.Default.Equals(definition, ValueTaskOfT);
        }
    }
}
