// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>nameof</c> applied to a type parameter (SST2498). The result is the parameter's own
/// spelling — the literal <c>"T"</c> — in every instantiation, never the name of the substituted type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2498NameofTypeParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The contextual keyword that introduces the <c>nameof</c> operator.</summary>
    private const string NameofKeyword = "nameof";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.NameofTypeParameter);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a <c>nameof</c> whose operand is a type parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: NameofKeyword }
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0] is not { NameColon: null, Expression: IdentifierNameSyntax operand })
        {
            return;
        }

        // A method someone declared as 'nameof' is an ordinary call; only the operator folds to a constant.
        if (!context.SemanticModel.GetConstantValue(invocation, context.CancellationToken).HasValue
            || context.SemanticModel.GetSymbolInfo(operand, context.CancellationToken).Symbol is not ITypeParameterSymbol typeParameter)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CorrectnessRules.NameofTypeParameter, invocation.GetLocation(), typeParameter.Name));
    }
}
