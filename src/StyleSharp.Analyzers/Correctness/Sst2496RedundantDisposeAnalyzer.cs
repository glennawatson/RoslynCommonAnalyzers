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
/// receiver once and reports only when it is a using-owned local. A <c>Close</c> call must resolve to
/// a framework disposal alias or its override; a custom contour or collection operation is left alone.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2496RedundantDisposeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.RedundantDispose);

    /// <summary>Framework types whose Close method releases the resource.</summary>
    private static readonly string[] DisposalAliasTypes = ["System.IO.Stream", "System.IO.TextReader", "System.IO.TextWriter"];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataTypeSlots(compilation, DisposalAliasTypes),
            Analyze,
            SyntaxKind.InvocationExpression);
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
    /// <param name="types">The framework disposal aliases resolved on first demand.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, LazyMetadataTypeSlots types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Dispose" or "Close" } access
            || access.Expression is not IdentifierNameSyntax receiver)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not ILocalSymbol local
            || !IsUsingLocal(local)
            || (access.Name.Identifier.ValueText == "Close" && !IsDisposalAlias(context, invocation, types)))
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

    /// <summary>Returns whether Close invokes a framework resource disposal method or an override.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The Close call.</param>
    /// <param name="types">The framework types resolved on first demand.</param>
    /// <returns>True when Close releases a framework resource.</returns>
    private static bool IsDisposalAlias(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, LazyMetadataTypeSlots types)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: false, ReturnsVoid: true, Parameters.Length: 0 } method)
        {
            return false;
        }

        for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
        {
            if (types.IsAny(current.ContainingType, 0, DisposalAliasTypes.Length))
            {
                return true;
            }
        }

        return false;
    }
}
