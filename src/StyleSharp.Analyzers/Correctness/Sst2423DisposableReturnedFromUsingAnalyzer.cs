// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a value owned by a <c>using</c> that is returned out of the <c>using</c> scope (SST2423):
/// <c>return local;</c>, an element of <c>return (local, …);</c>, or <c>yield return local;</c>, where
/// <c>local</c> is declared by a <c>using</c> declaration or a <c>using</c> statement (including
/// <c>await using</c>). The <c>using</c> disposes the value on the way out through the return, so the
/// caller receives an object that was disposed during the return, and the failure surfaces later as an
/// <c>ObjectDisposedException</c> with a misleading stack.
/// </summary>
/// <remarks>
/// The clean path is syntactic: the rule only binds a <c>return</c>/<c>yield return</c> whose expression
/// is a bare identifier, or a tuple of identifiers. Anything else — a member of the value, a different
/// object, a method call — never reaches the semantic model.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2423DisposableReturnedFromUsingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.DisposableReturnedFromUsing);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (DisposableTypes.Create(start.Compilation) is not { } types)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, types), SyntaxKind.ReturnStatement, SyntaxKind.YieldReturnStatement);
        });
    }

    /// <summary>Analyzes one return or yield-return statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, in DisposableTypes types)
    {
        var expression = context.Node switch
        {
            ReturnStatementSyntax returnStatement => returnStatement.Expression,
            YieldStatementSyntax yieldStatement => yieldStatement.Expression,
            _ => null,
        };

        switch (expression)
        {
            case IdentifierNameSyntax identifier:
            {
                CheckIdentifier(context, types, identifier);
                break;
            }

            case TupleExpressionSyntax tuple:
            {
                var arguments = tuple.Arguments;
                for (var i = 0; i < arguments.Count; i++)
                {
                    if (arguments[i].Expression is IdentifierNameSyntax element)
                    {
                        CheckIdentifier(context, types, element);
                    }
                }

                break;
            }
        }
    }

    /// <summary>Reports when an identifier resolves to a <c>using</c>-owned disposable being returned.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    /// <param name="identifier">The returned identifier.</param>
    private static void CheckIdentifier(SyntaxNodeAnalysisContext context, in DisposableTypes types, IdentifierNameSyntax identifier)
    {
        if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not ILocalSymbol { IsUsing: true } local
            || !types.ImplementsDisposable(local.Type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DisposableReturnedFromUsing,
            identifier.GetLocation(),
            local.Name));
    }
}
