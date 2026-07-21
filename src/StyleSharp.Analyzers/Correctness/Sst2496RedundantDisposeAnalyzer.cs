// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an explicit <c>Dispose()</c> or <c>Close()</c> on a local that a <c>using</c> statement or
/// <c>using</c> declaration already disposes (SST2496), so the value is disposed twice. The using owns
/// the disposal when its scope ends; the explicit call is a redundant second one.
/// </summary>
/// <remarks>
/// The clean path is syntactic: only a parameterless <c>Dispose</c>/<c>Close</c> member invocation on a
/// bare identifier is considered, and everything else is dropped before binding. A candidate binds the
/// receiver once and reports only when it is a local whose declaration carries the <c>using</c> keyword.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2496RedundantDisposeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.RedundantDispose);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns whether a local's declaration is governed by a <c>using</c>.</summary>
    /// <param name="local">The local the disposal targets.</param>
    /// <returns><see langword="true"/> when the local is declared by a using statement or using declaration.</returns>
    internal static bool IsUsingLocal(ILocalSymbol local)
    {
        var declarations = local.DeclaringSyntaxReferences;
        if (declarations.Length != 1)
        {
            return false;
        }

        return declarations[0].GetSyntax() is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: { } owner } }
            && owner switch
            {
                UsingStatementSyntax => true,
                LocalDeclarationStatementSyntax local2 => !local2.UsingKeyword.IsKind(SyntaxKind.None),
                _ => false,
            };
    }

    /// <summary>Reports one explicit disposal of a using-governed local.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Dispose" or "Close" } access
            || access.Expression is not IdentifierNameSyntax receiver)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not ILocalSymbol local
            || !IsUsingLocal(local))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.RedundantDispose,
            invocation.SyntaxTree,
            invocation.Span,
            local.Name,
            access.Name.Identifier.Text));
    }
}
