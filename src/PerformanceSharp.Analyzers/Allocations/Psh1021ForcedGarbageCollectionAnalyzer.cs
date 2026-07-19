// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags explicit <c>GC.Collect(...)</c> (any overload) and <c>GC.WaitForPendingFinalizers()</c>
/// calls (PSH1021). Forcing a collection blocks every managed thread for a full GC, and waiting on
/// the finalizer queue stalls the caller; both override the runtime's self-tuning heuristics and
/// almost always cost more throughput than they save. The check is syntax-gated on the
/// <c>GC.Collect</c> / <c>GC.WaitForPendingFinalizers</c> shape, then binds the invocation to confirm
/// the containing type is <c>System.GC</c> so a same-named user type is never reported. No code fix is
/// offered — removing the call is left to the author, since a rare diagnostic scenario may want it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1021ForcedGarbageCollectionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The GC type name used by the syntax gate.</summary>
    private const string GcTypeName = "GC";

    /// <summary>The metadata name of the GC type.</summary>
    private const string GcMetadataName = "System.GC";

    /// <summary>The forced-collection method name.</summary>
    private const string CollectMethodName = "Collect";

    /// <summary>The finalizer-drain method name.</summary>
    private const string WaitForPendingFinalizersMethodName = "WaitForPendingFinalizers";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.AvoidForcedGarbageCollection);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(GcMetadataName) is not { } gcType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, gcType),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1021 for a call that manually drives the garbage collector.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="gcType">The compilation's GC type symbol.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol gcType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetForcedGcMemberName(invocation) is not { } memberName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, gcType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.AvoidForcedGarbageCollection,
            invocation.SyntaxTree,
            invocation.Span,
            memberName));
    }

    /// <summary>Returns the forced-GC member name of a <c>GC.Collect</c> / <c>GC.WaitForPendingFinalizers</c> shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The member name when the syntax-only shape matches; otherwise <see langword="null"/>.</returns>
    private static string? TryGetForcedGcMemberName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var memberName = memberAccess.Name.Identifier.ValueText;
        if (memberName is not (CollectMethodName or WaitForPendingFinalizersMethodName))
        {
            return null;
        }

        var receiverIsGc = memberAccess.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == GcTypeName,
            MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.ValueText == GcTypeName,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText == GcTypeName,
            _ => false,
        };

        return receiverIsGc ? memberName : null;
    }
}
