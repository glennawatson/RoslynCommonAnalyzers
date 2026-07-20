// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call to <c>SafeHandle.DangerousGetHandle()</c> (SST2484). The returned <c>IntPtr</c> is the raw
/// operating-system handle, handed back without a reference count. Once the call returns, nothing keeps the
/// handle alive: a concurrent dispose or a finalize of the safe handle frees the underlying handle, the value
/// can be recycled to an unrelated resource, and code still holding the raw value reads it after free — a crash
/// or a cross-object handle confusion. Callers should bracket the raw value with <c>DangerousAddRef</c>/
/// <c>DangerousRelease</c>, or avoid the raw handle entirely.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is a syntactic prepass: the node must be a parameterless invocation whose invoked member is
/// named <c>DangerousGetHandle</c>. Only then is the call bound, to confirm it resolves to the method declared
/// on <see cref="System.Runtime.InteropServices.SafeHandle"/> (or an override reached through a derived safe
/// handle) rather than an unrelated same-named method. Every other invocation in the file fails the name check
/// and never binds.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>System.Runtime.InteropServices.SafeHandle</c> resolving,
/// so a compilation that has no safe handle type pays nothing. There is no code fix: the correct remedy is a
/// reference-counting protocol around the raw handle, not a mechanical rewrite of the call.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2484DangerousGetHandleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The dangerous handle-reading method name.</summary>
    private const string DangerousGetHandleName = "DangerousGetHandle";

    /// <summary>The metadata name of the safe-handle base type.</summary>
    private const string SafeHandleMetadataName = "System.Runtime.InteropServices.SafeHandle";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.DangerousGetHandle);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(SafeHandleMetadataName) is not { } safeHandleType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, safeHandleType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports one raw handle read through a safe handle's dangerous accessor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="safeHandleType">The compilation's safe-handle type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol safeHandleType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0 || GetInvokedName(invocation) != DangerousGetHandleName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: DangerousGetHandleName } method
            || !IsSafeHandleOrDerived(method.ContainingType, safeHandleType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DangerousGetHandle,
            invocation.GetLocation()));
    }

    /// <summary>Returns the invoked member's simple name text for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static string? GetInvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether a type is the safe-handle type or derives from it.</summary>
    /// <param name="type">The method's containing type.</param>
    /// <param name="safeHandleType">The compilation's safe-handle type.</param>
    /// <returns><see langword="true"/> when the call belongs to a safe handle.</returns>
    private static bool IsSafeHandleOrDerived(INamedTypeSymbol? type, INamedTypeSymbol safeHandleType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, safeHandleType))
            {
                return true;
            }
        }

        return false;
    }
}
