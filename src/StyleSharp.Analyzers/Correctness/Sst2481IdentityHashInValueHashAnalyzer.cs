// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>base.GetHashCode()</c> call folded into a value hash inside a <c>GetHashCode</c> override whose
/// base is <see cref="object"/> (SST2481) — that is, where the base call is combined with other state rather than
/// being the member's whole result. <c>object.GetHashCode()</c> is the runtime identity hash, tied to the
/// reference and not to the fields, so mixing it into a hash built from those fields makes two value-equal
/// instances hash differently and lose each other in a dictionary or hash set.
/// </summary>
/// <remarks>
/// <para>
/// A base call that binds to a real base-class value hash is a legitimate chain and is never reported, and a
/// <c>GetHashCode</c> that returns <c>base.GetHashCode()</c> outright — reference delegation rather than mixing —
/// is the whole result of the member and is left to the reference-delegation rule so the two never double up.
/// Structs are not reported: a struct's <c>base.GetHashCode()</c> is the reflection-based value-type hash, which
/// is deterministic on the field values, so folding it in is a performance concern rather than a lookup bug.
/// </para>
/// <para>
/// The shape is settled syntactically before any bind: a parameterless <c>base.GetHashCode()</c> that is not the
/// member's whole result, sitting inside a parameterless <c>GetHashCode</c> override. Only then is the call bound,
/// to confirm it reaches <see cref="object"/>'s implementation rather than a base class's own value hash. Every
/// other invocation in the file fails the first pattern check and never binds.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2481IdentityHashInValueHashAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The hash member name.</summary>
    private const string GetHashCodeName = "GetHashCode";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.IdentityHashInValueHash);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one base identity-hash call folded into a value hash.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsBaseGetHashCodeCall(invocation))
        {
            return;
        }

        // A base call that is the member's whole result is reference delegation, not mixing, and belongs to the
        // reference-delegation rule; only a call combined with other state folds the identity hash into a value hash.
        if (IsWholeResult(invocation) || !IsInsideHashOverride(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                Name: GetHashCodeName,
                ContainingType.SpecialType: SpecialType.System_Object,
            })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.IdentityHashInValueHash,
            invocation.GetLocation()));
    }

    /// <summary>Returns whether an invocation is a parameterless <c>base.GetHashCode()</c>.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the syntax-only shape matches.</returns>
    private static bool IsBaseGetHashCodeCall(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax, Name.Identifier.ValueText: GetHashCodeName }
            && invocation.ArgumentList.Arguments.Count == 0;

    /// <summary>Returns whether the base call is the entire value the member yields.</summary>
    /// <param name="invocation">The base hash invocation.</param>
    /// <returns><see langword="true"/> when the call is the member's expression body or a returned expression.</returns>
    private static bool IsWholeResult(InvocationExpressionSyntax invocation)
    {
        SyntaxNode node = invocation;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        return node.Parent switch
        {
            ArrowExpressionClauseSyntax arrow => arrow.Expression == node,
            ReturnStatementSyntax returnStatement => returnStatement.Expression == node,
            _ => false,
        };
    }

    /// <summary>Returns whether the node sits inside a parameterless <c>GetHashCode</c> override.</summary>
    /// <param name="node">The invocation being inspected.</param>
    /// <returns><see langword="true"/> when the nearest member is the hash override rather than a lambda or local function.</returns>
    private static bool IsInsideHashOverride(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText == GetHashCodeName
                        && method.ParameterList.Parameters.Count == 0
                        && ModifierListHelper.Contains(method.Modifiers, SyntaxKind.OverrideKeyword);

                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
