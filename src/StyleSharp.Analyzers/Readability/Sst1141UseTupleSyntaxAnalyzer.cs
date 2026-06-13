// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an explicit <c>ValueTuple&lt;...&gt;</c> type where the language tuple syntax
/// <c>(T1, T2)</c> would do (SST1141). The common no-diagnostic path is a
/// single name comparison, so the semantic model is consulted only for a generic name actually
/// spelled <c>ValueTuple</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1141UseTupleSyntaxAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest type arguments a value tuple expressible as tuple syntax can have.</summary>
    private const int MinTupleArity = 2;

    /// <summary>The most type arguments handled before the eighth (TRest) element nests.</summary>
    private const int MaxTupleArity = 7;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseTupleSyntax);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GenericName);
    }

    /// <summary>Reports SST1141 when a generic name is an explicit value-tuple type expressible as tuple syntax.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (GenericNameSyntax)context.Node;
        if (node.Identifier.ValueText != "ValueTuple")
        {
            return;
        }

        var count = node.TypeArgumentList.Arguments.Count;
        if (count is < MinTupleArity or > MaxTupleArity)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol is not INamedTypeSymbol { IsTupleType: true })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseTupleSyntax, node.GetLocation()));
    }
}
