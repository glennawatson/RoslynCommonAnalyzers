// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags invocations of <c>Enum.HasFlag</c> like <c>options.HasFlag(Options.Cache)</c>
/// (PSH1016). The call boxes both the value and the argument on runtimes without the JIT
/// intrinsic, while <c>(options &amp; Options.Cache) == Options.Cache</c> compiles to a
/// register test everywhere. Only calls that bind to <see cref="Enum.HasFlag"/> on a
/// receiver whose type is an enum are reported, so a user-defined <c>HasFlag</c> method
/// stays clean.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1016UseBitwiseFlagTestAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string HasFlagMethodName = nameof(Enum.HasFlag);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UseBitwiseFlagTest);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns the member access of an <c>x.HasFlag(flag)</c> shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The <c>HasFlag</c> member access, or <see langword="null"/> when the shape does not match.</returns>
    internal static MemberAccessExpressionSyntax? TryGetHasFlagAccess(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 1
            && invocation.Expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name.Identifier.ValueText == HasFlagMethodName
            ? access
            : null;

    /// <summary>Reports PSH1016 for a HasFlag call that binds to <see cref="Enum.HasFlag"/> on an enum-typed receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetHasFlagAccess(invocation) is not { } access)
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType.SpecialType: SpecialType.System_Enum }
            || model.GetTypeInfo(access.Expression, context.CancellationToken).Type is not { TypeKind: TypeKind.Enum })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UseBitwiseFlagTest,
            invocation.SyntaxTree,
            invocation.Span));
    }
}
