// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a lambda, anonymous method, or local function (SST2479) that captures a variable a
/// <c>for</c>/<c>while</c>/<c>do</c> loop steps through and is then stored where it outlives the loop
/// iteration, so every deferred call reads the variable's final value rather than its per-iteration value.
/// </summary>
/// <remarks>
/// <para>
/// A <c>for</c>/<c>while</c>/<c>do</c> loop uses a single shared storage slot for the variable it advances,
/// so a closure over that variable reads whatever value it holds when the closure runs. That is harmless when
/// the closure runs in place, but a defect when the closure escapes the iteration: subscribed to an event with
/// <c>+=</c>, added to a collection (<c>Add</c>/<c>Insert</c>/<c>Enqueue</c>/<c>Push</c> and friends), assigned
/// to a field, property, or array element, produced by <c>yield return</c>, or handed to a deferred runner
/// (<c>Task.Run</c>, <c>Task.Factory.StartNew</c>, <c>ThreadPool.QueueUserWorkItem</c>).
/// </para>
/// <para>
/// Escape is decided purely from local syntax; no dataflow or interprocedural analysis runs. The captured
/// symbol is bound to confirm it is a loop-stepped local (a <c>for</c> header variable, or a local declared
/// outside the loop and assigned or incremented inside it). A <c>foreach</c> iteration variable is a fresh copy
/// per iteration since C# 5 and is never reported; a variable a per-iteration local already copies is likewise
/// safe. A plain <c>return</c> ends the loop, so it cannot leave several delegates sharing one final value and
/// is not treated as an escape; only <c>yield return</c>, which resumes the loop, is.
/// </para>
/// <para>
/// The clean path is syntactic: every candidate escape site is rejected without binding unless it stores a
/// delegate expression, and only then are the delegate's captures bound and its enclosing loops walked.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2479CapturedLoopVariableAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.CapturedLoopVariable);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeEventSubscription, SyntaxKind.AddAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeStorageAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeYieldReturn, SyntaxKind.YieldReturnStatement);
    }

    /// <summary>Analyzes an invocation whose argument stores a capturing delegate.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsEscapeSink(invocation) || invocation.ArgumentList is not { } argumentList)
        {
            return;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            HandleStoredArgument(Unwrap(arguments[i].Expression), context);
        }
    }

    /// <summary>Analyzes an <c>x += delegate</c> event or delegate subscription.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeEventSubscription(SyntaxNodeAnalysisContext context)
        => HandleStoredLambda(Unwrap(((AssignmentExpressionSyntax)context.Node).Right), context);

    /// <summary>Analyzes a delegate assigned to a field, property, or array element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStorageAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Left is not (MemberAccessExpressionSyntax or ElementAccessExpressionSyntax))
        {
            return;
        }

        HandleStoredLambda(Unwrap(assignment.Right), context);
    }

    /// <summary>Analyzes a delegate produced by <c>yield return</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeYieldReturn(SyntaxNodeAnalysisContext context)
    {
        if (((YieldStatementSyntax)context.Node).Expression is not { } expression)
        {
            return;
        }

        HandleStoredLambda(Unwrap(expression), context);
    }

    /// <summary>Handles a stored argument that may be a lambda or a local-function method group.</summary>
    /// <param name="expression">The unwrapped argument expression.</param>
    /// <param name="context">The syntax node analysis context.</param>
    private static void HandleStoredArgument(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        if (expression is AnonymousFunctionExpressionSyntax anonymousFunction)
        {
            ReportIfCapturing(anonymousFunction, anonymousFunction.GetLocation(), context);
            return;
        }

        if (expression is not IdentifierNameSyntax identifier
            || context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction
            || localFunction.DeclaringSyntaxReferences.Length != 1
            || localFunction.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is not LocalFunctionStatementSyntax declaration)
        {
            return;
        }

        ReportIfCapturing(declaration, identifier.GetLocation(), context);
    }

    /// <summary>Handles a stored expression, acting only when it is a lambda or anonymous method.</summary>
    /// <param name="expression">The unwrapped stored expression.</param>
    /// <param name="context">The syntax node analysis context.</param>
    private static void HandleStoredLambda(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        if (expression is not AnonymousFunctionExpressionSyntax anonymousFunction)
        {
            return;
        }

        ReportIfCapturing(anonymousFunction, anonymousFunction.GetLocation(), context);
    }

    /// <summary>Reports when the delegate captures a loop-stepped variable from an enclosing loop.</summary>
    /// <param name="captureRoot">The delegate whose captures to inspect.</param>
    /// <param name="reportLocation">The location to report at.</param>
    /// <param name="context">The syntax node analysis context.</param>
    private static void ReportIfCapturing(SyntaxNode captureRoot, Location reportLocation, SyntaxNodeAnalysisContext context)
    {
        if (!HasLoopAncestor(captureRoot))
        {
            return;
        }

        var scan = new CaptureScan(context.SemanticModel, captureRoot, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, CaptureScan>(captureRoot, ref scan, VisitCapturedIdentifier);
        if (scan.Result is not { } name)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.CapturedLoopVariable, reportLocation, name));
    }

    /// <summary>Records the first captured identifier that resolves to a loop-stepped local.</summary>
    /// <param name="identifier">The candidate captured identifier.</param>
    /// <param name="state">The capture scan state.</param>
    /// <returns><see langword="true"/> to keep searching, or <see langword="false"/> once one is found.</returns>
    private static bool VisitCapturedIdentifier(IdentifierNameSyntax identifier, ref CaptureScan state)
    {
        if (state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences.Length == 0)
        {
            return true;
        }

        var declarationSpan = local.DeclaringSyntaxReferences[0].Span;
        if (state.CaptureRoot.Span.Contains(declarationSpan)
            || !IsLoopStepped(local, state.CaptureRoot, state.Model, state.CancellationToken))
        {
            return true;
        }

        state.Result = local.Name;
        return false;
    }

    /// <summary>Returns whether some enclosing loop steps the captured local across iterations.</summary>
    /// <param name="local">The captured local.</param>
    /// <param name="captureRoot">The delegate that captures the local.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local is a <c>for</c> header variable or is mutated inside an enclosing loop.</returns>
    private static bool IsLoopStepped(ILocalSymbol local, SyntaxNode captureRoot, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var current = captureRoot.Parent; current is not null; current = current.Parent)
        {
            if (current is MemberDeclarationSyntax)
            {
                return false;
            }

            if (StepsLocalAcrossIterations(current, local, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether one enclosing statement is a loop that steps the local across iterations.</summary>
    /// <param name="loop">The candidate enclosing loop statement.</param>
    /// <param name="local">The captured local.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local is declared outside the loop body and mutated by the loop.</returns>
    private static bool StepsLocalAcrossIterations(SyntaxNode loop, ILocalSymbol local, SemanticModel model, CancellationToken cancellationToken)
    {
        var declarationSpan = local.DeclaringSyntaxReferences[0].Span;
        return loop switch
        {
            ForStatementSyntax forStatement => !forStatement.Statement.Span.Contains(declarationSpan)
                && (IsMutatedInIncrementors(forStatement.Incrementors, local, model, cancellationToken)
                    || IsMutatedIn(forStatement.Statement, local, model, cancellationToken)),
            WhileStatementSyntax whileStatement => !whileStatement.Statement.Span.Contains(declarationSpan)
                && IsMutatedIn(whileStatement.Statement, local, model, cancellationToken),
            DoStatementSyntax doStatement => !doStatement.Statement.Span.Contains(declarationSpan)
                && IsMutatedIn(doStatement.Statement, local, model, cancellationToken),
            _ => false,
        };
    }

    /// <summary>Returns whether the invocation is a shape that stores a delegate beyond the current iteration.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> for a collection-store or deferred-runner call.</returns>
    private static bool IsEscapeSink(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax memberAccess =>
            IsCollectionStoreName(memberAccess.Name.Identifier.ValueText)
            || IsDeferredRunner(memberAccess.Name.Identifier.ValueText, memberAccess.Expression),
        SimpleNameSyntax simpleName => IsCollectionStoreName(simpleName.Identifier.ValueText),
        _ => false,
    };

    /// <summary>Returns whether the method name stores its argument in a collection that outlives the iteration.</summary>
    /// <param name="name">The invoked member's simple name.</param>
    /// <returns><see langword="true"/> for a known collection-store method name.</returns>
    private static bool IsCollectionStoreName(string name) => name switch
    {
        "Add" or "AddRange" or "AddFirst" or "AddLast" or "Insert" or "Enqueue" or "Push" or "TryAdd" => true,
        _ => false,
    };

    /// <summary>Returns whether the call defers the delegate to run after the current iteration.</summary>
    /// <param name="name">The invoked member's simple name.</param>
    /// <param name="receiver">The receiver expression the method is called on.</param>
    /// <returns><see langword="true"/> for a recognized deferred-runner call.</returns>
    private static bool IsDeferredRunner(string name, ExpressionSyntax receiver) => name switch
    {
        "Run" => LastName(receiver) == "Task",
        "StartNew" => LastName(receiver) == "Factory",
        "QueueUserWorkItem" => LastName(receiver) == "ThreadPool",
        _ => false,
    };

    /// <summary>Returns the trailing simple-name text of a receiver expression, or <see langword="null"/>.</summary>
    /// <param name="expression">The receiver expression.</param>
    /// <returns>The trailing name, or <see langword="null"/> for an unsupported shape.</returns>
    private static string? LastName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether the local is assigned or incremented by any of the loop's incrementors.</summary>
    /// <param name="incrementors">The <c>for</c> loop incrementor expressions.</param>
    /// <param name="local">The local to look for.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when an incrementor mutates the local.</returns>
    private static bool IsMutatedInIncrementors(SeparatedSyntaxList<ExpressionSyntax> incrementors, ILocalSymbol local, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var i = 0; i < incrementors.Count; i++)
        {
            var incrementor = incrementors[i];
            if (IsMutationOf(incrementor, local, model, cancellationToken) || IsMutatedIn(incrementor, local, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the local is mutated somewhere in a scope, skipping nested delegates.</summary>
    /// <param name="scope">The scope to scan.</param>
    /// <param name="local">The local to look for.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a straight-line mutation of the local is found.</returns>
    private static bool IsMutatedIn(SyntaxNode scope, ILocalSymbol local, SemanticModel model, CancellationToken cancellationToken)
    {
        var children = scope.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].AsNode() is not { } child || child is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                continue;
            }

            if (IsMutationOf(child, local, model, cancellationToken) || IsMutatedIn(child, local, model, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a single node assigns, increments, or passes the local by reference.</summary>
    /// <param name="node">The node to test.</param>
    /// <param name="local">The local to look for.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the node mutates the local.</returns>
    private static bool IsMutationOf(SyntaxNode node, ILocalSymbol local, SemanticModel model, CancellationToken cancellationToken) => node switch
    {
        AssignmentExpressionSyntax assignment => BindsTo(assignment.Left, local, model, cancellationToken),
        PrefixUnaryExpressionSyntax prefix when IsStep(prefix.Kind()) => BindsTo(prefix.Operand, local, model, cancellationToken),
        PostfixUnaryExpressionSyntax postfix when IsStep(postfix.Kind()) => BindsTo(postfix.Operand, local, model, cancellationToken),
        ArgumentSyntax argument when IsByReference(argument) => BindsTo(argument.Expression, local, model, cancellationToken),
        _ => false,
    };

    /// <summary>Returns whether a syntax kind is an increment or decrement operator.</summary>
    /// <param name="kind">The kind to test.</param>
    /// <returns><see langword="true"/> for pre/post increment or decrement.</returns>
    private static bool IsStep(SyntaxKind kind)
        => kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression;

    /// <summary>Returns whether an argument is passed by <c>ref</c> or <c>out</c>.</summary>
    /// <param name="argument">The argument to test.</param>
    /// <returns><see langword="true"/> when the argument is a writable reference.</returns>
    private static bool IsByReference(ArgumentSyntax argument)
        => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword);

    /// <summary>Returns whether an expression is an identifier that binds to the given local.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <param name="local">The local to compare against.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression names the local.</returns>
    private static bool BindsTo(ExpressionSyntax expression, ILocalSymbol local, SemanticModel model, CancellationToken cancellationToken)
        => Unwrap(expression) is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == local.Name
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, local);

    /// <summary>Returns whether the node has a <c>for</c>/<c>while</c>/<c>do</c> ancestor within its member.</summary>
    /// <param name="node">The node to walk up from.</param>
    /// <returns><see langword="true"/> when an enclosing loop exists.</returns>
    private static bool HasLoopAncestor(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }

            if (current is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Peels parentheses and casts off an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized, non-cast expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            current = current is ParenthesizedExpressionSyntax parenthesized
                ? parenthesized.Expression
                : ((CastExpressionSyntax)current).Expression;
        }

        return current;
    }

    /// <summary>The state threaded through the search for a delegate's loop-stepped captures.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="CaptureRoot">The delegate whose free captures are being searched.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct CaptureScan(SemanticModel Model, SyntaxNode CaptureRoot, CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets the name of the first loop-stepped captured local found.</summary>
        public string? Result { get; set; }
    }
}
