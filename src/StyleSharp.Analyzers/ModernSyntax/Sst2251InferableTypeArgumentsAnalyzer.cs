// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an explicit type-argument list on a method call that type inference would supply unchanged
/// (SST2251), so the type arguments can be dropped without changing which method the call binds to.
/// </summary>
/// <remarks>
/// <para>
/// A free syntactic check comes first: only a call whose invoked name carries a type-argument list —
/// <c>Create&lt;int&gt;(value)</c> or <c>receiver.Create&lt;int&gt;(value)</c> — is looked at. Type arguments on
/// a receiver type (<c>Box&lt;int&gt;.Make(value)</c>) belong to the type, not the call, and are never touched.
/// Object creations and attributes are not invocations and so are out of scope.
/// </para>
/// <para>
/// The safety of the rule is a speculative bind, never a guess. The call is bound once with its type
/// arguments; then the same call is bound with the type-argument list removed, and the diagnostic is
/// raised only when the shortened form resolves to the identical constructed method — the same original
/// definition and the same inferred type arguments. If inference fails, is required, moves the call to a
/// different overload, or lands on different type arguments, the two symbols differ and nothing is
/// reported. A rule that quietly re-targets a call or offers a fix that does not compile is worse than one
/// that stays quiet.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2251InferableTypeArgumentsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.OmitInferableTypeArguments);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns the generic name that carries the call's explicit type arguments, when there is one.</summary>
    /// <param name="invocation">The invocation being inspected.</param>
    /// <param name="genericName">The generic method name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the invoked name has a non-empty type-argument list.</returns>
    /// <remarks>
    /// Only the invoked name is considered — the whole expression for an unqualified call, or the member being
    /// accessed for a qualified call. A generic name anywhere else (the receiver's type, or an argument) is left
    /// alone. A conditional-access call (<c>receiver?.Create&lt;int&gt;(value)</c>) is not matched: its member
    /// binding has no receiver of its own to bind the shortened call against, so the safety check cannot run.
    /// </remarks>
    internal static bool TryGetExplicitTypeArguments(InvocationExpressionSyntax invocation, out GenericNameSyntax? genericName)
    {
        genericName = invocation.Expression switch
        {
            GenericNameSyntax name => name,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name,
            _ => null,
        };

        if (genericName is null)
        {
            return false;
        }

        var typeArguments = genericName.TypeArgumentList.Arguments;
        if (typeArguments.Count == 0)
        {
            genericName = null;
            return false;
        }

        for (var i = 0; i < typeArguments.Count; i++)
        {
            if (typeArguments[i].IsKind(SyntaxKind.OmittedTypeArgument))
            {
                genericName = null;
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the call binds to the same constructed method once its type arguments are removed.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation being inspected.</param>
    /// <param name="genericName">The generic name carrying the explicit type arguments.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when inference reproduces the identical constructed method.</returns>
    internal static bool OmissionKeepsTheSameMethod(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        GenericNameSyntax genericName,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { IsGenericMethod: true } before)
        {
            return false;
        }

        var withoutTypeArguments = SyntaxFactory.IdentifierName(genericName.Identifier);
        var rewritten = invocation.ReplaceNode(genericName, withoutTypeArguments);
        var after = model.GetSpeculativeSymbolInfo(invocation.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol;

        return after is IMethodSymbol && SymbolEqualityComparer.Default.Equals(after, before);
    }

    /// <summary>Reports the redundant type-argument list on an invocation.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetExplicitTypeArguments(invocation, out var genericName)
            || !OmissionKeepsTheSameMethod(context.SemanticModel, invocation, genericName!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ModernSyntaxRules.OmitInferableTypeArguments,
            genericName!.TypeArgumentList.GetLocation()));
    }
}
