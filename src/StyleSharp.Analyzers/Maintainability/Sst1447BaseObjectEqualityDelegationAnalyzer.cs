// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags <c>base.Equals(obj)</c> and <c>base.GetHashCode()</c> calls that bind to
/// <see cref="object"/>'s implementation from inside an <c>Equals</c> or <c>GetHashCode</c>
/// member (SST1447). Those base calls compare by reference identity, silently defeating the value
/// semantics the override exists to provide. A base call that binds to a real base-class override
/// is fine and never reported. The whole check is syntax-gated: only a <c>base.</c> receiver with
/// the right member name and argument count inside a matching member triggers a semantic bind.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1447BaseObjectEqualityDelegationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The instance <c>Equals</c> argument count.</summary>
    private const int EqualsArgumentCount = 1;

    /// <summary>The equality member name.</summary>
    private const string EqualsName = "Equals";

    /// <summary>The hash member name.</summary>
    private const string GetHashCodeName = "GetHashCode";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.BaseObjectEqualityDelegation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a base equality call that resolves to object's implementation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!HasBaseEqualityShape(invocation, out var memberName))
        {
            return;
        }

        if (FindEnclosingEqualityMember(invocation) is not { } enclosingName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                ContainingType.SpecialType: SpecialType.System_Object,
            })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.BaseObjectEqualityDelegation,
            invocation.GetLocation(),
            enclosingName,
            memberName));
    }

    /// <summary>Returns whether an invocation is <c>base.Equals(x)</c> or <c>base.GetHashCode()</c>.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="memberName">The matched member name.</param>
    /// <returns><see langword="true"/> when the syntax-only shape matches.</returns>
    private static bool HasBaseEqualityShape(InvocationExpressionSyntax invocation, out string memberName)
    {
        memberName = string.Empty;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } memberAccess)
        {
            return false;
        }

        var argumentCount = invocation.ArgumentList.Arguments.Count;
        memberName = memberAccess.Name.Identifier.ValueText switch
        {
            EqualsName when argumentCount == EqualsArgumentCount => EqualsName,
            GetHashCodeName when argumentCount == 0 => GetHashCodeName,
            _ => string.Empty,
        };
        return memberName.Length != 0;
    }

    /// <summary>Finds the enclosing Equals or GetHashCode member declaration name.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns>The enclosing member name, or <see langword="null"/> when unrelated.</returns>
    private static string? FindEnclosingEqualityMember(InvocationExpressionSyntax invocation)
    {
        for (SyntaxNode? current = invocation.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax method:
                {
                    var name = method.Identifier.ValueText;
                    return name is EqualsName or GetHashCodeName ? name : null;
                }

                case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax:
                {
                    return null;
                }
            }
        }

        return null;
    }
}
