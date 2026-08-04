// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped maintainability analyzer for exception-handling mistakes. One registration covers both the
/// catch-clause and throw-statement rules so they share the analyzer instance.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1429 — a <c>catch</c> of the base <see cref="Exception"/> (or a bare <c>catch</c>) has an empty body.</description></item>
/// <item><description>SST1430 — <c>throw ex;</c> re-throws the caught exception and discards its original stack trace.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExceptionHandlingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.NoEmptyCatchOfBaseException,
        MaintainabilityRules.PreserveStackTraceOnRethrow);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
        context.RegisterSyntaxNodeAction(AnalyzeThrowStatement, SyntaxKind.ThrowStatement);
    }

    /// <summary>Returns the <c>catch</c> clause that a bare <c>throw;</c> would re-throw from, or <see langword="null"/>.</summary>
    /// <param name="throwStatement">The throw statement.</param>
    /// <returns>The directly enclosing catch clause, or <see langword="null"/> when a lambda or member boundary intervenes.</returns>
    internal static CatchClauseSyntax? FindRethrowableCatch(ThrowStatementSyntax throwStatement)
    {
        for (var node = throwStatement.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case CatchClauseSyntax catchClause:
                    return catchClause;

                // A 'throw;' is only legal directly inside the catch, so any function boundary cuts it off.
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Reports SST1429 for a <c>catch</c> of the base exception type whose body handles nothing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;

        // A 'when' filter means the author picked which exceptions reach this clause deliberately.
        if (catchClause.Filter is not null || !SwallowsEveryError(catchClause, context))
        {
            return;
        }

        // A bare 'catch { }' catches everything; otherwise only the base 'System.Exception' is the concern.
        if (catchClause.Declaration is { Type: { } typeSyntax }
            && !IsSystemException(context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoEmptyCatchOfBaseException, catchClause.CatchKeyword.GetLocation()));
    }

    /// <summary>Returns whether a catch body discards the error rather than handling it.</summary>
    /// <param name="catchClause">The catch clause.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> for an empty body, or an opted-in constant-returning body.</returns>
    /// <remarks>
    /// An empty body discards the error outright. A body that only hands back a constant discards it just as
    /// completely — the caller cannot tell a failure from a legitimate result — but that shape is also how a
    /// Try-style member reports "no value", so it is only reported where the codebase has asked for it.
    /// </remarks>
    private static bool SwallowsEveryError(CatchClauseSyntax catchClause, SyntaxNodeAnalysisContext context)
    {
        var statements = catchClause.Block.Statements;
        if (statements.Count == 0)
        {
            return true;
        }

        if (statements.Count != 1 || statements[0] is not ReturnStatementSyntax returnStatement)
        {
            return false;
        }

        var options = ExceptionHandlingOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(catchClause.SyntaxTree));
        return options.CheckConstantReturningCatch && IsConstantResult(returnStatement.Expression, context);
    }

    /// <summary>Returns whether a returned expression carries no information about the error.</summary>
    /// <param name="expression">The returned expression, or <see langword="null"/> for a bare <c>return;</c>.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <returns><see langword="true"/> when the value is a constant or a default.</returns>
    private static bool IsConstantResult(ExpressionSyntax? expression, SyntaxNodeAnalysisContext context)
        => expression is null
        || expression is DefaultExpressionSyntax or LiteralExpressionSyntax
        || context.SemanticModel.GetConstantValue(expression, context.CancellationToken).HasValue;

    /// <summary>Reports SST1430 for a <c>throw ex;</c> that re-throws the caught exception variable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeThrowStatement(SyntaxNodeAnalysisContext context)
    {
        var throwStatement = (ThrowStatementSyntax)context.Node;
        if (throwStatement.Expression is not IdentifierNameSyntax thrown
            || FindRethrowableCatch(throwStatement) is not { Declaration: { } declaration }
            || !string.Equals(thrown.Identifier.ValueText, declaration.Identifier.ValueText, StringComparison.Ordinal))
        {
            return;
        }

        // Confirm the thrown name really is the caught variable before suggesting a bare 'throw;'.
        var thrownSymbol = context.SemanticModel.GetSymbolInfo(thrown, context.CancellationToken).Symbol;
        var caughtSymbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        if (caughtSymbol is null || !SymbolEqualityComparer.Default.Equals(thrownSymbol, caughtSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.PreserveStackTraceOnRethrow, throwStatement.GetLocation(), thrown.Identifier.ValueText));
    }

    /// <summary>Returns whether a type symbol is <see cref="Exception"/> itself.</summary>
    /// <param name="type">The caught type.</param>
    /// <returns><see langword="true"/> for the exact <c>System.Exception</c> type.</returns>
    private static bool IsSystemException(ITypeSymbol? type)
        => type is { Name: "Exception", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };
}
