// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>DotNetObjectReference.Create(...)</c> whose result is not stored in a field or property
/// (SST2713). The runtime keeps the created reference alive in a per-circuit map until it is disposed, so a
/// reference passed straight into an interop call — or otherwise dropped — is never reachable to dispose and
/// leaks for the life of the circuit.
/// </summary>
/// <remarks>
/// <para>
/// This reports only the not-stored shapes: the create call is a bare statement, the right side of a
/// <c>_ = ...</c> discard, or an argument of a method invocation. A reference assigned to a field or property is
/// stored and left to the disposable-ownership rules; a local, a <c>using</c> declaration, a field initializer,
/// and a <c>return</c> are all left alone, so a reference the component can still reach — or hand off — is never
/// reported.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on the <c>Microsoft.JSInterop.DotNetObjectReference</c> marker
/// resolving, so a project that does no JavaScript interop registers nothing. The clean path is a syntactic
/// shape probe — a <c>Create</c> member access whose receiver is named <c>DotNetObjectReference</c>, in a
/// not-stored position — before any binding; the semantic model is consulted only once that shape matches, to
/// confirm the call is the static <c>DotNetObjectReference.Create</c> factory.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2713UnstoredDotNetObjectReferenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the static factory that creates the callback reference.</summary>
    private const string DotNetObjectReferenceMetadataName = "Microsoft.JSInterop.DotNetObjectReference";

    /// <summary>The simple name of the factory type, matched syntactically before binding.</summary>
    private const string DotNetObjectReferenceTypeName = "DotNetObjectReference";

    /// <summary>The name of the factory method the rule reports.</summary>
    private const string CreateMethodName = "Create";

    /// <summary>The identifier a discard target carries.</summary>
    private const string DiscardName = "_";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.UnstoredDotNetObjectReference);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var reference = start.Compilation.GetTypeByMetadataName(DotNetObjectReferenceMetadataName);
            if (reference is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, reference), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports a not-stored <c>DotNetObjectReference.Create</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="reference">The resolved <c>DotNetObjectReference</c> factory type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol reference)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: 'DotNetObjectReference.Create(...)' in a not-stored position.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: CreateMethodName } memberAccess
            || !IsFactoryReceiver(memberAccess.Expression)
            || !IsNotStoredPosition(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: CreateMethodName, IsStatic: true, ContainingType: { } container }
            || !SymbolEqualityComparer.Default.Equals(container, reference))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.UnstoredDotNetObjectReference, invocation.GetLocation()));
    }

    /// <summary>Returns whether a call receiver's simple name is <c>DotNetObjectReference</c>.</summary>
    /// <param name="receiver">The receiver of the <c>Create</c> member access.</param>
    /// <returns><see langword="true"/> for <c>DotNetObjectReference</c> or a qualified name that ends in it.</returns>
    private static bool IsFactoryReceiver(ExpressionSyntax receiver)
        => receiver switch
        {
            IdentifierNameSyntax identifier => string.Equals(identifier.Identifier.ValueText, DotNetObjectReferenceTypeName, StringComparison.Ordinal),
            MemberAccessExpressionSyntax memberAccess => string.Equals(memberAccess.Name.Identifier.ValueText, DotNetObjectReferenceTypeName, StringComparison.Ordinal),
            _ => false,
        };

    /// <summary>Returns whether the create call's result is dropped rather than stored in a field or property.</summary>
    /// <param name="invocation">The <c>Create</c> invocation.</param>
    /// <returns><see langword="true"/> for a bare statement, a <c>_ = ...</c> discard, or a method-call argument.</returns>
    private static bool IsNotStoredPosition(InvocationExpressionSyntax invocation)
        => invocation.Parent switch
        {
            ExpressionStatementSyntax => true,
            ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax } } => true,
            AssignmentExpressionSyntax assignment
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && assignment.Right == invocation
                    && assignment.Left is IdentifierNameSyntax { Identifier.ValueText: DiscardName }
                    && assignment.Parent is ExpressionStatementSyntax => true,
            _ => false,
        };
}
