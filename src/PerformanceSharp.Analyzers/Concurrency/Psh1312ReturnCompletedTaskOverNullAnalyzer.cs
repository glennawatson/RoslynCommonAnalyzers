// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a <c>null</c> (or <c>default</c> literal / <c>default(T)</c>) returned where the
/// declared return type is the reference type <c>Task</c> or <c>Task&lt;T&gt;</c> (PSH1312),
/// from a non-async method, local function, or property/indexer getter. Async members are
/// skipped — there <c>return null</c> produces a completed task carrying a null result — and
/// <c>ValueTask</c> is never reported because its <c>default</c> is already a completed task.
/// The returned expression's shape is checked before any binding, and the suggested
/// replacement text is computed only after a violation is confirmed. Gated on
/// <c>Task.CompletedTask</c> existing in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1312ReturnCompletedTaskOverNullAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the suggested replacement expression text for the code fix.</summary>
    internal const string ReplacementKey = "Replacement";

    /// <summary>The replacement expression text for the non-generic task return type.</summary>
    internal const string CompletedTaskText = "Task.CompletedTask";

    /// <summary>The metadata name of the non-generic task type.</summary>
    private const string TaskMetadataName = "System.Threading.Tasks.Task";

    /// <summary>The metadata name of the generic task type.</summary>
    private const string TaskOfTMetadataName = "System.Threading.Tasks.Task`1";

    /// <summary>The name of the cached completed-task property the non-generic fix moves to.</summary>
    private const string CompletedTaskPropertyName = "CompletedTask";

    /// <summary>The replacement text preceding the result type for generic task return types.</summary>
    private const string FromResultTextPrefix = "Task.FromResult<";

    /// <summary>The replacement text following the result type for generic task return types.</summary>
    private const string FromResultTextSuffix = ">(default)";

    /// <summary>The replacement text opening an explicit default expression for targets below C# 7.1.</summary>
    private const string FromResultExplicitDefaultOpen = ">(default(";

    /// <summary>The replacement text closing an explicit default expression for targets below C# 7.1.</summary>
    private const string FromResultExplicitDefaultClose = "))";

    /// <summary>Cached diagnostic properties suggesting the non-generic completed task.</summary>
    private static readonly ImmutableDictionary<string, string?> CompletedTaskProperties =
        ImmutableDictionary<string, string?>.Empty.Add(ReplacementKey, CompletedTaskText);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.ReturnCompletedTaskOverNull);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(TaskMetadataName) is not { } taskType
                || taskType.GetMembers(CompletedTaskPropertyName).IsEmpty
                || start.Compilation.GetTypeByMetadataName(TaskOfTMetadataName) is not { } taskOfTType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeReturnStatement(nodeContext, taskType, taskOfTType), SyntaxKind.ReturnStatement);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeArrowClause(nodeContext, taskType, taskOfTType), SyntaxKind.ArrowExpressionClause);
        });
    }

    /// <summary>Returns whether an expression is a null literal, default literal, or <c>default(T)</c>, before any binding.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression is one of the null/default shapes.</returns>
    internal static bool IsNullOrDefaultShape(ExpressionSyntax expression)
        => expression.IsKind(SyntaxKind.NullLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultLiteralExpression)
            || expression.IsKind(SyntaxKind.DefaultExpression);

    /// <summary>Reports PSH1312 for a return statement handing back null/default where a task is declared.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="taskOfTType">The generic task type definition.</param>
    private static void AnalyzeReturnStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType, INamedTypeSymbol taskOfTType)
    {
        var returnStatement = (ReturnStatementSyntax)context.Node;
        if (returnStatement.Expression is not { } expression
            || !IsNullOrDefaultShape(expression)
            || FindEnclosingReturnType(returnStatement) is not { } returnTypeSyntax)
        {
            return;
        }

        AnalyzeReturnedExpression(context, expression, returnTypeSyntax, taskType, taskOfTType);
    }

    /// <summary>Reports PSH1312 for an expression-bodied member whose body is null/default where a task is declared.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="taskOfTType">The generic task type definition.</param>
    private static void AnalyzeArrowClause(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType, INamedTypeSymbol taskOfTType)
    {
        var arrow = (ArrowExpressionClauseSyntax)context.Node;
        if (!IsNullOrDefaultShape(arrow.Expression) || GetArrowOwnerReturnType(arrow) is not { } returnTypeSyntax)
        {
            return;
        }

        AnalyzeReturnedExpression(context, arrow.Expression, returnTypeSyntax, taskType, taskOfTType);
    }

    /// <summary>Finds the declared return type of the non-async function enclosing a return statement.</summary>
    /// <param name="returnStatement">The return statement being analyzed.</param>
    /// <returns>The return type syntax, or <see langword="null"/> when the enclosing function is async, a lambda, or out of scope.</returns>
    private static TypeSyntax? FindEnclosingReturnType(ReturnStatementSyntax returnStatement)
    {
        for (SyntaxNode? node = returnStatement.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case AnonymousFunctionExpressionSyntax:
                case GlobalStatementSyntax:
                case BaseTypeDeclarationSyntax:
                    return null;
                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword) ? null : localFunction.ReturnType;
                case MethodDeclarationSyntax method:
                    return method.Modifiers.Any(SyntaxKind.AsyncKeyword) ? null : method.ReturnType;
                case BaseMethodDeclarationSyntax:
                    return null;
                case AccessorDeclarationSyntax accessor:
                    return accessor.IsKind(SyntaxKind.GetAccessorDeclaration) ? GetAccessorOwnerType(accessor) : null;
            }
        }

        return null;
    }

    /// <summary>Gets the declared return type owning an expression body, skipping async owners and lambdas.</summary>
    /// <param name="arrow">The expression body being analyzed.</param>
    /// <returns>The return type syntax, or <see langword="null"/> when the owner is async or not a supported member kind.</returns>
    private static TypeSyntax? GetArrowOwnerReturnType(ArrowExpressionClauseSyntax arrow)
        => arrow.Parent switch
        {
            MethodDeclarationSyntax method when !method.Modifiers.Any(SyntaxKind.AsyncKeyword) => method.ReturnType,
            LocalFunctionStatementSyntax localFunction when !localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword) => localFunction.ReturnType,
            PropertyDeclarationSyntax property => property.Type,
            IndexerDeclarationSyntax indexer => indexer.Type,
            AccessorDeclarationSyntax accessor when accessor.IsKind(SyntaxKind.GetAccessorDeclaration) => GetAccessorOwnerType(accessor),
            _ => null,
        };

    /// <summary>Gets the declared type of the property or indexer owning a get accessor.</summary>
    /// <param name="accessor">The get accessor.</param>
    /// <returns>The owner's type syntax, or <see langword="null"/> for other accessor owners.</returns>
    private static TypeSyntax? GetAccessorOwnerType(AccessorDeclarationSyntax accessor)
        => accessor.Parent?.Parent switch
        {
            PropertyDeclarationSyntax property => property.Type,
            IndexerDeclarationSyntax indexer => indexer.Type,
            _ => null,
        };

    /// <summary>Binds the declared return type and reports when it is the reference task type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expression">The returned null/default expression.</param>
    /// <param name="returnTypeSyntax">The enclosing member's declared return type.</param>
    /// <param name="taskType">The non-generic task type.</param>
    /// <param name="taskOfTType">The generic task type definition.</param>
    private static void AnalyzeReturnedExpression(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        TypeSyntax returnTypeSyntax,
        INamedTypeSymbol taskType,
        INamedTypeSymbol taskOfTType)
    {
        if (context.SemanticModel.GetTypeInfo(returnTypeSyntax, context.CancellationToken).Type is not INamedTypeSymbol returnType)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(returnType, taskType))
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                ConcurrencyRules.ReturnCompletedTaskOverNull,
                expression.SyntaxTree,
                expression.Span,
                CompletedTaskProperties,
                CompletedTaskText));
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, taskOfTType))
        {
            return;
        }

        var typeText = returnType.TypeArguments[0].ToMinimalDisplayString(context.SemanticModel, expression.SpanStart);
        var replacementText = expression.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7_1 }
            ? FromResultTextPrefix + typeText + FromResultTextSuffix
            : FromResultTextPrefix + typeText + FromResultExplicitDefaultOpen + typeText + FromResultExplicitDefaultClose;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.ReturnCompletedTaskOverNull,
            expression.SyntaxTree,
            expression.Span,
            ImmutableDictionary<string, string?>.Empty.Add(ReplacementKey, replacementText),
            replacementText));
    }
}
