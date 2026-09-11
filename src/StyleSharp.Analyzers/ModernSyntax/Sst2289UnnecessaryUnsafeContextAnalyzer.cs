// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>unsafe</c> block whose pointer operations are all permitted in safe code from
/// C# 15, so the block marks a region the compiler no longer treats as unsafe (SST2289).
/// </summary>
/// <remarks>
/// The block has to contain at least one relaxed pointer operation to be reported: a block with no
/// pointer work at all is an unsafe modifier guarding nothing, which is a different shape with its
/// own rule. Anything that still reads or writes through a pointer stops the walk, so the
/// suggestion is only made where removing the keyword leaves code that still compiles.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2289UnnecessaryUnsafeContextAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(ModernSyntaxRules.UnnecessaryUnsafeContext);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.UnsafeStatement);
    }

    /// <summary>Reports one unsafe block that no longer needs its context.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!LanguageVersions.SupportsCSharp15(context.Node))
        {
            return;
        }

        var statement = (UnsafeStatementSyntax)context.Node;
        var scan = new UnsafeScan(context.SemanticModel, false, false, context.CancellationToken);
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, UnsafeScan>(statement.Block, ref scan, Visit);

        if (scan.RequiresUnsafe || !scan.Relaxed)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UnnecessaryUnsafeContext,
            statement.UnsafeKeyword.GetLocation()));
    }

    /// <summary>Classifies one descendant, stopping the walk once something still needs the context.</summary>
    /// <param name="node">The descendant node.</param>
    /// <param name="scan">The running classification.</param>
    /// <returns><see langword="false"/> once an operation that still requires <c>unsafe</c> is found.</returns>
    private static bool Visit(SyntaxNode node, ref UnsafeScan scan)
    {
        if (StillRequiresUnsafe(node, scan.Model, scan.CancellationToken))
        {
            scan.RequiresUnsafe = true;
            return false;
        }

        scan.Relaxed |= IsRelaxedInCSharp15(node);
        return true;
    }

    /// <summary>Gets whether a node performs an operation that still needs an unsafe context.</summary>
    /// <param name="node">The descendant node.</param>
    /// <param name="model">The semantic model for the analyzed tree.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the operation reads or writes through a pointer.</returns>
    private static bool StillRequiresUnsafe(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        if (node.IsKind(SyntaxKind.PointerIndirectionExpression)
            || node.IsKind(SyntaxKind.PointerMemberAccessExpression)
            || node is FunctionPointerTypeSyntax)
        {
            return true;
        }

        return node is ElementAccessExpressionSyntax elementAccess
            && model.GetTypeInfo(elementAccess.Expression, cancellationToken).Type is IPointerTypeSymbol;
    }

    /// <summary>Gets whether a node is a pointer operation C# 15 permits outside an unsafe context.</summary>
    /// <param name="node">The descendant node.</param>
    /// <returns><see langword="true"/> for a relaxed operation.</returns>
    private static bool IsRelaxedInCSharp15(SyntaxNode node) =>
        node is PointerTypeSyntax
            or FixedStatementSyntax
            or SizeOfExpressionSyntax
            or StackAllocArrayCreationExpressionSyntax
            or ImplicitStackAllocArrayCreationExpressionSyntax
            || node.IsKind(SyntaxKind.AddressOfExpression);

    /// <summary>The state threaded through the descendant walk.</summary>
    /// <param name="Model">The semantic model used to type pointer element access.</param>
    /// <param name="Relaxed">Whether a pointer operation C# 15 relaxes was seen.</param>
    /// <param name="RequiresUnsafe">Whether an operation that still requires an unsafe context was seen.</param>
    /// <param name="CancellationToken">The token that cancels analysis.</param>
    private record struct UnsafeScan(
        SemanticModel Model,
        bool Relaxed,
        bool RequiresUnsafe,
        CancellationToken CancellationToken);
}
