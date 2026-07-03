// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports finalizers with an empty body (PSH1002). Declaring any finalizer — even an
/// empty one — registers every instance of the type for finalization, so the object
/// survives an extra GC generation and adds finalizer-queue work for no benefit.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1002EmptyFinalizerAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.RemoveEmptyFinalizer);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeFinalizer, SyntaxKind.DestructorDeclaration);
    }

    /// <summary>Reports PSH1002 for a finalizer with an empty body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeFinalizer(SyntaxNodeAnalysisContext context)
    {
        var finalizer = (DestructorDeclarationSyntax)context.Node;
        if (finalizer.Body is not { Statements.Count: 0 })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(AllocationRules.RemoveEmptyFinalizer, finalizer.Identifier.GetLocation()));
    }
}
