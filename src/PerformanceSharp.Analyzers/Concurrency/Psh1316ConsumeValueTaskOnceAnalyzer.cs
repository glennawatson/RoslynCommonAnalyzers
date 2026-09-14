// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports the two ways a <c>ValueTask</c> instance is consumed more than once (PSH1316): a local
/// declared outside a loop and awaited inside it, so every iteration after the first reuses a token the
/// pool has already recycled; and a local copied into a second local where both are consumed. The first
/// consume — an <c>await</c>, <c>.Result</c>, <c>.GetAwaiter()</c>, or <c>.AsTask()</c> — invalidates the
/// pooled <c>IValueTaskSource</c> token, so a second consume silently reads another caller's result.
/// </summary>
/// <remarks>
/// The rule is deliberately narrow: it reports only what a dataflow pass cannot see — the loop and the
/// alias. A local assigned inside the loop (a fresh <c>ValueTask</c> each iteration), a <c>.Preserve()</c>d
/// instance, and a single consume are all correct and never reported. The clean path is a loop-body walk
/// for a consume on an identifier; the semantic model is only touched when one is found, and only for a
/// local of a <c>ValueTask</c> type.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1316ConsumeValueTaskOnceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ConcurrencyRules.ConsumeValueTaskOnce);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var valueTaskTypes = new LazyCompilationValue<ValueTaskTypes?>(start.Compilation, ValueTaskTypes.Create, runOnce: true);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLoop(nodeContext, valueTaskTypes),
                SyntaxKind.ForStatement,
                SyntaxKind.ForEachStatement,
                SyntaxKind.WhileStatement,
                SyntaxKind.DoStatement);

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeCopy(nodeContext, valueTaskTypes), SyntaxKind.LocalDeclarationStatement);
        });
    }

    /// <summary>Reports a ValueTask local awaited inside a loop it was declared outside of.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="valueTaskTypes">The ValueTask types resolved on first demand.</param>
    private static void AnalyzeLoop(in SyntaxNodeAnalysisContext context, LazyCompilationValue<ValueTaskTypes?> valueTaskTypes)
    {
        if (GetLoopBody(context.Node) is not { } body)
        {
            return;
        }

        var scan = new LoopScan(context, valueTaskTypes, context.Node);
        _ = DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, LoopScan>(body, ref scan, VisitLoopConsume);
    }

    /// <summary>Classifies one identifier inside a loop body, reporting a stale ValueTask consume.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The loop scan state.</param>
    /// <returns>Always <see langword="true"/>, so every consume in the loop is checked.</returns>
    private static bool VisitLoopConsume(IdentifierNameSyntax identifier, ref LoopScan state)
    {
        if (!IsConsume(identifier)
            || NearestEnclosingLoop(identifier) != state.Loop
            || IsDeclaredOrAssignedInside(identifier, state.Loop)
            || state.ValueTaskTypes.Get() is not { } valueTaskTypes
            || state.Context.SemanticModel.GetSymbolInfo(identifier, state.Context.CancellationToken).Symbol is not ILocalSymbol local
            || !valueTaskTypes.IsValueTask(local.Type)
            || IsDeclaredInside(local, state.Loop)
            || IsPreserved(local, identifier.Identifier.ValueText, state.Context.CancellationToken))
        {
            return true;
        }

        state.Context.ReportDiagnostic(DiagnosticHelper.Create(ConcurrencyRules.ConsumeValueTaskOnce, identifier.GetLocation(), local.Name));
        return true;
    }

    /// <summary>Reports a ValueTask local copied into a second local where both are consumed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="valueTaskTypes">The ValueTask types resolved on first demand.</param>
    private static void AnalyzeCopy(in SyntaxNodeAnalysisContext context, LazyCompilationValue<ValueTaskTypes?> valueTaskTypes)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (!declaration.UsingKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        var variables = declaration.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            AnalyzeCopyVariable(context, valueTaskTypes, variables[i]);
        }
    }

    /// <summary>Reports one <c>var copy = source;</c> alias of a consumed ValueTask.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="valueTaskTypes">The ValueTask types resolved on first demand.</param>
    /// <param name="variable">The copy declarator.</param>
    private static void AnalyzeCopyVariable(in SyntaxNodeAnalysisContext context, LazyCompilationValue<ValueTaskTypes?> valueTaskTypes, VariableDeclaratorSyntax variable)
    {
        if (variable.Initializer?.Value is not IdentifierNameSyntax source
            || !IsConsumedCopy(variable, source)
            || valueTaskTypes.Get() is not { } resolvedValueTaskTypes
            || context.SemanticModel.GetSymbolInfo(source, context.CancellationToken).Symbol is not ILocalSymbol sourceLocal
            || !resolvedValueTaskTypes.IsValueTask(sourceLocal.Type)
            || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol copyLocal)
        {
            return;
        }

        if (IsPreserved(sourceLocal, sourceLocal.Name, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ConcurrencyRules.ConsumeValueTaskOnce, variable.Identifier.GetLocation(), copyLocal.Name));
    }

    /// <summary>Checks whether both names in a local copy are consumed in the enclosing body.</summary>
    /// <param name="variable">The copy declarator.</param>
    /// <param name="source">The copied identifier.</param>
    /// <returns>Whether both names have a syntactic consume in the body.</returns>
    private static bool IsConsumedCopy(VariableDeclaratorSyntax variable, IdentifierNameSyntax source) =>
        EnclosingFunction.GetBody(variable) is { } body
            && IsConsumedIn(body, source.Identifier.ValueText)
            && IsConsumedIn(body, variable.Identifier.ValueText);

    /// <summary>Returns whether an identifier is consumed as a ValueTask.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns><see langword="true"/> for an await operand or a token-spending member access.</returns>
    private static bool IsConsume(IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is AwaitExpressionSyntax awaitExpression && awaitExpression.Expression == identifier)
        {
            return true;
        }

        return identifier.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Result" or "GetAwaiter" or "AsTask" or "ConfigureAwait" } access
            && access.Expression == identifier;
    }

    /// <summary>Returns whether a name is consumed as a ValueTask anywhere in a body.</summary>
    /// <param name="body">The body to scan.</param>
    /// <param name="name">The local name.</param>
    /// <returns><see langword="true"/> when a consume of the name is found.</returns>
    private static bool IsConsumedIn(SyntaxNode body, string name) =>
        !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, string>(body, ref name, VisitNameConsume);

    /// <summary>Continues the walk past an identifier that is not a consume of the name.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="name">The local name.</param>
    /// <returns><see langword="false"/> at a consume, which stops the walk.</returns>
    private static bool VisitNameConsume(IdentifierNameSyntax identifier, ref string name) =>
        identifier.Identifier.ValueText != name || !IsConsume(identifier);

    /// <summary>Returns whether a ValueTask local is preserved for reuse anywhere in its body.</summary>
    /// <param name="local">The local.</param>
    /// <param name="name">The local name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a <c>local.Preserve()</c> keeps the token valid.</returns>
    private static bool IsPreserved(ILocalSymbol local, string name, CancellationToken cancellationToken) =>
        local.DeclaringSyntaxReferences is [var reference]
            && EnclosingFunction.GetBody(reference.GetSyntax(cancellationToken)) is { } body
            && !DescendantTraversalHelper.VisitDescendants<MemberAccessExpressionSyntax, string>(body, ref name, VisitPreserve);

    /// <summary>Continues the walk past a member access that is not <c>name.Preserve</c>.</summary>
    /// <param name="access">The member access being visited.</param>
    /// <param name="name">The local name.</param>
    /// <returns><see langword="false"/> at a preserve, which stops the walk.</returns>
    private static bool VisitPreserve(MemberAccessExpressionSyntax access, ref string name) =>
        access.Name.Identifier.ValueText != "Preserve"
            || access.Expression is not IdentifierNameSyntax identifier
            || identifier.Identifier.ValueText != name;

    /// <summary>Returns whether a local is declared inside a loop.</summary>
    /// <param name="local">The local.</param>
    /// <param name="loop">The loop node.</param>
    /// <returns><see langword="true"/> when the declaration is within the loop.</returns>
    private static bool IsDeclaredInside(ILocalSymbol local, SyntaxNode loop) =>
        local.DeclaringSyntaxReferences is [var reference] && loop.Span.Contains(reference.Span);

    /// <summary>Returns whether an identifier is declared or reassigned within the loop, so the ValueTask is fresh.</summary>
    /// <param name="identifier">The consumed identifier.</param>
    /// <param name="loop">The loop node.</param>
    /// <returns><see langword="true"/> when the same name is written inside the loop.</returns>
    private static bool IsDeclaredOrAssignedInside(IdentifierNameSyntax identifier, SyntaxNode loop)
    {
        if (GetLoopBody(loop) is not { } body)
        {
            return false;
        }

        var name = identifier.Identifier.ValueText;
        return !DescendantTraversalHelper.VisitDescendants<SyntaxNode, string>(body, ref name, VisitNameWrite);
    }

    /// <summary>Continues the walk past a node that neither assigns nor declares the name.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="name">The local name.</param>
    /// <returns><see langword="false"/> at a write, which stops the walk.</returns>
    private static bool VisitNameWrite(SyntaxNode node, ref string name) => node switch
    {
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } => left.Identifier.ValueText != name,
        VariableDeclaratorSyntax declarator => declarator.Identifier.ValueText != name,
        _ => true,
    };

    /// <summary>Gets a loop's body statement.</summary>
    /// <param name="loop">The loop node.</param>
    /// <returns>The body, or <see langword="null"/>.</returns>
    private static StatementSyntax? GetLoopBody(SyntaxNode loop) => loop switch
    {
        ForStatementSyntax statement => statement.Statement,
        ForEachStatementSyntax statement => statement.Statement,
        WhileStatementSyntax statement => statement.Statement,
        DoStatementSyntax statement => statement.Statement,
        _ => null,
    };

    /// <summary>Gets the nearest enclosing loop of a node.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The nearest loop, or <see langword="null"/>.</returns>
    private static SyntaxNode? NearestEnclosingLoop(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return current;
            }

            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>The ValueTask types of one compilation, resolved once.</summary>
    /// <param name="ValueTask">The non-generic <c>ValueTask</c> type.</param>
    /// <param name="ValueTaskOfT">The generic <c>ValueTask&lt;T&gt;</c> type.</param>
    private readonly record struct ValueTaskTypes(INamedTypeSymbol? ValueTask, INamedTypeSymbol? ValueTaskOfT)
    {
        /// <summary>Resolves the ValueTask types for a compilation, or nothing when neither exists.</summary>
        /// <param name="compilation">The compilation.</param>
        /// <returns>The resolved types, or <see langword="null"/>.</returns>
        public static ValueTaskTypes? Create(Compilation compilation)
        {
            var valueTask = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
            var valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
            return valueTask is null && valueTaskOfT is null ? null : new ValueTaskTypes(valueTask, valueTaskOfT);
        }

        /// <summary>Returns whether a type is a <c>ValueTask</c> or <c>ValueTask&lt;T&gt;</c>.</summary>
        /// <param name="type">The type to test.</param>
        /// <returns><see langword="true"/> for either ValueTask type.</returns>
        public bool IsValueTask(ITypeSymbol type) =>
            (ValueTask is not null && SymbolEqualityComparer.Default.Equals(type, ValueTask))
                || (ValueTaskOfT is not null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, ValueTaskOfT));
    }

    /// <summary>The state threaded through a loop-body consume scan.</summary>
    /// <param name="Context">The syntax node analysis context.</param>
    /// <param name="ValueTaskTypes">The ValueTask types resolved on first demand.</param>
    /// <param name="Loop">The loop being analyzed.</param>
    private readonly record struct LoopScan(SyntaxNodeAnalysisContext Context, LazyCompilationValue<ValueTaskTypes?> ValueTaskTypes, SyntaxNode Loop);
}
