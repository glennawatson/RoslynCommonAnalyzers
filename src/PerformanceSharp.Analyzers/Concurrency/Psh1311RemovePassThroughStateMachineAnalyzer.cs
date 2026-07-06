// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags async methods and local functions that exist only to forward another task (PSH1311):
/// the entire body is one tail-position await — an expression body <c>=> await X</c>, a lone
/// <c>return await X;</c>, or a lone <c>await X;</c> in a <c>Task</c>-returning body — and the
/// awaited task's type is identical to the declared <c>Task</c>/<c>Task&lt;T&gt;</c> return
/// type, so returning the task directly removes the state machine. A trailing
/// <c>.ConfigureAwait(bool)</c> is looked through; with nothing after the await there is no
/// continuation left for it to configure. Lambdas and async void are never reported; the
/// diagnostic sits on the <c>async</c> keyword.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1311RemovePassThroughStateMachineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the ConfigureAwait unwrap requires.</summary>
    private const string ConfigureAwaitMethodName = "ConfigureAwait";

    /// <summary>The metadata name of the non-generic task type.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <summary>The metadata name of the generic task type.</summary>
    private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.RemovePassThroughStateMachine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var taskType = start.Compilation.GetTypeByMetadataName(TaskMetadataName);
            if (taskType is null)
            {
                return;
            }

            var taskOfTType = start.Compilation.GetTypeByMetadataName(TaskOfTMetadataName);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeDeclaration(nodeContext, taskType, taskOfTType),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.LocalFunctionStatement);
        });
    }

    /// <summary>Returns whether a declaration has the async single-tail-await syntax shape, before any binding.</summary>
    /// <param name="node">The method declaration or local function statement to inspect.</param>
    /// <param name="asyncKeyword">The declaration's <c>async</c> modifier when the shape matches.</param>
    /// <param name="awaitExpression">The lone tail-position await when the shape matches.</param>
    /// <param name="isStatementAwait">Whether the await is a lone expression statement rather than a returned or arrow-bodied value.</param>
    /// <returns><see langword="true"/> when the declaration is async and its whole body is one tail-position await.</returns>
    internal static bool TryGetShape(SyntaxNode node, out SyntaxToken asyncKeyword, [NotNullWhen(true)] out AwaitExpressionSyntax? awaitExpression, out bool isStatementAwait)
    {
        asyncKeyword = default;
        awaitExpression = null;
        isStatementAwait = false;

        SyntaxTokenList modifiers;
        BlockSyntax? body;
        ArrowExpressionClauseSyntax? expressionBody;
        switch (node)
        {
            case MethodDeclarationSyntax method:
            {
                modifiers = method.Modifiers;
                body = method.Body;
                expressionBody = method.ExpressionBody;
                break;
            }

            case LocalFunctionStatementSyntax localFunction:
            {
                modifiers = localFunction.Modifiers;
                body = localFunction.Body;
                expressionBody = localFunction.ExpressionBody;
                break;
            }

            default:
                return false;
        }

        var asyncIndex = modifiers.IndexOf(SyntaxKind.AsyncKeyword);
        if (asyncIndex < 0)
        {
            return false;
        }

        asyncKeyword = modifiers[asyncIndex];

        if (expressionBody is not null)
        {
            awaitExpression = expressionBody.Expression as AwaitExpressionSyntax;
            return awaitExpression is not null;
        }

        if (body is null || body.Statements.Count != 1)
        {
            return false;
        }

        switch (body.Statements[0])
        {
            case ReturnStatementSyntax { Expression: AwaitExpressionSyntax returned }:
            {
                awaitExpression = returned;
                return true;
            }

            case ExpressionStatementSyntax { Expression: AwaitExpressionSyntax awaited }:
            {
                awaitExpression = awaited;
                isStatementAwait = true;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Returns the forwarded task expression, looking through one trailing <c>.ConfigureAwait(bool literal)</c> call.</summary>
    /// <param name="awaited">The awaited expression.</param>
    /// <returns>The ConfigureAwait receiver when the awaited expression is that shape; otherwise the awaited expression itself.</returns>
    internal static ExpressionSyntax UnwrapConfigureAwait(ExpressionSyntax awaited)
    {
        if (awaited is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } invocation
            || invocation.Expression is not MemberAccessExpressionSyntax access
            || access.Name.Identifier.ValueText != ConfigureAwaitMethodName)
        {
            return awaited;
        }

        var argument = invocation.ArgumentList.Arguments[0].Expression;
        return argument.IsKind(SyntaxKind.TrueLiteralExpression) || argument.IsKind(SyntaxKind.FalseLiteralExpression)
            ? access.Expression
            : awaited;
    }

    /// <summary>Reports PSH1311 for an async declaration whose whole body forwards one task of the declared return type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="taskOfTType">The generic task type, when it exists.</param>
    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType, INamedTypeSymbol? taskOfTType)
    {
        var node = context.Node;
        if (!TryGetShape(node, out var asyncKeyword, out var awaitExpression, out var isStatementAwait))
        {
            return;
        }

        // A predefined return type (void, int, ...) can never be Task, so async void exits before any binding.
        var returnTypeSyntax = node is MethodDeclarationSyntax method ? method.ReturnType : ((LocalFunctionStatementSyntax)node).ReturnType;
        if (returnTypeSyntax is PredefinedTypeSyntax)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not IMethodSymbol declared)
        {
            return;
        }

        var returnType = declared.ReturnType;
        var isPlainTask = SymbolEqualityComparer.Default.Equals(returnType, taskType);
        if (!isPlainTask && !IsGenericTask(returnType, taskOfTType))
        {
            return;
        }

        // A lone `await X;` statement can only forward when the declaration returns the non-generic Task.
        if (isStatementAwait && !isPlainTask)
        {
            return;
        }

        var forwarded = UnwrapConfigureAwait(awaitExpression.Expression);
        var forwardedType = context.SemanticModel.GetTypeInfo(forwarded, context.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(forwardedType, returnType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.RemovePassThroughStateMachine,
            node.SyntaxTree,
            asyncKeyword.Span));
    }

    /// <summary>Returns whether a return type is a constructed <c>Task&lt;T&gt;</c>.</summary>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="taskOfTType">The generic task type, when it exists.</param>
    /// <returns><see langword="true"/> when the return type is a constructed generic task.</returns>
    private static bool IsGenericTask(ITypeSymbol returnType, INamedTypeSymbol? taskOfTType)
        => taskOfTType is not null
            && returnType is INamedTypeSymbol named
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfTType);
}
