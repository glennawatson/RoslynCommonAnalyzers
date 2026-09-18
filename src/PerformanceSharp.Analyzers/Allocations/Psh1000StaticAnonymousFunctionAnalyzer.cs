// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests adding the <c>static</c> modifier to anonymous functions that capture no
/// state (PSH1000). A lambda or anonymous method qualifies only when flow analysis
/// proves it captures nothing and makes no runtime references to enclosing non-static local functions.
/// Functions converted to <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> are skipped because
/// static anonymous functions are illegal in expression trees, and files parsed as
/// C# 8 or earlier are skipped because the modifier does not exist there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1000StaticAnonymousFunctionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the expression-tree delegate wrapper type.</summary>
    private const string ExpressionOfTMetadataName = "System.Linq.Expressions.Expression`1";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AllocationRules.MakeAnonymousFunctionStatic);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, ExpressionOfTMetadataName),
            AnalyzeAnonymousFunction,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression);
    }

    /// <summary>Returns whether an anonymous function passes the syntax-only candidate checks.</summary>
    /// <param name="function">The anonymous function to inspect.</param>
    /// <returns><see langword="true"/> when the function is not already static and the language version allows the modifier.</returns>
    internal static bool IsSyntaxCandidate(AnonymousFunctionExpressionSyntax function) =>
        !ModifierListHelper.Contains(function.Modifiers, SyntaxKind.StaticKeyword)
            && ((CSharpParseOptions)function.SyntaxTree.Options).LanguageVersion >= LanguageVersion.CSharp9;

    /// <summary>Returns the small leading span the diagnostic is reported on.</summary>
    /// <param name="function">The anonymous function to report.</param>
    /// <returns>The parameter (list) span for lambdas, or the <c>delegate</c> keyword span for anonymous methods.</returns>
    internal static TextSpan GetReportSpan(AnonymousFunctionExpressionSyntax function) => function switch
    {
        SimpleLambdaExpressionSyntax simple => simple.Parameter.Span,
        ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Span,
        AnonymousMethodExpressionSyntax anonymousMethod => anonymousMethod.DelegateKeyword.Span,
        _ => function.Span
    };

    /// <summary>Reports PSH1000 for an anonymous function that provably captures nothing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="expressionTreeType">The compilation's deferred expression-tree type.</param>
    private static void AnalyzeAnonymousFunction(in SyntaxNodeAnalysisContext context, LazyMetadataType expressionTreeType)
    {
        var function = (AnonymousFunctionExpressionSyntax)context.Node;
        if (!IsSyntaxCandidate(function)
            || HasEnclosingLocalFunctionReference(context.SemanticModel, function, context.CancellationToken))
        {
            return;
        }

        var expressionOfTType = expressionTreeType.Get();
        if (IsExpressionTreeConversion(context.SemanticModel, function, expressionOfTType, context.CancellationToken)
            || !HasNoCaptures(context.SemanticModel, function, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.MakeAnonymousFunctionStatic,
            function.SyntaxTree,
            GetReportSpan(function)));
    }

    /// <summary>Rejects references to enclosing non-static local functions before allocating data-flow analysis state.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The candidate anonymous function.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>Whether a local function in an enclosing block prevents the static modifier.</returns>
    private static bool HasEnclosingLocalFunctionReference(SemanticModel model, AnonymousFunctionExpressionSyntax function, CancellationToken cancellationToken)
    {
        for (var ancestor = function.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax)
            {
                return false;
            }

            if (ancestor is not BlockSyntax block)
            {
                continue;
            }

            foreach (var statement in block.Statements)
            {
                if (statement is not LocalFunctionStatementSyntax localFunction || localFunction.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    continue;
                }

                var state = (Model: model, Declaration: localFunction, CancellationToken: cancellationToken);
                if (!DescendantTraversalHelper.VisitDescendants<SimpleNameSyntax, (SemanticModel Model, LocalFunctionStatementSyntax Declaration, CancellationToken CancellationToken)>(
                    function,
                    ref state,
                    VisitEnclosingLocalFunctionReference))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Binds only names matching an enclosing non-static local-function declaration.</summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="state">The enclosing declaration and its binding context.</param>
    /// <returns>Whether the search should continue.</returns>
    private static bool VisitEnclosingLocalFunctionReference(
        SimpleNameSyntax name,
        ref (SemanticModel Model, LocalFunctionStatementSyntax Declaration, CancellationToken CancellationToken) state)
    {
        if (!string.Equals(name.Identifier.ValueText, state.Declaration.Identifier.ValueText, StringComparison.Ordinal)
            || IsCompileTimeName(name, state.Model, state.CancellationToken)
            || state.Model.GetSymbolInfo(name, state.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction, IsStatic: false } referenced)
        {
            return true;
        }

        return !SymbolEqualityComparer.Default.Equals(referenced.OriginalDefinition, state.Model.GetDeclaredSymbol(state.Declaration, state.CancellationToken));
    }

    /// <summary>Returns whether the anonymous function converts to an expression tree, where <c>static</c> is illegal.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The anonymous function to inspect.</param>
    /// <param name="expressionOfTType">The compilation's <c>Expression&lt;TDelegate&gt;</c> type, when it exists.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the function's converted type is constructed from <c>Expression&lt;TDelegate&gt;</c>.</returns>
    private static bool IsExpressionTreeConversion(
        SemanticModel model,
        AnonymousFunctionExpressionSyntax function,
        INamedTypeSymbol? expressionOfTType,
        CancellationToken cancellationToken) =>
        expressionOfTType is not null
            && model.GetTypeInfo(function, cancellationToken).ConvertedType is INamedTypeSymbol convertedType
            && SymbolEqualityComparer.Default.Equals(convertedType.ConstructedFrom, expressionOfTType);

    /// <summary>Checks captured variables and references to enclosing non-static local functions.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The anonymous function to inspect.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>Whether adding <c>static</c> preserves access to enclosing state and local functions.</returns>
    /// <remarks>
    /// <see cref="DataFlowAnalysis.CapturedInside"/> excludes captures in sibling lambdas.
    /// <see cref="DataFlowAnalysis.UsedLocalFunctions"/> includes sibling uses, so each enclosing
    /// non-static function must also have a runtime reference inside the candidate's syntax.
    /// </remarks>
    private static bool HasNoCaptures(SemanticModel model, AnonymousFunctionExpressionSyntax function, CancellationToken cancellationToken)
    {
        var dataFlow = model.AnalyzeDataFlow(function);
        if (dataFlow is not { Succeeded: true, CapturedInside.IsEmpty: true })
        {
            return false;
        }

        ISymbol? functionSymbol = null;
        foreach (var localFunction in dataFlow.UsedLocalFunctions)
        {
            if (!localFunction.IsStatic
                && !IsDeclaredWithinFunction(localFunction, model, function, ref functionSymbol, cancellationToken)
                && HasLocalFunctionReference(model, function, localFunction, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds runtime references to a local function within the candidate's syntax.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The candidate anonymous function.</param>
    /// <param name="localFunction">The enclosing local function.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>Whether the local function is invoked or used as a method group in the candidate.</returns>
    private static bool HasLocalFunctionReference(
        SemanticModel model,
        AnonymousFunctionExpressionSyntax function,
        IMethodSymbol localFunction,
        CancellationToken cancellationToken)
    {
        var state = (Model: model, LocalFunction: localFunction, CancellationToken: cancellationToken);
        return !DescendantTraversalHelper.VisitDescendants<SimpleNameSyntax, (SemanticModel Model, IMethodSymbol LocalFunction, CancellationToken CancellationToken)>(
            function,
            ref state,
            VisitLocalFunctionReference);
    }

    /// <summary>Stops at a matching local-function reference, excluding compile-time names.</summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="state">The reference being sought and its binding context.</param>
    /// <returns>Whether the search should continue.</returns>
    private static bool VisitLocalFunctionReference(
        SimpleNameSyntax name,
        ref (SemanticModel Model, IMethodSymbol LocalFunction, CancellationToken CancellationToken) state)
    {
        if (!string.Equals(name.Identifier.ValueText, state.LocalFunction.Name, StringComparison.Ordinal))
        {
            return true;
        }

        if (IsCompileTimeName(name, state.Model, state.CancellationToken))
        {
            return true;
        }

        return state.Model.GetSymbolInfo(name, state.CancellationToken).Symbol is not IMethodSymbol referenced
            || !SymbolEqualityComparer.Default.Equals(referenced.OriginalDefinition, state.LocalFunction.OriginalDefinition);
    }

    /// <summary>Recognizes a compile-time nameof operand without confusing a user-defined nameof method.</summary>
    /// <param name="name">The candidate operand.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>Whether the reference is evaluated only to obtain its name.</returns>
    private static bool IsCompileTimeName(SimpleNameSyntax name, SemanticModel model, CancellationToken cancellationToken) =>
        name.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } invocation }
            && model.GetConstantValue(invocation, cancellationToken).HasValue;

    /// <summary>Checks whether a local function belongs to the candidate lambda or one of its nested functions.</summary>
    /// <param name="localFunction">The referenced local function.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="function">The candidate anonymous function.</param>
    /// <param name="functionSymbol">The anonymous-function symbol, resolved only when containment requires it.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>Whether the local function remains within the candidate's scope after adding <c>static</c>.</returns>
    private static bool IsDeclaredWithinFunction(
        IMethodSymbol localFunction,
        SemanticModel model,
        AnonymousFunctionExpressionSyntax function,
        ref ISymbol? functionSymbol,
        CancellationToken cancellationToken)
    {
        for (var containing = localFunction.ContainingSymbol; containing is IMethodSymbol method; containing = method.ContainingSymbol)
        {
            if (method.MethodKind != MethodKind.AnonymousFunction)
            {
                continue;
            }

            functionSymbol ??= model.GetSymbolInfo(function, cancellationToken).Symbol;
            if (SymbolEqualityComparer.Default.Equals(method, functionSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
