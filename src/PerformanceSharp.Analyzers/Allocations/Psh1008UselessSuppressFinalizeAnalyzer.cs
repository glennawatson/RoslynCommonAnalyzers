// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>GC.SuppressFinalize(this)</c> calls that can never do anything (PSH1008).
/// SuppressFinalize only matters for objects the GC has registered for finalization; when the
/// containing type is sealed (or a struct) and neither it nor any base declares a finalizer, the
/// call is pure per-dispose overhead. Unsealed classes are never reported because a derived type
/// may add a finalizer and rely on the base dispose path suppressing it. The check is
/// syntax-gated on the <c>GC.SuppressFinalize(this)</c> shape before any symbol work.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1008UselessSuppressFinalizeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The suppress method name used by the syntax gate.</summary>
    private const string SuppressFinalizeMethodName = "SuppressFinalize";

    /// <summary>The GC type name used by the syntax gate.</summary>
    private const string GcTypeName = "GC";

    /// <summary>The metadata name of the GC type.</summary>
    private const string GcMetadataName = "System.GC";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UselessSuppressFinalize);

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

    /// <summary>Reports a SuppressFinalize call in a type that can never have a finalizer.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="gcType">The compilation's GC type symbol.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol gcType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!HasSuppressFinalizeThisShape(invocation))
        {
            return;
        }

        if (context.ContainingSymbol?.ContainingType is not { } type || !IsFinalizerFree(type))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, gcType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UselessSuppressFinalize,
            invocation.SyntaxTree,
            invocation.Span,
            type.Name));
    }

    /// <summary>Returns whether an invocation has the <c>GC.SuppressFinalize(this)</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the syntax-only shape matches.</returns>
    private static bool HasSuppressFinalizeThisShape(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 1
            || !invocation.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.ThisExpression))
        {
            return false;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: SuppressFinalizeMethodName } memberAccess)
        {
            return false;
        }

        return memberAccess.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == GcTypeName,
            MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.ValueText == GcTypeName,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText == GcTypeName,
            _ => false,
        };
    }

    /// <summary>Returns whether a type can never be registered for finalization.</summary>
    /// <param name="type">The containing type.</param>
    /// <returns><see langword="true"/> when the type is finalizer-free and cannot gain one.</returns>
    private static bool IsFinalizerFree(INamedTypeSymbol type)
    {
        if (type.IsValueType)
        {
            return true;
        }

        if (type.TypeKind != TypeKind.Class || !type.IsSealed)
        {
            return false;
        }

        for (var current = type; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
        {
            var members = current.GetMembers(WellKnownMemberNames.DestructorName);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol { MethodKind: MethodKind.Destructor })
                {
                    return false;
                }
            }
        }

        return true;
    }
}
