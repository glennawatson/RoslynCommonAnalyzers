// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports <c>StateHasChanged()</c> called from the <c>Dispose</c> or <c>DisposeAsync</c> body of a
/// component (SST2709). Disposal runs while the renderer is tearing the component down, so its place in the
/// render tree is already gone; requesting a render then is unsupported and throws at runtime every time
/// the component is disposed.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on <c>ComponentBase</c> resolving, so a non-component
/// project registers nothing. The clean path is a syntactic shape probe first — the callee names
/// <c>StateHasChanged</c> on <c>this</c>/<c>base</c>, and the nearest enclosing member (not crossing a
/// lambda or local function, which could run later) is a <c>Dispose</c>/<c>DisposeAsync</c> method — and
/// the semantic model is consulted only once that shape matches, to confirm the enclosing type is a
/// component and the call binds to <c>ComponentBase.StateHasChanged</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2709StateHasChangedInDisposeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The synchronous disposal method name.</summary>
    private const string DisposeName = "Dispose";

    /// <summary>The asynchronous disposal method name.</summary>
    private const string DisposeAsyncName = "DisposeAsync";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(FrameworksRules.StateHasChangedDuringDisposal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (BlazorComponentModel.Create(start.Compilation) is not { } model)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, model), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports a render requested from a disposal method.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="model">The component model resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, BlazorComponentModel model)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!BlazorComponentModel.IsSelfStateHasChangedSyntax(invocation.Expression)
            || FindEnclosingDisposeMethod(invocation) is null)
        {
            return;
        }

        if (invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } typeDeclaration
            || context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is not { } type
            || !model.DerivesFromComponentBase(type))
        {
            return;
        }

        if (!model.IsStateHasChanged(context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(FrameworksRules.StateHasChangedDuringDisposal, invocation.GetLocation(), type.Name));
    }

    /// <summary>Finds the disposal method directly enclosing a node, without crossing a deferred-execution boundary.</summary>
    /// <param name="node">The node to search upward from.</param>
    /// <returns>The enclosing <c>Dispose</c>/<c>DisposeAsync</c> method, or <see langword="null"/> when there is none.</returns>
    private static MethodDeclarationSyntax? FindEnclosingDisposeMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    return null;
                case MethodDeclarationSyntax method:
                    return IsDisposeMethodName(method.Identifier.ValueText) ? method : null;
                case TypeDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Returns whether a method name is a disposal method name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> for <c>Dispose</c> or <c>DisposeAsync</c>.</returns>
    private static bool IsDisposeMethodName(string name)
        => string.Equals(name, DisposeName, StringComparison.Ordinal) || string.Equals(name, DisposeAsyncName, StringComparison.Ordinal);
}
